using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RadCare.HospitalTreatment
{
    // Evaluation keeps the tutorial's physical tasks and records performance without step prompts.
    public sealed class HospitalEvaluation : MonoBehaviour
    {
        [Serializable] public sealed class ErrorEntry { public string reason; public bool critical; public float seconds; }
        [Serializable] public sealed class Report {
            public string scenario="Hospital Treatment Evaluation",caseId="SIM-P001";
            public bool complete,passed;
            public float seconds;
            public int score,ordinaryErrors,criticalErrors;
            public string grade;
            public HospitalProcedure.Entry[] actions;
            public ErrorEntry[] mistakes;
        }
        public HospitalScenario scenario;
        public int OrdinaryErrors { get; private set; }
        public int CriticalErrors { get; private set; }
        public string ReportPath { get; private set; }
        public Report FinalReport { get; private set; }
        public string Status {
            get {
                if(scenario==null||!scenario.Ready)return "평가 준비 중";
                if(FinalReport!=null)return $"평가 완료 · {FinalReport.score}점 ({FinalReport.grade})\n일반 오류 {OrdinaryErrors} · 중대 오류 {CriticalErrors}\n왼쪽 메뉴 버튼 길게 누르기: 로비";
                var p=scenario.Procedure;
                return $"병원 상처 처치 평가 · {p.Elapsed/60:00}:{p.Elapsed%60:00}\n완료 항목 {p.Index}/{HospitalProcedure.Steps.Length} · 일반 오류 {OrdinaryErrors} · 중대 오류 {CriticalErrors}\n차트와 환자 상태를 확인하며 처치를 진행하세요.";
            }
        }
        readonly List<ErrorEntry> mistakes=new List<ErrorEntry>();
        readonly Dictionary<string,float> lastMistake=new Dictionary<string,float>();
        void Awake(){if(scenario==null)scenario=GetComponent<HospitalScenario>();}
        void Update(){if(scenario!=null&&scenario.Ready&&scenario.Procedure.Complete&&FinalReport==null)Save(true);}
        public void RecordMistake(string reason,bool critical){
            if(scenario==null||!scenario.Ready||scenario.Procedure.Complete)return;
            var key=reason+":"+scenario.Procedure.Index;
            float seconds=scenario.Procedure.Elapsed;
            if(lastMistake.TryGetValue(key,out float previous)&&seconds-previous<1.5f)return;
            lastMistake[key]=seconds;
            mistakes.Add(new ErrorEntry{reason=reason,critical=critical,seconds=seconds});
            if(critical)CriticalErrors++;else OrdinaryErrors++;
        }
        public void Save(bool complete){
            if(FinalReport!=null||scenario==null||!scenario.Ready)return;
            var p=scenario.Procedure;
            int timePenalty=Mathf.CeilToInt(Mathf.Max(0,p.Elapsed-270f)/30f)*2;
            int score=Mathf.Clamp((complete?100:Mathf.RoundToInt(60f*p.Index/HospitalProcedure.Steps.Length))-OrdinaryErrors*3-CriticalErrors*12-timePenalty,0,100);
            var report=new Report{complete=complete,seconds=p.Elapsed,score=score,ordinaryErrors=OrdinaryErrors,criticalErrors=CriticalErrors,
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
