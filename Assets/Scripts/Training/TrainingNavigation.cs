using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class TrainingNavigation : MonoBehaviour
{
    public static TrainingNavigation Instance { get; private set; }
    public bool IsLoading { get; private set; }
    public string Error { get; private set; }

    private TrainingCatalog catalog;
    private TextMeshProUGUI returnLabel;
    private float returnHeld;
    private bool returnArmed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic()
    {
        Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (Instance != null)
        {
            return;
        }
        TrainingCatalog available = Resources.Load<TrainingCatalog>("TrainingCatalog");
        if (available == null)
        {
            return;
        }
        GameObject root = new GameObject("Training Navigation");
        TrainingNavigation navigation = root.AddComponent<TrainingNavigation>();
        navigation.catalog = available;
        Instance = navigation;
        DontDestroyOnLoad(root);
        SceneManager.sceneLoaded += navigation.OnSceneLoaded;
        navigation.OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        returnHeld = 0f;
        returnArmed = false;
        returnLabel = null;
        if (!catalog.ContainsScene(scene.path))
        {
            return;
        }
        OVRCameraRig rig = FindAnyObjectByType<OVRCameraRig>();
        if (rig == null)
        {
            return;
        }
        RectTransform canvas = TrainingUI.Canvas("Return to Lobby", rig.centerEyeAnchor, new Vector2(800f, 50f));
        canvas.localPosition = new Vector3(0f, 0.38f, 1.3f);
        returnLabel = TrainingUI.Label(canvas, catalog.font, "왼쪽 메뉴 버튼 1.5초 누르기: 로비",
            Vector2.zero, new Vector2(800f, 50f), 20f);
    }

    private void Update()
    {
        if (IsLoading || returnLabel == null)
        {
            return;
        }
        bool held = (OVRManager.hasInputFocus && OVRInput.Get(OVRInput.Button.Start, OVRInput.Controller.LTouch))
            || (Application.isEditor && Keyboard.current != null && Keyboard.current.escapeKey.isPressed);
        if (!held)
        {
            returnArmed = true;
            returnHeld = 0f;
            returnLabel.text = string.IsNullOrEmpty(Error) ? "왼쪽 메뉴 버튼 1.5초 누르기: 로비" : Error;
            return;
        }
        if (!returnArmed)
        {
            return;
        }
        returnHeld += Time.unscaledDeltaTime;
        returnLabel.text = $"로비로 돌아가기... {Mathf.CeilToInt(Mathf.Clamp01(returnHeld / 1.5f) * 100f)}%";
        if (returnHeld >= 1.5f)
        {
            returnArmed = false;
            TryLoad(catalog.lobbyScene);
        }
    }

    public bool TryLoad(string scenePath)
    {
        if (IsLoading)
        {
            return false;
        }
        if (string.IsNullOrWhiteSpace(scenePath) || !Application.CanStreamedLevelBeLoaded(scenePath))
        {
            Error = "씬이 빌드 목록에 없습니다. Build Profiles를 확인해주세요.";
            Debug.LogError(Error + " " + scenePath, this);
            return false;
        }
        Error = null;
        IsLoading = true;
        StartCoroutine(Load(scenePath));
        return true;
    }

    private IEnumerator Load(string scenePath)
    {
        SoundManager sound = SoundManager.Instance;
        if (sound != null)
        {
            sound.StopAllCoroutines();
            foreach (AudioSource source in sound.GetComponentsInChildren<AudioSource>(true))
            {
                source.Stop();
            }
            SoundManager.Instance = null;
            Destroy(sound.gameObject);
        }
        EvaluationSoundManager.Instance?.ResetPlayback();
        yield return null;
        AsyncOperation operation = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Single);
        yield return operation;
        IsLoading = false;
    }
}
