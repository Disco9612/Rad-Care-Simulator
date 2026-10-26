using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace RadCare.HospitalTreatment.Editor {
 [InitializeOnLoad] public static class HospitalEvaluationSmoke {
  const string Request="Temp/HospitalEvaluationSmoke.request";
  static double started;
  static HospitalEvaluationSmoke(){EditorApplication.update+=Poll;if(SessionState.GetBool("HospitalEvaluationSmoke",false))EditorApplication.update+=Check;}
  static void Poll(){
   if(EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists(Request))return;
   File.Delete(Request);
   if(EditorApplication.isPlaying||UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!="Assets/Scenes/Hospital_Evaluation.unity"){
    File.WriteAllText("Temp/HospitalEvaluationSmoke-error.txt","Hospital_Evaluation edit mode required");return;
   }
   SessionState.SetBool("HospitalEvaluationSmoke",true);EditorApplication.update+=Check;EditorApplication.isPlaying=true;
  }
  static void Check(){
   if(!EditorApplication.isPlaying)return;
   if(started==0)started=EditorApplication.timeSinceStartup;
   if(EditorApplication.timeSinceStartup-started<7)return;
   started=0;EditorApplication.update-=Check;SessionState.SetBool("HospitalEvaluationSmoke",false);
   try{
    var s=UnityEngine.Object.FindAnyObjectByType<HospitalScenario>();var e=s.GetComponent<HospitalEvaluation>();
    if(!s.Ready||e==null||s.targets.Length!=28)throw new Exception("Evaluation scene not ready");
    if(s.GetComponent<HospitalObjectGlow>().enabled||s.GetComponent<HospitalIrrigationGuide>().enabled)throw new Exception("Tutorial cues are active");
    if(s.statusText.text.Contains(s.Procedure.Current.Prompt))throw new Exception("Step prompt exposed in evaluation");
    if(s.GetComponent<HospitalNarration>().source.isPlaying)throw new Exception("Guide narration playing in evaluation");
    if(!Application.CanStreamedLevelBeLoaded("Assets/Scenes/Hospital_Tutorial.unity")||!Application.CanStreamedLevelBeLoaded("Assets/Scenes/Hospital_Evaluation.unity"))throw new Exception("Lobby scenes not loadable");
    var wrong=s.targets.First(t=>t.action==HospitalAction.AskPain);s.Act(wrong);
    if(e.OrdinaryErrors!=1||s.Procedure.Index!=0)throw new Exception("Wrong action was not scored correctly");
    var fresh=s.targets.First(t=>t.action==HospitalAction.ApplyDressing&&t.kind==HospitalInteraction.Carry);
    if(s.input.TryGrab(fresh)||e.OrdinaryErrors!=2)throw new Exception("Wrong tool grab was not scored correctly");
    s.Act(s.targets.First(t=>t.action==HospitalAction.Start));
    if(s.Procedure.Index!=1)throw new Exception("Correct action did not advance");
    e.Save(false);
    if(e.FinalReport==null||e.FinalReport.complete||e.FinalReport.passed||!File.Exists(e.ReportPath))throw new Exception("Incomplete evaluation report invalid");
    File.WriteAllText("Temp/HospitalEvaluationSmoke-result.txt",$"ready=true; tutorialGuidance=false; wrongActions={e.OrdinaryErrors}; stage={s.Procedure.Index}; score={e.FinalReport.score}; report={e.ReportPath}");
    if(File.Exists("Temp/HospitalEvaluationSmoke-error.txt"))File.Delete("Temp/HospitalEvaluationSmoke-error.txt");
   }catch(Exception ex){File.WriteAllText("Temp/HospitalEvaluationSmoke-error.txt",ex.ToString());Debug.LogException(ex);}
   finally{EditorApplication.isPlaying=false;}
  }
 }
}
