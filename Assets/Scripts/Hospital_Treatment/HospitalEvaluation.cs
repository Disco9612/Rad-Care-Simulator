using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RadCare.HospitalTreatment
{
    // Evaluation keeps the tutorial's physical tasks and records performance without step prompts.
    public sealed class HospitalEvaluation : MonoBehaviour
    {
        [Serializable] public sealed class ErrorEntry { public string reason,action,selectedTool; public int stepIndex; public bool critical; public float seconds; }
        [Serializable] public sealed class Report {
            public string scenario="Hospital Treatment Evaluation",caseId="SIM-P001";
            public bool complete,passed;
            public float seconds;
            public int score,ordinaryErrors,criticalErrors,mistakeCount;
            public string grade;
            public HospitalProcedure.Entry[] actions;
            public ErrorEntry[] mistakes;
        }
        public HospitalScenario scenario;
        public int OrdinaryErrors { get; private set; }
        public int CriticalErrors { get; private set; }
        public int MistakeCount => OrdinaryErrors+CriticalErrors;
        public bool IsRestarting { get; private set; }
        string retryFeedback;
        float feedbackUntil;
        public string RetryMessage => IsRestarting||Time.unscaledTime<feedbackUntil?retryFeedback:null;
        public string ReportPath { get; private set; }
        public Report FinalReport { get; private set; }
        public string Status {
            get {
                if(scenario==null||!scenario.Ready)return "평가 준비 중";
                if(FinalReport!=null)return "평가가 종료되었습니다. 종료 메뉴에서 평가 결과를 확인하세요.";
                var p=scenario.Procedure;
                string feedback=IsRestarting||Time.unscaledTime<feedbackUntil?retryFeedback:"차트와 환자 상태를 확인하며 처치를 진행하세요.";
                if(!IsRestarting&&p.Current?.Action==HospitalAction.ReturnSyringe)feedback="세척 완료 · 트리거를 놓고 주사기를 카트의 원래 자리에 놓으세요.";
                return $"병원 상처 처치 평가 · {p.Elapsed/60:00}:{p.Elapsed%60:00}\n완료 항목 {p.Index}/{HospitalProcedure.Steps.Length}\n{feedback}";
            }
        }
        readonly List<ErrorEntry> mistakes=new List<ErrorEntry>();
        public FreeEvaluationSession Session {get;private set;}
        public bool FreeMode=>true;
        readonly Dictionary<HospitalInteractable,float> grabbed=new Dictionary<HospitalInteractable,float>();
        void Awake(){
            if(scenario==null)scenario=GetComponent<HospitalScenario>();
            scenario.Procedure.FreeEvaluation=true;
            Session=gameObject.AddComponent<FreeEvaluationSession>();
            var definitions=new List<FreeEvaluationRecord.Procedure>();
            var actions=new[]{HospitalAction.Chart,HospitalAction.AskPain,HospitalAction.RemoveDressing,HospitalAction.DisposeDressing,HospitalAction.AdjustLight,HospitalAction.RecordBefore,HospitalAction.PlaceDrape,HospitalAction.PlaceReceiver,HospitalAction.SelectSterile,HospitalAction.WashFirst,HospitalAction.RespondPain,HospitalAction.WashSecond,HospitalAction.ReturnSyringe,HospitalAction.ClearWaste,HospitalAction.RecordAfter,HospitalAction.ApplyDressing,HospitalAction.SecureDressing,HospitalAction.ExplainPlan,HospitalAction.SaveChart};
            foreach(var action in actions)definitions.Add(new FreeEvaluationRecord.Procedure(action.ToString(),ActionLabel(action)));
            definitions.Insert(5,new FreeEvaluationRecord.Procedure("measure_before","처치 전 세 부위 측정"));
            definitions.Insert(15,new FreeEvaluationRecord.Procedure("measure_after","처치 후 세 부위 재측정"));
            Session.Configure("Hospital",definitions.ToArray());Session.Finished+=()=>{scenario.Procedure.EndEvaluation();scenario.input.ResetForStepRetry();scenario.InteractionAudio?.StopAll();scenario.Refresh();};
        }
        public static string ActionLabel(HospitalAction a){
            switch(a){
                case HospitalAction.ReviewOrder:return "의료진 지시 확인";case HospitalAction.ProtectChoice:return "보호 드레싱 선택";case HospitalAction.Chart:return "환자 인계 확인";case HospitalAction.AskPain:return "초기 통증 확인";case HospitalAction.RemoveDressing:return "기존 드레싱 제거";case HospitalAction.DisposeDressing:return "기존 드레싱 폐기";case HospitalAction.AdjustLight:return "처치등 조절";case HospitalAction.RecordBefore:return "처치 전 측정 기록";case HospitalAction.PlaceDrape:return "방수포 배치";case HospitalAction.PlaceReceiver:return "체액 받이 배치";case HospitalAction.SelectSterile:return "멸균 세척 도구 선택";case HospitalAction.WashFirst:return "첫 번째 세척";case HospitalAction.RespondPain:return "환자 상태 확인";case HospitalAction.WashSecond:return "두 번째 세척";case HospitalAction.ReturnSyringe:return "주사기 반환";case HospitalAction.ClearWaste:return "오염 소모품 폐기";case HospitalAction.RecordAfter:return "재측정 결과 기록";case HospitalAction.ApplyDressing:return "새 드레싱 적용";case HospitalAction.SecureDressing:return "고정 테이프 부착";case HospitalAction.ExplainPlan:return "후속 계획 설명";case HospitalAction.SaveChart:return "차트 저장";default:return a.ToString();}
        }
        public void RecordAction(HospitalAction action,float duration=0){
            if(action==HospitalAction.Start){Session.Record.Begin();return;}
            Session.Record.Add(action.ToString(),ActionLabel(action),duration);
        }
        public void RecordMeasurement(float duration){bool after=Session.Record.Has("WashFirst");Session.Record.Add(after?"measure_after":"measure_before",after?"처치 후 세 부위 재측정":"처치 전 세 부위 측정",duration);}
        public bool ReportToolGrab(HospitalInteractable tool){if(tool==null||!scenario.CanSelect(tool))return false;grabbed[tool]=Session.Record.seconds;Session.Record.Add("grab_"+tool.itemId,tool.label,0,false,"집기");return true;}
        public float HeldDuration(HospitalInteractable item)=>grabbed.TryGetValue(item,out float time)?Session.Record.seconds-time:0;
        public void RecordMistake(string reason,bool critical,HospitalInteractable selectedTool=null){Session.Record.Add("attempt",reason,0,false,"시도");}
        IEnumerator FinishRestart(){
            yield return new WaitForSecondsRealtime(.25f);
            IsRestarting=false;
            scenario.Refresh();
        }
        public void Save(bool complete){
            if(FinalReport!=null||scenario==null||!scenario.Ready)return;
            var p=scenario.Procedure;
            int timePenalty=Mathf.CeilToInt(Mathf.Max(0,p.Elapsed-270f)/30f)*2;
            int score=Mathf.Clamp((complete?100:Mathf.RoundToInt(60f*p.Index/HospitalProcedure.Steps.Length))-OrdinaryErrors*3-CriticalErrors*12-timePenalty,0,100);
            var report=new Report{complete=complete,seconds=p.Elapsed,score=score,ordinaryErrors=OrdinaryErrors,criticalErrors=CriticalErrors,mistakeCount=MistakeCount,
                passed=complete&&score>=80&&CriticalErrors==0,grade=complete?(score>=90&&CriticalErrors==0?"A":score>=80&&CriticalErrors==0?"B":"재평가"):"미완료",
                actions=p.Entries.ToArray(),mistakes=mistakes.ToArray()};
            try{
                string folder=Path.Combine(Application.persistentDataPath,"HospitalTreatmentEvaluations");Directory.CreateDirectory(folder);
                ReportPath=Path.Combine(folder,"SIM-P001-EVAL-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+".json");
                File.WriteAllText(ReportPath,JsonUtility.ToJson(report,true));FinalReport=report;
            }catch(Exception e){Debug.LogWarning("Hospital evaluation report save failed: "+e.Message,this);}
        }
        void OnDestroy(){}
    }
}
