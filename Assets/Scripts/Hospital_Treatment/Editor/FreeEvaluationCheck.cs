using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using TMPro;
using RadCare.HospitalTreatment;
[InitializeOnLoad]
public static class FreeEvaluationCheck
{
 const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
 static FreeEvaluationCheck(){EditorApplication.update+=Poll;}
 static void Require(bool value,string reason){if(!value)throw new Exception(reason);}
 static void Invoke(object obj,string method)=>obj.GetType().GetMethod(method,Flags).Invoke(obj,null);
 static void Set(object obj,string field,object value)=>obj.GetType().GetField(field,Flags).SetValue(obj,value);
 static void Poll(){if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlaying||!File.Exists("Temp/FreeEvaluationCheck.request"))return;try{File.Delete("Temp/FreeEvaluationCheck.request");}catch(IOException){return;}try{Run();File.WriteAllText("Temp/FreeEvaluationCheck-result.txt","PASS: out-of-order actions; unique completion vs repeated timeline; finish freezes time/events; 3-second hold/release/focus; tutorial gate unchanged; both scene tool availability; results layout/render.");}catch(Exception e){File.WriteAllText("Temp/FreeEvaluationCheck-error.txt",e.ToString());Debug.LogException(e);}}
 static void Run(){
  var record=new FreeEvaluationRecord{procedures=new[]{new FreeEvaluationRecord.Procedure("a","첫 절차"),new FreeEvaluationRecord.Procedure("b","다음 절차")}};
  record.Add("a","before start");Require(record.timeline.Count==0,"Prestart event recorded");record.Begin();record.Tick(65.9f);record.Add("b","second first",4);record.Tick(2);record.Add("b","repeat",1);record.Add("grab","grab",0,false);Require(record.CompletedCount==1&&record.timeline.Count==3&&record.timeline[0].at==65.9f&&record.timeline[2].order==3,"Order/coverage/time");record.finished=true;record.Tick(9);record.Add("a","late");Require(record.seconds==67.9f&&record.timeline.Count==3,"Finish did not freeze");
  var sequence=new FreeEvaluationRecord{procedures=new[]{new FreeEvaluationRecord.Procedure("wash","식염수 세척"),new FreeEvaluationRecord.Procedure("wipe","거즈로 닦기")}};
  sequence.Begin();sequence.Add("grab","거즈 집기",0,false);sequence.Add("wipe","거즈로 닦기",3);sequence.Add("wash","식염수 세척",10);sequence.Add("wipe","거즈로 닦기",3);
  Require(sequence.CompletedCount==2&&sequence.OrderErrorCount==1&&sequence.HasOrderError("wipe"),"Retry erased order error or completion");
  Require(sequence.timeline[1].orderError&&sequence.timeline[1].expectedKey=="wash"&&!sequence.timeline[3].orderError,"Incorrect event order evidence");
  var restored=JsonUtility.FromJson<FreeEvaluationRecord>(JsonUtility.ToJson(sequence));Require(restored.OrderErrorCount==1&&restored.timeline[1].expectedLabel=="식염수 세척","Saved order evidence lost");
  var correct=new FreeEvaluationRecord{procedures=sequence.procedures};correct.Begin();correct.Add("wash","세척");correct.Add("wipe","닦기");correct.Add("wipe","반복");Require(correct.OrderErrorCount==0,"Correct sequence/repeat flagged");
  var hold=new EvaluationFinishHold();Require(!hold.Tick(true,true,4),"Held on entry finished");hold.Tick(true,false,0);Require(!hold.Tick(true,true,2.99f),"Early finish");Require(hold.Tick(true,true,.02f),"3 second finish missing");hold.Tick(true,false,0);hold.Tick(true,true,2);hold.Tick(false,true,10);Require(!hold.Tick(true,true,4),"Focus recovery finished");hold.Tick(true,false,0);hold.Tick(true,true,2);hold.Tick(true,false,0);Require(!hold.Tick(true,true,2),"Release did not cancel");
  var p=new HospitalProcedure{FreeEvaluation=true};Require(!p.Try(HospitalAction.SaveChart),"Prestart action");p.Try(HospitalAction.Start);Require(p.Try(HospitalAction.SaveChart)&&p.Try(HospitalAction.PlaceDrape)&&!p.Complete,"Free order gated/auto finished");Require(p.Irrigate(10,true),"Free irrigation gated");p.EndEvaluation();Require(!p.Try(HospitalAction.Chart),"Postfinish action");var tutorial=new HospitalProcedure();tutorial.Try(HospitalAction.Start);Require(!tutorial.Try(HospitalAction.SaveChart),"Tutorial gate changed");
  foreach(string name in new[]{"Hospital_Evaluation","Emergency_Decon_Evaluation"}){
   var scene=EditorSceneManager.OpenPreviewScene("Assets/Scenes/"+name+".unity");try{
    if(name.StartsWith("Hospital")){
     var s=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<HospitalScenario>(true)).Single();var eval=s.GetComponent<HospitalEvaluation>();Invoke(eval,"Awake");Invoke(s,"Start");s.Procedure.Try(HospitalAction.Start);eval.RecordAction(HospitalAction.Start);
     Require(s.targets.Where(t=>t.IsMovable&&!t.retired).All(s.CanSelect),"Hospital tool gate");Require(eval.Session.Record.procedures.Length==21,"Hospital procedure count");
     var chart=s.GetComponent<HospitalChartScreen>();chart.RefreshScreen();
     var chartRoot=(GameObject)typeof(HospitalChartScreen).GetField("displayRoot",Flags).GetValue(chart);
     foreach(var text in chartRoot.GetComponentsInChildren<TMP_Text>()){text.ForceMeshUpdate(true,true);Require(!text.isTextOverflowing,"Chart text overflow: "+text.text);}
     var savedActive=RenderTexture.active;var chartImage=new Texture2D(1024,768,TextureFormat.RGB24,false);RenderTexture.active=chart.Display;chartImage.ReadPixels(new Rect(0,0,1024,768),0,0);chartImage.Apply();File.WriteAllBytes("Temp/FreeEvaluationChart.png",chartImage.EncodeToPNG());RenderTexture.active=savedActive;UnityEngine.Object.DestroyImmediate(chartImage);
     // Generated off-screen objects are not part of the preview scene.
     UnityEngine.Object.DestroyImmediate(chartRoot);
     eval.Session.Record.started=false;
    }else{
     var manager=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<EvaluationGameManager>(true)).Single();Invoke(manager,"Awake");manager.Begin();Require(manager.CanWash&&manager.CanTreat&&manager.CanScan&&manager.CanReturnForceps,"Decon action gates");manager.ReportWashingCompleted(10);Require(manager.IsRunning&&manager.Session.Record.Has("wash"),"Wash before measurement failed");manager.Session.Record.started=false;
    }
   }finally{EditorSceneManager.ClosePreviewScene(scene);}
  }
  Render();
 }
 static void Render(){var scene=EditorSceneManager.NewPreviewScene();try{
  var root=new GameObject("Evaluation preview");SceneManager.MoveGameObjectToScene(root,scene);var menu=root.AddComponent<TutorialCompletionMenu>();Set(menu,"catalog",Resources.Load<TrainingCatalog>("TrainingCatalog"));
  var record=new FreeEvaluationRecord{scenario="Preview",procedures=Enumerable.Range(1,21).Select(i=>new FreeEvaluationRecord.Procedure("p"+i,i%2==0?"처치 후 세 부위 재측정":"오염 소모품 폐기")).ToArray()};record.Begin();for(int i=0;i<19;i++){record.Tick(43);record.Add("p"+(i+2),i%2==0?"처치 후 세 부위 재측정":"오염 소모품 폐기",10);}record.finished=true;Set(menu,"report",record);Invoke(menu,"BuildReport");
  Canvas.ForceUpdateCanvases();foreach(var text in root.GetComponentsInChildren<TMP_Text>()){text.ForceMeshUpdate(true,true);Require(!text.isTextOverflowing,"Result text overflow: "+text.text);}
  var go=new GameObject("Preview camera");SceneManager.MoveGameObjectToScene(go,scene);var camera=go.AddComponent<Camera>();camera.enabled=false;camera.scene=scene;camera.orthographic=true;camera.orthographicSize=.60f;camera.transform.position=new Vector3(0,0,-2);camera.nearClipPlane=.01f;camera.farClipPlane=10;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.01f,.02f,.03f);camera.GetUniversalAdditionalCameraData().allowXRRendering=false;
  var rt=new RenderTexture(1600,1050,24);rt.Create();var old=RenderTexture.active;var image=new Texture2D(1600,1050,TextureFormat.RGB24,false);try{RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1600,1050),0,0);image.Apply();File.WriteAllBytes("Temp/FreeEvaluationResults.png",image.EncodeToPNG());}finally{RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(image);rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
 }finally{EditorSceneManager.ClosePreviewScene(scene);}}
}
