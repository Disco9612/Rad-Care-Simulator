using System;
using TMPro;
using UnityEngine;

public enum ScenarioStep
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

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public event Action<ScenarioStep> OnStepChanged;

    public CanvasGroup mainUI;
    public TextMeshProUGUI uIText;
    [SerializeField] private OVRHand leftHand;
    [SerializeField] private bool useControllers;
    [SerializeField] private ScenarioStep firstStep = ScenarioStep.SalineInteraction;

    private ScenarioStep _currentStep = ScenarioStep.Briefing;
    private bool wasStartPressed;
    private bool salineContactReported;
    private int treatmentProgress;

    public ScenarioStep currentStep => _currentStep;
    public bool IsTreatmentComplete { get; private set; }
    public bool IsWashingComplete { get; private set; }
    public bool IsWasteDisposed { get; private set; }
    public bool CanScan => currentStep == ScenarioStep.SurveyMeterInteraction
        || currentStep == ScenarioStep.SurveyMeterWarningInteraction;
    public bool CanDetectContamination => CanScan && !IsTreatmentComplete;
    public bool CanVerifyTreatment => currentStep == ScenarioStep.SurveyMeterInteraction && IsTreatmentComplete;
    public bool CanWash => currentStep == ScenarioStep.SalineInteraction && !IsWashingComplete;
    public bool CanUseSaline => currentStep == ScenarioStep.SalineInteraction;
    public bool CanTreat => currentStep == ScenarioStep.ForcepsInteraction;
    public bool CanDispose => currentStep == ScenarioStep.DisposeStage && !IsWasteDisposed;
    public bool CanReturnSurveyMeter => currentStep == ScenarioStep.SurveyMeterWarningInteraction
        || currentStep == ScenarioStep.ResultVerification;
    public bool CanReturnSaline => CanUseSaline && IsWashingComplete;
    public bool CanReturnForceps => currentStep == ScenarioStep.DisposeStage && IsWasteDisposed;

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
        if (!startPressed || currentStep != ScenarioStep.Briefing)
        {
            return;
        }
        if (firstStep != ScenarioStep.SurveyMeterInteraction && firstStep != ScenarioStep.SalineInteraction)
        {
            Debug.LogError("The scenario must start with measurement or saline treatment.", this);
            return;
        }
        if (mainUI != null)
        {
            mainUI.alpha = 0f;
            mainUI.blocksRaycasts = false;
        }
        PlayNarration(sound => sound.NPC1);
        ChangeStep(firstStep);
        if (uIText != null)
        {
            uIText.text = "제염 미션 완료. 결과 리포트를 출력합니다";
        }
    }

    public bool ReportMeasurementCompleted()
    {
        if (currentStep != ScenarioStep.SurveyMeterInteraction)
        {
            return false;
        }
        ChangeStep(IsTreatmentComplete ? ScenarioStep.ResultVerification : ScenarioStep.SurveyMeterWarningInteraction);
        return true;
    }

    public bool ReportSurveyMeterReturned()
    {
        if (!CanReturnSurveyMeter)
        {
            return false;
        }
        ChangeStep(currentStep == ScenarioStep.ResultVerification ? ScenarioStep.End : ScenarioStep.SalineInteraction);
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
        SoundManager.Instance?.StopSFX();
        PlayNarration(sound => sound.NPC5);
        return true;
    }

    public bool ReportSalineReturned()
    {
        if (!CanReturnSaline)
        {
            return false;
        }
        ChangeStep(ScenarioStep.ForcepsInteraction);
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
        ChangeStep(ScenarioStep.DisposeStage);
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
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFXOneShot(SoundManager.Instance.trashSound);
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
        ChangeStep(ScenarioStep.SurveyMeterInteraction);
        return true;
    }

    private void ChangeStep(ScenarioStep nextStep)
    {
        if (_currentStep == nextStep)
        {
            return;
        }
        _currentStep = nextStep;
        SoundManager.Instance?.StopSFX();
        OnStepChanged?.Invoke(nextStep);
        switch (nextStep)
        {
            case ScenarioStep.SurveyMeterWarningInteraction:
                PlayNarration(sound => sound.NPC2);
                break;
            case ScenarioStep.SalineInteraction:
                PlayNarration(sound => sound.NPC3);
                break;
            case ScenarioStep.ResultVerification:
                PlayNarration(sound => sound.NPC11);
                break;
            case ScenarioStep.End:
                PlayNarration(sound => sound.NPC12);
                GameResult();
                break;
        }
        Debug.Log($"단계 변경됨: {nextStep}", this);
    }

    private void PlayDisposalInstruction()
    {
        if (CanDispose)
        {
            PlayNarration(sound => sound.NPC9);
        }
    }

    private static void PlayNarration(Func<SoundManager, AudioClip> selectClip)
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayNPC(selectClip(SoundManager.Instance));
        }
    }

    public void GameResult()
    {
        if (currentStep == ScenarioStep.End && mainUI != null)
        {
            mainUI.alpha = 1f;
            mainUI.blocksRaycasts = true;
        }
    }
}
