using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class TrainingLobby : MonoBehaviour
{
    [SerializeField] private TrainingCatalog catalog;

    private sealed class Choice
    {
        public Image panel;
        public Action action;
    }

    private readonly List<Choice> choices = new List<Choice>();
    private OVRCameraRig rig;
    private RectTransform canvas;
    private RectTransform content;
    private TextMeshProUGUI heading;
    private TextMeshProUGUI subtitle;
    private TextMeshProUGUI status;
    private Image pointerDot;
    private int hoveredIndex = -1;
    private TrainingCatalog.Scenario selectedScenario;
    private int selectedIndex;
    private int page;
    private bool stickReleased = true;
    private bool ready;
    private bool controllerWasTracked;
    private readonly Color normalColor = new Color(0.10f, 0.20f, 0.28f, 1f);
    private readonly Color selectedColor = new Color(0.03f, 0.46f, 0.53f, 1f);
    private const int PageSize = 4;

    private IEnumerator Start()
    {
        if (catalog == null)
        {
            catalog = Resources.Load<TrainingCatalog>("TrainingCatalog");
        }
        if (catalog == null)
        {
            Debug.LogError("Training catalog is missing.", this);
            yield break;
        }
        GameObject cameraRoot = new GameObject("Lobby Camera Rig");
        cameraRoot.transform.SetParent(transform, false);
        rig = cameraRoot.AddComponent<OVRCameraRig>();
        OVRManager manager = cameraRoot.AddComponent<OVRManager>();
        manager.isInsightPassthroughEnabled = false;
        manager.trackingOriginType = OVRManager.TrackingOrigin.FloorLevel;
        Camera camera = rig.centerEyeAnchor.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.025f, 0.045f, 0.07f);
        camera.nearClipPlane = 0.05f;
        if (rig.centerEyeAnchor.GetComponent<AudioListener>() == null)
        {
            rig.centerEyeAnchor.gameObject.AddComponent<AudioListener>();
        }
        canvas = TrainingUI.Canvas("Training Lobby", transform, new Vector2(1050f, 800f));
        TrainingUI.Panel(canvas, "Background", Vector2.zero, canvas.sizeDelta, new Color(0.04f, 0.08f, 0.12f));
        TrainingUI.Label(canvas, catalog.font, "RAD CARE  /  TRAINING CENTER", new Vector2(0f, 340f), new Vector2(970f, 45f), 24f);
        heading = TrainingUI.Label(canvas, catalog.font, "", new Vector2(0f, 265f), new Vector2(970f, 65f), 42f);
        subtitle = TrainingUI.Label(canvas, catalog.font, "", new Vector2(0f, 190f), new Vector2(970f, 80f), 24f);
        GameObject contentObject = new GameObject("Choices", typeof(RectTransform));
        contentObject.transform.SetParent(canvas, false);
        content = (RectTransform)contentObject.transform;
        status = TrainingUI.Label(canvas, catalog.font, "", new Vector2(0f, -265f), new Vector2(970f, 50f), 22f);
        TrainingUI.Label(canvas, catalog.font,
            "오른손으로 가리킨 뒤 트리거 / 왼쪽 스틱 + A 선택\nB: 이전 화면    Y: 메뉴 위치 재정렬",
            new Vector2(0f, -335f), new Vector2(970f, 85f), 22f);
        ShowScenarios();
        pointerDot = TrainingUI.Panel(canvas, "Controller Pointer", Vector2.zero,
            new Vector2(12f, 12f), new Color(0.4f, 1f, 0.9f));
        pointerDot.gameObject.SetActive(false);
        Recenter();
        yield return new WaitForSecondsRealtime(0.5f);
        Recenter();
        ready = true;
    }

    private void Recenter()
    {
        Transform head = rig.centerEyeAnchor;
        Quaternion facing = Quaternion.Euler(0f, head.eulerAngles.y, 0f);
        canvas.SetPositionAndRotation(head.position + facing * new Vector3(0f, 0f, 1.6f), facing);
    }

    private void ClearChoices()
    {
        foreach (Choice choice in choices)
        {
            choice.panel.gameObject.SetActive(false);
            Destroy(choice.panel.gameObject);
        }
        choices.Clear();
        selectedIndex = 0;
        hoveredIndex = -1;
        status.text = "";
    }

    private void AddChoice(string text, Vector2 position, Vector2 size, Action action)
    {
        Image panel = TrainingUI.Panel(content, text, position, size, normalColor);
        TrainingUI.Label(panel.transform, catalog.font, text, Vector2.zero, size - new Vector2(30f, 10f), 28f);
        choices.Add(new Choice { panel = panel, action = action });
    }

    private void ShowScenarios()
    {
        ClearChoices();
        selectedScenario = null;
        heading.text = "교육 시나리오 선택";
        subtitle.text = "학습할 시나리오를 선택하세요.";
        int pageCount = Mathf.Max(1, Mathf.CeilToInt(catalog.scenarios.Length / (float)PageSize));
        page = Mathf.Clamp(page, 0, pageCount - 1);
        for (int index = page * PageSize; index < Mathf.Min((page + 1) * PageSize, catalog.scenarios.Length); index++)
        {
            TrainingCatalog.Scenario scenario = catalog.scenarios[index];
            if (scenario == null)
            {
                continue;
            }
            AddChoice(scenario.title, new Vector2(0f, 100f - (index % PageSize) * 85f),
                new Vector2(930f, 70f), () => ShowModes(scenario));
        }
        if (pageCount > 1)
        {
            AddChoice("이전 목록", new Vector2(-240f, -225f), new Vector2(440f, 55f),
                () => { page = (page + pageCount - 1) % pageCount; ShowScenarios(); });
            AddChoice("다음 목록", new Vector2(240f, -225f), new Vector2(440f, 55f),
                () => { page = (page + 1) % pageCount; ShowScenarios(); });
        }
        if (catalog.scenarios.Length == 0)
        {
            status.text = "등록된 시나리오가 없습니다.";
        }
        Highlight();
    }

    private void ShowModes(TrainingCatalog.Scenario scenario)
    {
        ClearChoices();
        selectedScenario = scenario;
        heading.text = scenario.title;
        subtitle.text = scenario.description;
        AddChoice("튜토리얼\n음성 + 발광 안내로 순서 익히기", new Vector2(0f, 80f),
            new Vector2(930f, 100f), () => Launch(scenario.tutorialScene));
        AddChoice("평가\n발광 없이 수행 / 잘못된 도구 선택 기록", new Vector2(0f, -45f),
            new Vector2(930f, 100f), () => Launch(scenario.evaluationScene));
        AddChoice("시나리오 다시 선택", new Vector2(0f, -165f),
            new Vector2(930f, 65f), ShowScenarios);
        Highlight();
    }

    private void Launch(string path)
    {
        TrainingNavigation navigation = TrainingNavigation.Instance;
        if (navigation != null && navigation.TryLoad(path))
        {
            status.text = "교육 씬을 불러오는 중입니다...";
        }
        else
        {
            status.text = navigation == null ? "씬 이동 관리자를 찾을 수 없습니다." : navigation.Error;
        }
    }

    private void Highlight()
    {
        for (int index = 0; index < choices.Count; index++)
        {
            choices[index].panel.color = index == selectedIndex || index == hoveredIndex ? selectedColor : normalColor;
        }
    }

    private int PointedChoice()
    {
        pointerDot.gameObject.SetActive(false);
        Transform pointer = rig.rightControllerAnchor;
        Ray ray = new Ray(pointer.position, pointer.forward);
        Plane plane = new Plane(canvas.forward, canvas.position);
        if (!plane.Raycast(ray, out float distance) || distance > 5f)
        {
            return -1;
        }
        Vector3 hit = ray.GetPoint(distance);
        Vector3 localHit = canvas.InverseTransformPoint(hit);
        if (canvas.rect.Contains(localHit))
        {
            pointerDot.gameObject.SetActive(true);
            pointerDot.rectTransform.anchoredPosition = localHit;
        }
        for (int index = 0; index < choices.Count; index++)
        {
            RectTransform rect = choices[index].panel.rectTransform;
            if (rect.rect.Contains(rect.InverseTransformPoint(hit)))
            {
                return index;
            }
        }
        return -1;
    }

    private void Update()
    {
        if (!ready || TrainingNavigation.Instance == null || TrainingNavigation.Instance.IsLoading)
        {
            return;
        }
        Keyboard keyboard = Application.isEditor ? Keyboard.current : null;
        bool focused = OVRManager.hasInputFocus;
        bool tracked = focused && OVRInput.IsControllerConnected(OVRInput.Controller.RTouch)
            && OVRInput.GetControllerPositionTracked(OVRInput.Controller.RTouch);
        if (tracked && !controllerWasTracked)
        {
            Recenter();
        }
        controllerWasTracked = tracked;
        int pointedIndex = tracked ? PointedChoice() : -1;
        if (!tracked)
        {
            pointerDot.gameObject.SetActive(false);
        }
        if (hoveredIndex != pointedIndex)
        {
            hoveredIndex = pointedIndex;
            Highlight();
        }
        if ((focused && OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.LTouch))
            || (keyboard != null && keyboard.rKey.wasPressedThisFrame))
        {
            Recenter();
        }
        if ((focused && OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.RTouch))
            || (keyboard != null && keyboard.backspaceKey.wasPressedThisFrame))
        {
            if (selectedScenario != null)
            {
                ShowScenarios();
            }
            return;
        }
        float stick = focused ? OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch).y : 0f;
        int direction = 0;
        if (Mathf.Abs(stick) < 0.3f)
        {
            stickReleased = true;
        }
        else if (stickReleased && Mathf.Abs(stick) > 0.6f)
        {
            direction = stick > 0f ? -1 : 1;
            stickReleased = false;
        }
        if (keyboard != null)
        {
            if (keyboard.upArrowKey.wasPressedThisFrame) direction = -1;
            if (keyboard.downArrowKey.wasPressedThisFrame) direction = 1;
        }
        if (choices.Count == 0)
        {
            return;
        }
        if (direction != 0)
        {
            selectedIndex = (selectedIndex + direction + choices.Count) % choices.Count;
            Highlight();
        }
        bool trigger = tracked && OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch);
        if (trigger)
        {
            int pointed = pointedIndex;
            if (pointed >= 0)
            {
                selectedIndex = pointed;
                Highlight();
                choices[selectedIndex].action();
            }
        }
        else if ((focused && OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch))
            || (keyboard != null && keyboard.enterKey.wasPressedThisFrame))
        {
            choices[selectedIndex].action();
        }
    }
}
