using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;

public enum EvaluationStep
{
    Briefing,
    SurveyMeterInteraction,
    SurveyMeterWarningInteraction,
    SalineInteraction,
    ForcepsInteraction,
    DisposeStage,
    ResultVerification,
    End
}

public class EvaluationGameManager : MonoBehaviour
{
    public static EvaluationGameManager Instance;
    public event Action<EvaluationStep> OnStepChanged;

    public CanvasGroup mainUI;
    public TextMeshProUGUI uIText;
    [SerializeField] private OVRHand leftHand;
    [SerializeField] private bool useControllers;
    [SerializeField] private EvaluationStep firstStep = EvaluationStep.SurveyMeterInteraction;

    private EvaluationStep _currentStep = EvaluationStep.Briefing;
    private bool wasStartPressed;
    private bool salineContactReported;
    private int treatmentProgress;

    public EvaluationStep currentStep => _currentStep;
    public bool IsTreatmentComplete { get; private set; }
    public bool IsWashingComplete { get; private set; }
    public bool IsWasteDisposed { get; private set; }
    public bool CanScan => !IsRestarting && (currentStep == EvaluationStep.SurveyMeterInteraction
        || currentStep == EvaluationStep.SurveyMeterWarningInteraction);
    public bool CanDetectContamination => CanScan && !IsTreatmentComplete;
    public bool CanVerifyTreatment => !IsRestarting && currentStep == EvaluationStep.SurveyMeterInteraction && IsTreatmentComplete;
    public bool CanWash => !IsRestarting && currentStep == EvaluationStep.SalineInteraction && !IsWashingComplete;
    public bool CanUseSaline => !IsRestarting && currentStep == EvaluationStep.SalineInteraction;
    public bool CanTreat => !IsRestarting && currentStep == EvaluationStep.ForcepsInteraction;
    public bool CanDispose => !IsRestarting && currentStep == EvaluationStep.DisposeStage && !IsWasteDisposed;
    public bool CanReturnSurveyMeter => !IsRestarting && (currentStep == EvaluationStep.SurveyMeterWarningInteraction
        || currentStep == EvaluationStep.ResultVerification);
    public bool CanReturnSaline => CanUseSaline && IsWashingComplete;
    public bool CanReturnForceps => !IsRestarting && currentStep == EvaluationStep.DisposeStage && IsWasteDisposed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            WriteRecord(currentStep == EvaluationStep.End);
            CancelInvoke();
            Instance = null;
        }
    }

    private void LateUpdate()
    {
        bool startPressed = useControllers
            ? OVRInput.Get(OVRInput.Button.One, OVRInput.Controller.RTouch)
            : leftHand != null && leftHand.GetFingerIsPinching(OVRHand.HandFinger.Index);
        if (startPressed && !wasStartPressed)
        {
            GameStart(true);
        }
        wasStartPressed = startPressed;
    }

    private void GameStart(bool startPressed)
    {
        if (!startPressed || currentStep != EvaluationStep.Briefing)
        {
            return;
        }
        if (firstStep != EvaluationStep.SurveyMeterInteraction && firstStep != EvaluationStep.SalineInteraction)
        {
            Debug.LogError("The scenario must start with measurement or saline treatment.", this);
            return;
        }
        if (mainUI != null)
        {
            mainUI.alpha = 0f;
            mainUI.blocksRaycasts = false;
        }
        CapturePlayerStart();
        PlayNarration(sound => sound.NPC1);
        ChangeStep(firstStep);
        if (uIText != null)
        {
            uIText.text = "제염 미션 완료. 결과 리포트를 출력합니다";
        }
    }

    public bool ReportMeasurementCompleted()
    {
        if (IsRestarting || currentStep != EvaluationStep.SurveyMeterInteraction)
        {
            return false;
        }
        ChangeStep(IsTreatmentComplete ? EvaluationStep.ResultVerification : EvaluationStep.SurveyMeterWarningInteraction);
        return true;
    }

    public bool ReportSurveyMeterReturned()
    {
        if (!CanReturnSurveyMeter)
        {
            return false;
        }
        ChangeStep(currentStep == EvaluationStep.ResultVerification ? EvaluationStep.End : EvaluationStep.SalineInteraction);
        return true;
    }

    public void ReportSalineContact()
    {
        if (!CanUseSaline || salineContactReported)
        {
            return;
        }
        salineContactReported = true;
        PlayNarration(sound => sound.NPC4);
    }

    public bool ReportWashingCompleted()
    {
        if (!CanWash)
        {
            return false;
        }
        IsWashingComplete = true;
        EvaluationSoundManager.Instance?.StopSFX();
        PlayNarration(sound => sound.NPC5);
        return true;
    }

    public bool ReportSalineReturned()
    {
        if (!CanReturnSaline)
        {
            return false;
        }
        ChangeStep(EvaluationStep.ForcepsInteraction);
        return true;
    }

    public void ReportTreatmentProgress(int milestone)
    {
        if (!CanTreat || milestone != treatmentProgress + 1 || milestone > 3)
        {
            return;
        }
        treatmentProgress = milestone;
        PlayNarration(sound => milestone == 1 ? sound.NPC6 : milestone == 2 ? sound.NPC7 : sound.NPC8);
    }

    public bool ReportTreatmentCompleted()
    {
        if (!CanTreat || treatmentProgress != 3)
        {
            return false;
        }
        ChangeStep(EvaluationStep.DisposeStage);
        Invoke(nameof(PlayDisposalInstruction), 3f);
        return true;
    }

    public bool ReportWasteDisposed()
    {
        if (!CanDispose)
        {
            return false;
        }
        IsWasteDisposed = true;
        CancelInvoke(nameof(PlayDisposalInstruction));
        if (EvaluationSoundManager.Instance != null)
        {
            EvaluationSoundManager.Instance.PlaySFXOneShot(EvaluationSoundManager.Instance.trashSound);
        }
        PlayNarration(sound => sound.NPC10);
        return true;
    }

    public bool ReportForcepsReturned()
    {
        if (!CanReturnForceps)
        {
            return false;
        }
        IsTreatmentComplete = true;
        ChangeStep(EvaluationStep.SurveyMeterInteraction);
        return true;
    }

    private void ChangeStep(EvaluationStep nextStep)
    {
        if (_currentStep == nextStep)
        {
            return;
        }
        _currentStep = nextStep;
        checkpointPending = true;
        stageVersion++;
        EvaluationSoundManager.Instance?.StopSFX();
        OnStepChanged?.Invoke(nextStep);
        switch (nextStep)
        {
            case EvaluationStep.SurveyMeterWarningInteraction:
                PlayNarration(sound => sound.NPC2);
                break;
            case EvaluationStep.SalineInteraction:
                PlayNarration(sound => sound.NPC3);
                break;
            case EvaluationStep.ResultVerification:
                PlayNarration(sound => sound.NPC11);
                break;
            case EvaluationStep.End:
                PlayNarration(sound => sound.NPC12);
                GameResult();
                break;
        }
        if (nextStep != EvaluationStep.End)
        {
            StartCoroutine(CaptureCheckpoint(stageVersion));
        }
        else
        {
            checkpointPending = false;
            WriteRecord(true);
        }
        Debug.Log($"평가 단계 변경됨: {nextStep}", this);
    }

    private void PlayDisposalInstruction()
    {
        if (CanDispose)
        {
            PlayNarration(sound => sound.NPC9);
        }
    }

    private static void PlayNarration(Func<EvaluationSoundManager, AudioClip> selectClip)
    {
        if (EvaluationSoundManager.Instance != null)
        {
            EvaluationSoundManager.Instance.PlayNPC(selectClip(EvaluationSoundManager.Instance));
        }
    }

    public void GameResult()
    {
        if (currentStep == EvaluationStep.End && mainUI != null)
        {
            if (uIText != null)
            {
                uIText.text = $"평가 완료\n실수 횟수: {MistakeCount}회";
            }
            mainUI.alpha = 1f;
            mainUI.blocksRaycasts = true;
        }
    }
    public EvaluationQuestTriggerGrab Grabber { get; private set; }
    public bool IsRestarting { get; private set; }
    public bool IsRunning => currentStep != EvaluationStep.Briefing && currentStep != EvaluationStep.End;
    public bool CanAcceptGrab => IsRunning && !IsRestarting && !checkpointPending;
    public int MistakeCount => session.mistakes.Count;

    private EvaluationTool[] tools;
    private EvaluationWasteContainer[] containers;
    private OVRCameraRig playerRig;
    private Vector3 startHeadPosition;
    private float startHeadYaw;
    private float startRigHeight;
    private bool playerStartCaptured;
    private bool checkpointPending;
    private int stageVersion;
    private bool checkpointTreatment;
    private bool checkpointWashing;
    private bool checkpointWaste;
    private bool checkpointSalineContact;
    private int checkpointProgress;
    private TextMeshProUGUI statusText;
    private string feedback = "";
    private readonly SessionRecord session = new SessionRecord();
    private string recordPath;

    [Serializable]
    private class SessionRecord
    {
        public string sessionId = Guid.NewGuid().ToString("N");
        public string startedUtc = DateTime.UtcNow.ToString("O");
        public bool completed;
        public List<MistakeRecord> mistakes = new List<MistakeRecord>();
    }

    [Serializable]
    private class MistakeRecord
    {
        public string timeUtc;
        public string step;
        public bool remeasurement;
        public string selectedTool;
    }

    private void Start()
    {
        tools = FindObjectsByType<EvaluationTool>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        containers = FindObjectsByType<EvaluationWasteContainer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Grabber = FindAnyObjectByType<EvaluationQuestTriggerGrab>();
        playerRig = Grabber == null ? null : Grabber.GetComponent<OVRCameraRig>();
        recordPath = Path.Combine(Application.persistentDataPath, "Evaluation", session.sessionId + ".json");
        CreateStatusDisplay();
    }

    public static bool IsExpectedTool(EvaluationStep step, EvaluationToolKind tool)
    {
        switch (step)
        {
            case EvaluationStep.SurveyMeterInteraction:
            case EvaluationStep.SurveyMeterWarningInteraction:
            case EvaluationStep.ResultVerification:
                return tool == EvaluationToolKind.SurveyMeter;
            case EvaluationStep.SalineInteraction:
                return tool == EvaluationToolKind.Saline;
            case EvaluationStep.ForcepsInteraction:
            case EvaluationStep.DisposeStage:
                return tool == EvaluationToolKind.Forceps;
            default:
                return false;
        }
    }

    public bool IsToolInUse(EvaluationTool tool)
    {
        return !IsRestarting && IsRunning && Grabber != null && Grabber.IsHolding(tool);
    }

    public bool ReportToolGrab(EvaluationTool tool)
    {
        if (tool == null || !CanAcceptGrab)
        {
            return false;
        }
        if (IsExpectedTool(currentStep, tool.Kind))
        {
            if (tool.Kind == EvaluationToolKind.Saline)
            {
                ReportSalineContact();
            }
            return true;
        }

        IsRestarting = true;
        session.mistakes.Add(new MistakeRecord
        {
            timeUtc = DateTime.UtcNow.ToString("O"),
            step = currentStep.ToString(),
            remeasurement = IsTreatmentComplete,
            selectedTool = tool.Kind.ToString()
        });
        feedback = "잘못된 도구 선택 · 현재 단계 재시작";
        RefreshStatusDisplay();
        WriteRecord(false);
        StartCoroutine(RestartCurrentStage());
        return false;
    }

    public bool CanReturn(EvaluationToolKind tool)
    {
        switch (tool)
        {
            case EvaluationToolKind.SurveyMeter: return CanReturnSurveyMeter;
            case EvaluationToolKind.Saline: return CanReturnSaline;
            case EvaluationToolKind.Forceps: return CanReturnForceps;
            default: return false;
        }
    }

    public void ReportToolReturned(EvaluationToolKind tool)
    {
        switch (tool)
        {
            case EvaluationToolKind.SurveyMeter: ReportSurveyMeterReturned(); break;
            case EvaluationToolKind.Saline: ReportSalineReturned(); break;
            case EvaluationToolKind.Forceps: ReportForcepsReturned(); break;
        }
    }

    private IEnumerator CaptureCheckpoint(int version)
    {
        yield return null;
        if (version != stageVersion || IsRestarting)
        {
            yield break;
        }
        checkpointTreatment = IsTreatmentComplete;
        checkpointWashing = IsWashingComplete;
        checkpointWaste = IsWasteDisposed;
        checkpointSalineContact = salineContactReported;
        checkpointProgress = treatmentProgress;
        foreach (EvaluationTool tool in tools)
        {
            tool.CaptureStageState();
        }
        foreach (EvaluationWasteContainer container in containers)
        {
            container.CaptureStageState();
        }
        checkpointPending = false;
    }

    private IEnumerator RestartCurrentStage()
    {
        int restartMistakeCount = MistakeCount;
        CancelInvoke();
        Grabber?.ReleaseAll();
        foreach (EvaluationTool tool in tools)
        {
            tool.SetGrabbable(false);
        }
        EvaluationSoundManager.Instance?.ResetPlayback();
        yield return null;

        IsTreatmentComplete = checkpointTreatment;
        IsWashingComplete = checkpointWashing;
        IsWasteDisposed = checkpointWaste;
        salineContactReported = checkpointSalineContact;
        treatmentProgress = checkpointProgress;
        foreach (EvaluationTool tool in tools)
        {
            tool.ResetForStage();
        }
        foreach (EvaluationWasteContainer container in containers)
        {
            container.ResetForStage();
        }
        RestorePlayerStart();
        Physics.SyncTransforms();
        yield return new WaitForSecondsRealtime(0.25f);

        IsRestarting = false;
        OnStepChanged?.Invoke(currentStep);
        ReplayStageInstruction();
        yield return new WaitForSecondsRealtime(2f);
        if (restartMistakeCount != MistakeCount)
        {
            yield break;
        }
        feedback = "";
        RefreshStatusDisplay();
    }

    private void ReplayStageInstruction()
    {
        switch (currentStep)
        {
            case EvaluationStep.SurveyMeterInteraction:
                if (!IsTreatmentComplete)
                {
                    PlayNarration(sound => sound.NPC1);
                }
                else
                {
                    PlayNarration(sound => sound.NPC10);
                }
                break;
            case EvaluationStep.SurveyMeterWarningInteraction: PlayNarration(sound => sound.NPC2); break;
            case EvaluationStep.SalineInteraction: PlayNarration(sound => sound.NPC3); break;
            case EvaluationStep.ForcepsInteraction: PlayNarration(sound => sound.NPC5); break;
            case EvaluationStep.DisposeStage: PlayNarration(sound => sound.NPC9); break;
            case EvaluationStep.ResultVerification: PlayNarration(sound => sound.NPC11); break;
        }
    }

    private void CapturePlayerStart()
    {
        if (playerRig == null || playerStartCaptured)
        {
            return;
        }
        startHeadPosition = playerRig.centerEyeAnchor.position;
        startHeadYaw = playerRig.centerEyeAnchor.eulerAngles.y;
        startRigHeight = playerRig.transform.position.y;
        playerStartCaptured = true;
    }

    private void RestorePlayerStart()
    {
        if (!playerStartCaptured || playerRig == null)
        {
            return;
        }
        Transform head = playerRig.centerEyeAnchor;
        float yaw = Mathf.DeltaAngle(head.eulerAngles.y, startHeadYaw);
        playerRig.transform.RotateAround(head.position, Vector3.up, yaw);
        Vector3 correction = startHeadPosition - head.position;
        correction.y = startRigHeight - playerRig.transform.position.y;
        playerRig.transform.position += correction;
    }

    private void CreateStatusDisplay()
    {
        if (playerRig == null)
        {
            return;
        }
        GameObject display = new GameObject("Evaluation Status", typeof(RectTransform), typeof(Canvas));
        display.transform.SetParent(playerRig.centerEyeAnchor, false);
        display.transform.localPosition = new Vector3(0f, -0.3f, 1f);
        display.transform.localScale = Vector3.one * 0.001f;
        Canvas canvas = display.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 100;
        ((RectTransform)display.transform).sizeDelta = new Vector2(850f, 130f);
        GameObject label = new GameObject("Mistake Count", typeof(RectTransform));
        label.transform.SetParent(display.transform, false);
        statusText = label.AddComponent<TextMeshProUGUI>();
        if (uIText != null)
        {
            statusText.font = uIText.font;
        }
        statusText.fontSize = 26f;
        statusText.alignment = TextAlignmentOptions.Center;
        statusText.raycastTarget = false;
        statusText.rectTransform.sizeDelta = new Vector2(850f, 130f);
        RefreshStatusDisplay();
    }

    private void RefreshStatusDisplay()
    {
        if (statusText != null)
        {
            statusText.text = $"평가 모드 · 실수 {MistakeCount}회\n{feedback}";
        }
    }

    private void WriteRecord(bool completed)
    {
        session.completed = completed;
        if (string.IsNullOrEmpty(recordPath))
        {
            return;
        }
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(recordPath));
            File.WriteAllText(recordPath, JsonUtility.ToJson(session, true));
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"평가 기록 저장 실패: {exception.Message}", this);
        }
    }

    private void OnApplicationQuit()
    {
        WriteRecord(currentStep == EvaluationStep.End);
    }

}
