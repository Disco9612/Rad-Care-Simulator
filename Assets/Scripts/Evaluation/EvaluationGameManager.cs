using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public enum EvaluationStep { Briefing, SurveyMeterInteraction, SurveyMeterWarningInteraction, SalineInteraction, ForcepsInteraction, DisposeStage, ResultVerification, End }

// Evaluation has no step gate or rewind. Physical actions write an append-only record.
public class EvaluationGameManager : MonoBehaviour
{
    public static EvaluationGameManager Instance;
    public event Action<EvaluationStep> OnStepChanged;
    public CanvasGroup mainUI;
    public TextMeshProUGUI uIText;
    [SerializeField] OVRHand leftHand;
    [SerializeField] bool useControllers;
    [SerializeField] EvaluationStep firstStep=EvaluationStep.SurveyMeterInteraction;
    EvaluationStep step=EvaluationStep.Briefing;
    bool startWasPressed,measurementDone;
    int treatmentProgress;
    readonly Dictionary<EvaluationToolKind,float> grabbedAt=new Dictionary<EvaluationToolKind,float>();
    public FreeEvaluationSession Session {get;private set;}
    public EvaluationStep currentStep=>step;
    public bool IsTreatmentComplete {get;private set;}
    public bool IsWashingComplete {get;private set;}
    public bool IsWasteDisposed {get;private set;}
    public bool IsRestarting=>false;
    public bool IsRunning=>Session!=null&&Session.Record.started&&!Session.Record.finished;
    public bool CanAcceptGrab=>IsRunning;
    public bool CanScan=>IsRunning&&!measurementDone;
    public bool CanDetectContamination=>CanScan&&!IsTreatmentComplete&&!IsWashingComplete;
    public bool CanVerifyTreatment=>CanScan&&(IsTreatmentComplete||IsWashingComplete);
    public bool CanWash=>IsRunning;
    public bool CanUseSaline=>IsRunning;
    public bool CanTreat=>IsRunning&&!IsTreatmentComplete;
    public bool CanDispose=>IsRunning&&IsTreatmentComplete&&!IsWasteDisposed;
    public bool CanReturnSurveyMeter=>IsRunning;
    public bool CanReturnSaline=>IsRunning;
    public bool CanReturnForceps=>IsRunning;
    public int MistakeCount=>0;
    public float ElapsedSeconds=>Session.Record.seconds;
    public EvaluationQuestTriggerGrab Grabber {get;private set;}
    static FreeEvaluationRecord.Procedure P(string key,string label)=>new FreeEvaluationRecord.Procedure(key,label);
    void Awake(){Instance=this;Session=gameObject.AddComponent<FreeEvaluationSession>();Session.Configure("Emergency_Decon",new[]{P("measure_before","초기 오염 측정"),P("meter_return_before","측정기 반환"),P("wash","식염수 세척"),P("saline_return","식염수 반환"),P("treat","집게 처치"),P("dispose","오염물 폐기"),P("forceps_return","집게 반환"),P("measure_after","처치 후 재측정"),P("meter_return_after","재측정 후 측정기 반환")});Session.Finished+=End;}
    void Start(){Grabber=FindAnyObjectByType<EvaluationQuestTriggerGrab>();if(uIText!=null)uIText.text="A 버튼으로 평가 시작\n자유롭게 수행한 뒤 왼쪽 X 버튼을 3초간 눌러 종료";EvaluationSoundManager.Instance?.ResetPlayback();}
    void LateUpdate(){bool pressed=TrainingInputFocus.Available&&(useControllers?OVRInput.Get(OVRInput.Button.One,OVRInput.Controller.RTouch):leftHand!=null&&leftHand.GetFingerIsPinching(OVRHand.HandFinger.Index));if(pressed&&!startWasPressed&&step==EvaluationStep.Briefing)Begin();startWasPressed=pressed;}
    public void Begin(){if(Session.Record.started)return;Session.Record.Begin();step=EvaluationStep.SurveyMeterInteraction;if(mainUI!=null){mainUI.alpha=0;mainUI.blocksRaycasts=false;}OnStepChanged?.Invoke(step);}
    void End(){step=EvaluationStep.End;EvaluationSoundManager.Instance?.ResetPlayback();Grabber?.ReleaseAll();OnStepChanged?.Invoke(step);}
    public bool ReportToolGrab(EvaluationTool tool){if(tool==null||!IsRunning)return false;grabbedAt[tool.Kind]=ElapsedSeconds;Session.Record.Add("grab_"+tool.Kind,ToolName(tool.Kind)+" 집기",0,false,"집기");if(tool.Kind==EvaluationToolKind.SurveyMeter)measurementDone=false;OnStepChanged?.Invoke(step);return true;}
    public bool IsToolInUse(EvaluationTool tool)=>IsRunning&&Grabber!=null&&Grabber.IsHolding(tool);
    public static string ToolName(EvaluationToolKind kind)=>kind==EvaluationToolKind.SurveyMeter?"측정기":kind==EvaluationToolKind.Saline?"식염수":"집게";
    public bool ReportMeasurementCompleted(float duration=0){if(!CanScan)return false;measurementDone=true;bool after=IsTreatmentComplete||IsWashingComplete;Session.Record.Add(after?"measure_after":"measure_before",after?"처치 후 재측정":"초기 오염 측정",duration);EvaluationSoundManager.Instance?.StopSFX();return true;}
    public void ReportSalineContact(){}
    public bool ReportWashingCompleted(float duration=0){if(!IsRunning)return false;IsWashingComplete=true;Session.Record.Add("wash","식염수 세척",duration);return true;}
    public void ReportTreatmentProgress(int milestone){if(!CanTreat||milestone!=treatmentProgress+1)return;treatmentProgress=milestone;}
    public bool ReportTreatmentCompleted(float duration=0){if(!CanTreat||treatmentProgress<3)return false;IsTreatmentComplete=true;Session.Record.Add("treat","집게 처치",duration);return true;}
    public bool ReportWasteDisposed(){if(!CanDispose)return false;IsWasteDisposed=true;Session.Record.Add("dispose","오염물 폐기");EvaluationSoundManager.Instance?.PlaySFXOneShot(EvaluationSoundManager.Instance.trashSound);return true;}
    public bool CanReturn(EvaluationToolKind tool)=>IsRunning;
    public void ReportToolReturned(EvaluationToolKind tool){if(!IsRunning)return;bool after=IsTreatmentComplete||IsWashingComplete;string key=tool==EvaluationToolKind.SurveyMeter?(after?"meter_return_after":"meter_return_before"):tool==EvaluationToolKind.Saline?"saline_return":"forceps_return";Session.Record.Add(key,ToolName(tool)+" 반환",grabbedAt.TryGetValue(tool,out float time)?ElapsedSeconds-time:0);OnStepChanged?.Invoke(step);}
    public bool ReportSurveyMeterReturned(){if(!IsRunning)return false;ReportToolReturned(EvaluationToolKind.SurveyMeter);return true;}
    public bool ReportSalineReturned(){if(!IsRunning)return false;ReportToolReturned(EvaluationToolKind.Saline);return true;}
    public bool ReportForcepsReturned(){if(!IsRunning)return false;ReportToolReturned(EvaluationToolKind.Forceps);return true;}
    public void GameResult(){Session.Finish();}
    // Retained for legacy tooling; runtime evaluation never uses this as a gate.
    public static bool IsExpectedTool(EvaluationStep step,EvaluationToolKind tool)=>true;
    void OnDestroy(){if(Instance==this)Instance=null;}
}
