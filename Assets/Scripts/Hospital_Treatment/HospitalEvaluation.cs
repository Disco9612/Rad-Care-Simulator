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
                if(FinalReport!=null)return $"평가 완료 · {FinalReport.score}점 ({FinalReport.grade})\n실수 {MistakeCount}회 · 일반 오류 {OrdinaryErrors} · 중대 오류 {CriticalErrors}\n왼쪽 메뉴 버튼 길게 누르기: 로비";
                var p=scenario.Procedure;
                string feedback=IsRestarting||Time.unscaledTime<feedbackUntil?retryFeedback:"차트와 환자 상태를 확인하며 처치를 진행하세요.";
                if(!IsRestarting&&p.Current?.Action==HospitalAction.ReturnSyringe)feedback="세척 완료 · 트리거를 놓고 주사기를 카트의 원래 자리에 놓으세요.";
                return $"병원 상처 처치 평가 · {p.Elapsed/60:00}:{p.Elapsed%60:00}\n완료 항목 {p.Index}/{HospitalProcedure.Steps.Length} · 실수 {MistakeCount}회 · 일반 {OrdinaryErrors} · 중대 {CriticalErrors}\n{feedback}";
            }
        }
        readonly List<ErrorEntry> mistakes=new List<ErrorEntry>();
        void Awake(){if(scenario==null)scenario=GetComponent<HospitalScenario>();}
        void Update(){if(scenario!=null&&scenario.Ready&&scenario.Procedure.Complete&&FinalReport==null)Save(true);}
        // Shared by every Carry/Irrigator grab, including ray and near-hand selection.
        public bool ReportToolGrab(HospitalInteractable tool){
            if(tool==null||!tool.IsMovable||!scenario.CanSelect(tool))return false;
            if(scenario.CanUse(tool))return true;
            RecordMistake("순서에 맞지 않는 도구 선택",false,tool);
            return false;
        }
        public void RecordMistake(string reason,bool critical,HospitalInteractable selectedTool=null){
            if(scenario==null||!scenario.Ready||!scenario.Procedure.Started||scenario.Procedure.Complete||IsRestarting||FinalReport!=null)return;
            float seconds=scenario.Procedure.Elapsed;
            IsRestarting=true;
            mistakes.Add(new ErrorEntry{reason=reason,critical=critical,seconds=seconds,
                stepIndex=scenario.Procedure.Index,action=scenario.Procedure.Current.Action.ToString(),selectedTool=selectedTool!=null?selectedTool.name:null});
            if(critical)CriticalErrors++;else OrdinaryErrors++;
            retryFeedback=$"실수 +1 · {reason}"+(selectedTool!=null?$" ({selectedTool.label})":"")+"\n현재 단계부터 다시 진행하세요.";
            feedbackUntil=Time.unscaledTime+2.25f;
            scenario.RestartCurrentStep();
            if(selectedTool!=null&&selectedTool.IsMovable&&!selectedTool.retired)selectedTool.ReturnHome();
            if(selectedTool!=null)scenario.GetComponent<HospitalNarration>()?.PlayEquipmentFeedback();
            Physics.SyncTransforms();
            StartCoroutine(FinishRestart());
        }
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
        void OnDestroy(){if(FinalReport==null&&scenario!=null&&scenario.Ready&&scenario.Procedure.Started)Save(scenario.Procedure.Complete);}
    }
}
