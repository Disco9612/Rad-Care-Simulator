using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RadCare.HospitalTreatment.Editor {
 [InitializeOnLoad] public static class HospitalEvaluationSmoke {
  const string Request="Temp/HospitalEvaluationSmoke.request";
  const string Result="Temp/HospitalEvaluationSmoke-result.txt";
  const string Error="Temp/HospitalEvaluationSmoke-error.txt";
  static double started,nextCheck;
  static IEnumerator checks;
  static HospitalEvaluationSmoke(){EditorApplication.update+=Poll;if(SessionState.GetBool("HospitalEvaluationSmoke",false))EditorApplication.update+=Check;}
  static void Poll(){
   if(EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists(Request))return;
   string mode=File.ReadAllText(Request).Trim();
   File.Delete(Request);
   if(EditorApplication.isPlayingOrWillChangePlaymode||UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!="Assets/Scenes/Hospital_Evaluation.unity"){
    File.WriteAllText(Error,"Hospital_Evaluation edit mode required");return;
   }
   if(File.Exists(Result))File.Delete(Result);
   if(File.Exists(Error))File.Delete(Error);
   started=0;nextCheck=0;checks=null;
   SessionState.SetBool("HospitalEvaluationAllTools",mode=="all-tools");
   SessionState.SetBool("HospitalEvaluationInputRoute",mode=="input-route");
   SessionState.SetBool("HospitalEvaluationSmoke",true);EditorApplication.update-=Check;EditorApplication.update+=Check;EditorApplication.isPaused=false;EditorApplication.isPlaying=true;
  }
  static void Check(){
   if(!EditorApplication.isPlaying)return;
   if(started==0)started=EditorApplication.timeSinceStartup;
   if(EditorApplication.timeSinceStartup-started<7||EditorApplication.timeSinceStartup<nextCheck)return;
   try{
    if(checks==null){EditorApplication.isPaused=false;checks=SessionState.GetBool("HospitalEvaluationInputRoute",false)?RunInputRouteChecks():SessionState.GetBool("HospitalEvaluationAllTools",false)?RunAllToolChecks():RunChecks();}
    if(checks.MoveNext()){nextCheck=EditorApplication.timeSinceStartup+(checks.Current is float delay?delay:0);return;}
   }catch(Exception ex){File.WriteAllText(Error,ex.ToString());Debug.LogException(ex);}
   started=0;checks=null;EditorApplication.update-=Check;SessionState.SetBool("HospitalEvaluationSmoke",false);EditorApplication.isPlaying=false;
  }
  static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
  static HospitalInteractable Target(HospitalScenario s,HospitalAction action,HospitalInteraction kind=HospitalInteraction.Action)=>s.targets.First(t=>t.action==action&&t.kind==kind);
  static void Carry(HospitalScenario s,HospitalAction action){
   var item=Target(s,action,HospitalInteraction.Carry);var socket=Target(s,action,HospitalInteraction.Socket);
   Require(s.input.TryGrab(item),"Correct item could not be grabbed: "+action);
   item.movable.position=socket.transform.position;
   Require(s.input.TryCompletePlacement(.3f),"Correct placement failed: "+action);
  }
  static IEnumerator RunInputRouteChecks(){
   var s=UnityEngine.Object.FindAnyObjectByType<HospitalScenario>();var e=s.GetComponent<HospitalEvaluation>();
   Require(s.Ready&&e!=null,"Evaluation not ready");
   s.input.enabled=false;s.enabled=false;s.GetComponent<HospitalNarration>().enabled=false;
   while(s.Procedure.Current.Action!=HospitalAction.PlaceDrape)Require(s.Procedure.Try(s.Procedure.Current.Action),"Could not prepare drape step");
   s.Refresh();Physics.SyncTransforms();
   var syringe=Target(s,HospitalAction.SelectSterile,HospitalInteraction.Irrigator);
   var drape=Target(s,HospitalAction.PlaceDrape,HospitalInteraction.Carry);
   var narration=s.GetComponent<HospitalNarration>();
   var feedbackClip=Resources.Load<AudioClip>("HospitalTreatment/equipment_not_for_step");
   Require(feedbackClip!=null&&feedbackClip.length>1&&feedbackClip.length<8,"Equipment feedback clip missing or invalid");
   var empty=new Vector3(1000,1000,1000);var emptyRay=new Ray(empty,Vector3.up);
   int index=s.Procedure.Index;
   for(int mode=0;mode<6;mode++){
    bool left=mode%2==1;bool grip=mode==2||mode==3;
    s.input.ProcessHandGrab(emptyRay,empty,false,false,left);
    if(mode==4)Require(!s.input.ProcessHandGrab(emptyRay,empty,true,false,left),"Empty press incorrectly grabbed an item");
    Physics.SyncTransforms();var point=syringe.grabCollider.bounds.center;
    var ray=mode==5?new Ray(point+Vector3.up*.3f,Vector3.down):emptyRay;
    var palm=mode==5?empty:point;
    Require(s.input.ProcessHandGrab(ray,palm,!grip,grip,left),"Wrong syringe attempt was not resolved through input route: "+mode);
    Require(e.MistakeCount==mode+1&&e.IsRestarting&&s.input.Held==null&&s.Procedure.Index==index,"Wrong syringe input did not fail/restart drape step: "+mode);
    Require(e.RetryMessage.Contains("실수 +1")&&e.RetryMessage.Contains(syringe.label),"Wrong-tool feedback missing");
    Require(narration.EquipmentFeedbackPlaying,"Wrong-tool voice did not play");
    int voiceCount=narration.EquipmentFeedbackCount;
    narration.PlayEquipmentFeedback();narration.RefreshNarration();
    Require(narration.EquipmentFeedbackPlaying&&narration.EquipmentFeedbackCount==voiceCount,"Voice was interrupted or duplicated");
    s.input.ProcessHandGrab(ray,palm,!grip,grip,left);
    Require(e.MistakeCount==mode+1,"Same held button counted again");
    yield return .4f;
    double retryDeadline=EditorApplication.timeSinceStartup+5;
    while(e.IsRestarting&&EditorApplication.timeSinceStartup<retryDeadline)yield return .1f;
    Require(!e.IsRestarting,"Restart did not finish while feedback voice played");
   }
   // Correct-tool holding is outside wrong-tool attempt handling.
   s.input.ProcessHandGrab(emptyRay,empty,false,false,false);
   Require(s.input.ProcessHandGrab(emptyRay,drape.grabCollider.bounds.center,true,false,false)&&s.input.Held==drape,"Correct drape grab failed");
   s.input.ProcessHandGrab(emptyRay,empty,false,false,true);
   Require(!s.input.ProcessHandGrab(emptyRay,syringe.grabCollider.bounds.center,true,false,true)&&s.input.Held==drape&&e.MistakeCount==6,"Already-held correct tool was disturbed");
   drape.movable.position=Target(s,HospitalAction.PlaceDrape,HospitalInteraction.Socket).transform.position;
   Require(s.input.TryCompletePlacement(.3f)&&s.Procedure.Current.Action==HospitalAction.PlaceReceiver,"Cannot finish correct drape interaction after retries");
   e.Save(false);
   File.WriteAllText(Result,"version=equipment-voice-v5; drapeWrongSyringe=true; bothHands=true; triggerAndGrip=true; heldPressApproach=true; raySelection=true; mistakes=6; holdingCorrectToolUnaffected=true; correctRecovery=true; equipmentVoice=true; voiceNotInterruptedOrDuplicated=true");
  }
  // Independent scenario contract: the physical item required by each interaction.
  static string ExpectedTool(HospitalAction action){
   switch(action){
    case HospitalAction.RemoveDressing:case HospitalAction.DisposeDressing:return "old";
    case HospitalAction.PlaceDrape:return "drape";
    case HospitalAction.PlaceReceiver:return "receiver";
    case HospitalAction.SelectSterile:case HospitalAction.WashFirst:case HospitalAction.WashSecond:case HospitalAction.ReturnSyringe:return "syringe";
    case HospitalAction.ClearWaste:return "used-consumables";
    case HospitalAction.ApplyDressing:return "fresh";
    case HospitalAction.SecureDressing:return "fixation_tape";
    default:return null;
   }
  }
  static IEnumerator RunAllToolChecks(){
   var s=UnityEngine.Object.FindAnyObjectByType<HospitalScenario>();var e=s.GetComponent<HospitalEvaluation>();
   Require(s.Ready&&e!=null,"Evaluation scene not ready");
   s.input.enabled=false;s.enabled=false;s.GetComponent<HospitalNarration>().enabled=false;
   var tools=s.targets.Where(t=>t.IsMovable).ToArray();
   var expectedIds=new[]{"old","drape","receiver","syringe","used-consumables","fresh","fixation_tape"};
   Require(tools.Select(t=>t.itemId).OrderBy(x=>x).SequenceEqual(expectedIds.OrderBy(x=>x)),"Scene tool inventory differs from the seven expected tools");
   Require((int)HospitalAction.SaveChart==23,"Serialized scene action IDs changed");
   var narration=s.GetComponent<HospitalNarration>();
   for(int i=0;i<HospitalProcedure.Steps.Length;i++)Require(narration.cues.Any(c=>c.key==HospitalProcedure.NarrationKey(i)),"Step audio binding shifted: "+i);
   var covered=new System.Collections.Generic.HashSet<string>();int attempts=0,steps=0;
   s.Act(Target(s,HospitalAction.Start));
   while(!s.Procedure.Complete){
    int index=s.Procedure.Index;var action=s.Procedure.Current.Action;
    if(action==HospitalAction.ReturnSyringe){
     var syringe=tools.Single(t=>t.itemId=="syringe");var waste=tools.Single(t=>t.itemId=="used-consumables");
     Require(s.input.Held==syringe&&syringe.IsHeld,"Finishing washing dropped the syringe");
     Require(!s.CanUse(waste)&&!waste.GetComponentInChildren<Canvas>(true).gameObject.activeInHierarchy,"Waste collection exposed before syringe return");
     Require(e.Status.Contains("주사기를 카트"),"Return instruction missing from evaluation");
     s.GetComponent<HospitalChartScreen>().RefreshScreen();
     Require(s.GetComponent<HospitalChartScreen>().StateLabel=="주사기 반환 대기","Monitor skips return stage");
     int mistakes=e.MistakeCount;
     syringe.movable.position=syringe.homePosition+Vector3.up*.2f;
     Require(!s.input.TryCompletePlacement(.3f),"Syringe returned before reaching its original place");
     syringe.movable.position=syringe.homePosition;
     Require(!s.input.TryCompletePlacement(.3f,true),"Syringe returned while treatment trigger was held");
     Require(!s.input.TryCompletePlacement(.1f),"Syringe returned without dwell");
     syringe.movable.position=syringe.homePosition+Vector3.up*.2f;
     Require(!s.input.TryCompletePlacement(.3f)&&s.Procedure.Index==index&&e.MistakeCount==mistakes,"Position adjustment skipped return step or counted an error");
    }
    s.input.ResetForStepRetry();s.Irrigate(null,false);s.Refresh();
    var completed=tools.Where(t=>t.retired).Select(t=>new{tool=t,position=t.movable.position,rotation=t.movable.rotation,active=t.movable.gameObject.activeSelf}).ToArray();
    foreach(var tool in tools.Where(t=>!t.retired&&t.itemId!=ExpectedTool(action))){
     string context=action+" / "+tool.itemId;
     Require(s.CanSelect(tool)&&tool.grabCollider!=null&&tool.grabCollider.enabled&&tool.grabCollider.gameObject.activeInHierarchy,"Wrong tool has no active grab surface: "+context);
     Physics.SyncTransforms();
     Require(s.input.FindNearTarget(tool.grabCollider.bounds.center)==tool,"Near-hand selection misses tool: "+context);
     // Verify that rejection restores both position and rotation, even if the attempted grab moved it.
     tool.movable.position+=Vector3.up*.12f;tool.movable.rotation=Quaternion.Euler(13,27,9)*tool.homeRotation;
     int count=e.MistakeCount;int entries=s.Procedure.Entries.Count;
     Physics.SyncTransforms();
     var empty=new Vector3(1000,1000,1000);var ray=new Ray(empty,Vector3.up);bool left=attempts%2==1;
     s.input.ProcessHandGrab(ray,empty,false,false,left);
     Require(s.input.ProcessHandGrab(ray,tool.grabCollider.bounds.center,true,false,left),"Wrong-tool input did not reach evaluation: "+context);
     Require(e.MistakeCount==count+1&&e.IsRestarting,"Wrong tool did not count/restart: "+context);
     Require(s.input.Held==null&&!tool.IsHeld&&Vector3.Distance(tool.movable.position,tool.homePosition)<.001f&&Quaternion.Angle(tool.movable.rotation,tool.homeRotation)<.1f,"Tool not restored: "+context);
     Require(s.Procedure.Index==index&&s.Procedure.Entries.Count==entries,"Retry changed the current/completed steps: "+context);
     foreach(var prior in completed)Require(prior.tool.retired&&prior.tool.movable.gameObject.activeSelf==prior.active&&Vector3.Distance(prior.tool.movable.position,prior.position)<.001f&&Quaternion.Angle(prior.tool.movable.rotation,prior.rotation)<.1f,"Retry changed a completed placement: "+context);
     Require(!s.input.TryGrab(tool)&&e.MistakeCount==count+1,"Duplicate input was counted: "+context);
     covered.Add(tool.itemId);attempts++;
     File.WriteAllText("Temp/HospitalEvaluationAllTools-progress.txt",$"attempts={attempts}; step={index}; checked={context}");
     yield return .35f;
     Require(!e.IsRestarting,"Retry did not resume: "+context);
    }
    int beforeCorrect=e.MistakeCount;string expected=ExpectedTool(action);
    if(expected!=null){
     var tool=tools.Single(t=>t.itemId==expected);
     Require(s.input.TryGrab(tool),"Correct tool rejected: "+action+" / "+expected);
     if(s.Procedure.IsWashing&&action!=HospitalAction.SelectSterile){
      tool.movable.rotation=Quaternion.FromToRotation(tool.tip.forward,-s.IrrigationNormal)*tool.movable.rotation;
      tool.movable.position+=s.IrrigationPoint-tool.tip.position;s.Irrigate(tool,true,10);
     }else if(action==HospitalAction.ReturnSyringe){
      tool.movable.position=tool.homePosition;
      Require(!s.input.TryCompletePlacement(.1f)&&s.input.TryCompletePlacement(.16f),"Physical return did not advance after dwell");
      Require(s.input.Held==null&&s.Procedure.Current.Action==HospitalAction.ClearWaste&&s.CanUse(tools.Single(t=>t.itemId=="used-consumables")),"Waste collection did not unlock after return");
      s.ReportToolReturned(tool);
      Require(s.Procedure.Entries.Count(x=>x.action=="ReturnSyringe")==1,"Return completion was skipped or duplicated");
     }else if(s.Procedure.Index==index){
      tool.movable.position=Target(s,action,HospitalInteraction.Socket).transform.position;
      Require(s.input.TryCompletePlacement(.3f),"Correct placement failed: "+action);
     }
    }else{
     s.Act(Target(s,action));
     if(HospitalScenario.IsMeasurement(action)){s.AdvanceAssistantMeasurement(6);s.Act(Target(s,action));}
    }
    Require(e.MistakeCount==beforeCorrect&&s.Procedure.Index>index,"Correct interaction failed after retry: "+action);
    steps++;
   }
   Require(covered.Count==tools.Length&&attempts==e.MistakeCount,"Not all tools were validated");
   e.Save(true);
   var saved=JsonUtility.FromJson<HospitalEvaluation.Report>(File.ReadAllText(e.ReportPath));
   Require(saved.complete&&saved.mistakeCount==attempts&&saved.mistakes.Length==attempts,"Final report lost mistakes");
   foreach(var tool in tools)Require(!s.input.TryGrab(tool)&&e.MistakeCount==attempts,"Completed evaluation accepts new mistakes");
   File.WriteAllText(Result,$"version=syringe-return-v3; tools={tools.Length}; interactions={steps}; wrongToolAttempts={attempts}; restoredPose=true; restartedSameStep=true; priorProgressPreserved=true; returnBeforeWaste=true; triggerReleaseRequired=true; returnDwell=true; audioBindings=true; correctInteractions=true; complete=true; report={e.ReportPath}");
  }
  static IEnumerator RunChecks(){
   var s=UnityEngine.Object.FindAnyObjectByType<HospitalScenario>();var e=s.GetComponent<HospitalEvaluation>();
   Require(s.Ready&&e!=null&&s.targets.Length==28,"Evaluation scene not ready");
   Require(!s.GetComponent<HospitalObjectGlow>().enabled&&!s.GetComponent<HospitalIrrigationGuide>().enabled,"Tutorial cues are active");
   Require(!s.statusText.text.Contains(s.Procedure.Current.Prompt),"Step prompt exposed in evaluation");
   Require(!s.GetComponent<HospitalNarration>().source.isPlaying,"Guide narration playing in evaluation");
   Require(Application.CanStreamedLevelBeLoaded("Assets/Scenes/Hospital_Tutorial.unity")&&Application.CanStreamedLevelBeLoaded("Assets/Scenes/Hospital_Evaluation.unity"),"Lobby scenes not loadable");
   // Drive the same interaction entry points deterministically without requiring a connected headset.
   s.input.enabled=false;s.enabled=false;s.GetComponent<HospitalNarration>().enabled=false;
   var syringe=Target(s,HospitalAction.SelectSterile,HospitalInteraction.Irrigator);
   Require(!s.input.TryGrab(syringe)&&e.MistakeCount==0,"Pre-start grab was scored");
   s.Act(Target(s,HospitalAction.Start));
   Require(s.Procedure.Current.Action==HospitalAction.Chart,"Correct start did not advance");
   Require(!s.CanSelect(Target(s,HospitalAction.AskPain)),"Out-of-step confirmation is selectable");
   Physics.SyncTransforms();
   Require(s.CanSelect(syringe)&&syringe.grabCollider.enabled,"Wrong tools cannot be attempted in evaluation");
   Require(s.input.FindNearTarget(syringe.grabCollider.bounds.center)==syringe,"Wrong tool near-grab does not reach evaluation");
   Require(!s.input.TryGrab(syringe),"Incorrect syringe grab was accepted");
   Require(e.MistakeCount==1&&e.IsRestarting&&s.Procedure.Index==1&&s.Procedure.Entries.Count==1,"Wrong grab did not restart Chart with one mistake");
   Require(s.input.Held==null&&!syringe.IsHeld&&Vector3.Distance(syringe.movable.position,syringe.homePosition)<.001f,"Wrong tool was not returned");
   Require(e.Status.Contains("실수 1회")&&e.Status.Contains("현재 단계부터 다시"),"Retry feedback missing");
   Require(!s.input.TryGrab(syringe)&&e.MistakeCount==1,"Same retry counted twice");
   s.Act(Target(s,HospitalAction.Chart));
   Require(s.Procedure.Index==1,"Input advanced during restart");
   yield return .4f;
   Require(!e.IsRestarting,"Restart did not unlock");
   Require(!s.input.TryGrab(syringe)&&e.MistakeCount==2,"A second attempt was lost to time-based deduplication");
   yield return .4f;
   s.Act(Target(s,HospitalAction.Chart));s.Act(Target(s,HospitalAction.AskPain));
   var old=Target(s,HospitalAction.RemoveDressing,HospitalInteraction.Carry);
   Require(s.input.TryGrab(old),"Correct dressing grab failed after retry");
   old.movable.position=Target(s,HospitalAction.DisposeDressing,HospitalInteraction.Socket).transform.position;
   Require(s.input.TryCompletePlacement(.3f),"Old dressing disposal failed");
   s.Act(Target(s,HospitalAction.AdjustLight));
   var measurementTargets=s.targets.Where(t=>HospitalScenario.IsMeasurement(t.action)).ToArray();
   var request=Target(s,HospitalAction.MeasureWound);
   Require(measurementTargets.Length==3&&measurementTargets.Count(s.CanSelect)==1&&s.CanSelect(request),"Overlapping measurement targets remain selectable");
   var rect=request.GetComponentInChildren<Canvas>(true).GetComponent<RectTransform>();
   var corners=new Vector3[4];rect.GetWorldCorners(corners);
   var center=(corners[0]+corners[2])*.5f;var normal=Vector3.Cross(corners[1]-corners[0],corners[2]-corners[0]).normalized;
   Physics.SyncTransforms();
   Require(s.input.FindRayTarget(new Ray(center-normal*.3f,normal),out _)==request,"Assistant UI ray selects the wrong target");
   s.Act(request);s.AdvanceAssistantMeasurement(3);
   Require(s.AssistantSeconds>0,"Assistant request did not start");
   Require(!s.input.TryGrab(syringe)&&e.MistakeCount==3,"Wrong grab during measurement was not counted");
   Require(s.AssistantSeconds==0&&!s.AssistantReportReady&&s.Procedure.Current.Action==HospitalAction.MeasureWound,"Measurement did not restart");
   Require(old.retired&&!old.movable.gameObject.activeSelf,"Retry undid earlier disposal");
   yield return .4f;
   s.Act(request);s.AdvanceAssistantMeasurement(6);s.Act(request);
   Require(s.Procedure.Current.Action==HospitalAction.RecordBefore,"Measurement cannot finish after retry");
   s.Act(Target(s,HospitalAction.RecordBefore));
   Carry(s,HospitalAction.PlaceDrape);Carry(s,HospitalAction.PlaceReceiver);
   Require(s.input.TryGrab(syringe)&&s.Procedure.IsWashing,"Correct syringe grab was rejected");
   int beforeWashing=e.MistakeCount;
   Require(s.input.UpdateHeldUse(true,true,3),"Syringe trigger was not consumed");
   Require(s.input.Held==syringe&&s.Procedure.WashSeconds==0&&e.MistakeCount==beforeWashing,"Grab trigger started washing or returned syringe");
   s.input.UpdateHeldUse(false,false);
   syringe.movable.rotation=Quaternion.FromToRotation(syringe.tip.forward,-s.IrrigationNormal)*syringe.movable.rotation;
   syringe.movable.position+=s.IrrigationPoint+s.IrrigationNormal*.08f-syringe.tip.position;
   Require(s.input.UpdateHeldUse(true,true,3),"Aiming press leaked into UI selection");
   Require(s.input.Held==syringe&&syringe.IsHeld&&!e.IsRestarting&&e.MistakeCount==beforeWashing&&s.Procedure.WashSeconds==0,"Off-contact treatment attempt returned syringe or counted a mistake");
   // Move into contact while keeping the trigger held: washing must start without another grab.
   syringe.movable.position+=s.IrrigationPoint-syringe.tip.position;
   s.input.UpdateHeldUse(true,false,3);
   Require(s.Procedure.WashSeconds==3,"Valid washing did not progress");
   syringe.movable.position+=s.IrrigationNormal*.08f;
   s.input.UpdateHeldUse(false,false);s.input.UpdateHeldUse(true,true,2);
   Require(s.input.Held==syringe&&s.Procedure.WashSeconds==3&&e.MistakeCount==beforeWashing,"Losing contact reset progress or returned syringe");
   syringe.movable.rotation=Quaternion.FromToRotation(syringe.tip.forward,s.IrrigationNormal)*syringe.movable.rotation;
   syringe.movable.position+=s.IrrigationPoint-syringe.tip.position;
   s.input.UpdateHeldUse(false,false);s.input.UpdateHeldUse(true,true,2);
   Require(s.input.Held==syringe&&s.Procedure.WashSeconds==3&&e.MistakeCount==beforeWashing,"Adjusting nozzle angle returned syringe");
   s.Procedure.Tick(20);
   int index=s.Procedure.Index,entries=s.Procedure.Entries.Count;
   // The generic restart contract still resets a partially completed step after an actual error.
   e.RecordMistake("단계 재시작 검증",false,syringe);
   Require(e.MistakeCount==4&&s.input.Held==null&&!syringe.IsHeld&&s.Procedure.WashSeconds==0&&!s.Irrigating,"Wash retry did not reset held tool and progress");
   Require(s.Procedure.Index==index&&s.Procedure.Entries.Count==entries&&s.Procedure.Elapsed>=20&&s.ReceiverPlaced,"Retry lost completed progress or evaluation time");
   Require(Target(s,HospitalAction.PlaceDrape,HospitalInteraction.Carry).retired&&old.retired,"Retry reset completed physical tasks");
   yield return .4f;
   Require(s.input.TryGrab(syringe),"Tool cannot be re-grabbed to repeat washing");
   syringe.movable.rotation=Quaternion.FromToRotation(syringe.tip.forward,-s.IrrigationNormal)*syringe.movable.rotation;
   syringe.movable.position+=s.IrrigationPoint-syringe.tip.position;
   s.Irrigate(syringe,true,10);
   Require(s.Procedure.Current.Action==HospitalAction.RespondPain,"Washing cannot finish after retry");
   s.input.UpdateHeldUse(false,false);
   s.input.UpdateHeldUse(true,true,1);
   Require(e.MistakeCount==5&&e.CriticalErrors==1&&e.IsRestarting&&s.input.Held==null&&s.Procedure.Current.Action==HospitalAction.RespondPain,"Out-of-step irrigation no longer restarts the step");
   yield return .4f;
   e.Save(false);
   Require(e.FinalReport!=null&&!e.FinalReport.complete&&!e.FinalReport.passed&&File.Exists(e.ReportPath),"Incomplete evaluation report invalid");
   var saved=JsonUtility.FromJson<HospitalEvaluation.Report>(File.ReadAllText(e.ReportPath));
   Require(saved.mistakeCount==5&&saved.ordinaryErrors==4&&saved.criticalErrors==1&&saved.mistakes.Length==5&&saved.mistakes[0].action=="Chart"&&saved.mistakes[0].selectedTool==syringe.name,"Mistake count/context missing from saved report");
   File.WriteAllText(Result,$"version=syringe-contact-v2; ready=true; assistantRay=true; repeatedAttemptCount=true; priorProgressPreserved=true; washRetry=true; offContactKeepsHeld=true; wrongAngleKeepsHeld=true; triggerConsumed=true; outOfStepUseRestarts=true; mistakes={e.MistakeCount}; report={e.ReportPath}");
  }
 }
}
