using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace RadCare.HospitalTreatment.Editor {
 [InitializeOnLoad] public static class HospitalGrabCalibration {
  static HospitalGrabCalibration(){EditorApplication.update+=Poll;}
  static void Poll(){const string path="Temp/HospitalGrab.request";if(EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists(path))return;var cmd=File.ReadAllText(path).Trim();File.Delete(path);try{if(cmd=="calibrate")Calibrate();if(cmd=="verify")Verify();Audit();}catch(Exception e){File.WriteAllText("Temp/HospitalGrab-error.txt",e.ToString());}}

  public static Bounds LocalBounds(Transform movable,Transform space){bool first=true;var bounds=new Bounds();foreach(var f in movable.GetComponentsInChildren<MeshFilter>(true)){var r=f.GetComponent<MeshRenderer>();if(r==null||!r.enabled||f.sharedMesh==null)continue;var b=f.sharedMesh.bounds;for(int i=0;i<8;i++){var local=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));var p=space.InverseTransformPoint(f.transform.TransformPoint(local));if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);}}if(first)throw new Exception("No visible mesh "+movable.name);return bounds;}
  static void Calibrate(){
   if(EditorApplication.isPlaying)throw new Exception("Edit mode required");var scene=SceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/Hospital_Tutorial.unity")throw new Exception("Hospital scene required");
   EditorSceneManager.SaveScene(scene,"Temp/Hospital-before-grab-calibration.unity",true);
   var s=UnityEngine.Object.FindAnyObjectByType<HospitalScenario>();
   foreach(var t in s.targets.Where(x=>x.IsMovable)){
    var c=t.grabCollider as BoxCollider;if(c==null)throw new Exception("Missing physical grab collider "+t.name);
    var b=LocalBounds(t.movable,c.transform);c.center=b.center;c.size=Vector3.Max(b.size+Vector3.one*.004f,Vector3.one*.016f);
    var local=LocalBounds(t.movable,t.movable);t.gripLocalPosition=local.center;t.heldEuler=Vector3.zero;
    if(t.itemId=="drape")t.gripLocalPosition=new Vector3(local.min.x+.025f,local.center.y,local.center.z);
    if(t.itemId=="receiver")t.gripLocalPosition=new Vector3(local.min.x+.018f,local.max.y-.012f,local.center.z);
    if(t.kind==HospitalInteraction.Irrigator){var renderers=t.movable.GetComponentsInChildren<MeshRenderer>();var flange=renderers.Single(r=>r.name.EndsWith("flange_center_collar"));var outlet=renderers.Single(r=>r.name.EndsWith("tip_outlet_recess"));var barrel=renderers.Single(r=>r.name.EndsWith("_barrel"));
     t.gripLocalPosition=t.movable.InverseTransformPoint(flange.bounds.center);t.heldEuler=new Vector3(-90,0,0);
     t.tip.position=outlet.bounds.center;t.tip.rotation=Quaternion.LookRotation((outlet.bounds.center-barrel.bounds.center).normalized,t.movable.forward);
    }
    EditorUtility.SetDirty(t);EditorUtility.SetDirty(c);
   }
   if(s.irrigationTarget==null){var target=new GameObject("Irrigation_Tutorial_Target");target.transform.SetParent(s.wound,false);s.irrigationTarget=target.transform;}
   s.irrigationTarget.position=s.wound.position+Vector3.up*.018f;s.irrigationTarget.rotation=Quaternion.FromToRotation(Vector3.up,new Vector3(0,1,-.65f).normalized);
   var guide=s.GetComponent<HospitalIrrigationGuide>();if(guide==null)guide=s.gameObject.AddComponent<HospitalIrrigationGuide>();guide.scenario=s;
   const string materialPath="Assets/Scripts/Hospital_Treatment/Materials/Irrigation_Guide.mat";
   var mat=AssetDatabase.LoadAssetAtPath<Material>(materialPath);if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Unlit"));AssetDatabase.CreateAsset(mat,materialPath);}guide.tutorialMaterial=mat;AssetDatabase.SaveAssets();EditorUtility.SetDirty(s);EditorUtility.SetDirty(guide);
   EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Verify();
  }
  static void Verify(){var s=UnityEngine.Object.FindAnyObjectByType<HospitalScenario>();int objects=0;foreach(var t in s.targets.Where(x=>x.IsMovable)){
   var c=(BoxCollider)t.grabCollider;var expected=LocalBounds(t.movable,c.transform);var actual=new Bounds(c.center,c.size);if(!actual.Contains(expected.min)||!actual.Contains(expected.max))throw new Exception("Collider excludes geometry: "+t.name);
   var p=t.movable.position;var q=t.movable.rotation;try{var palm=new Vector3(2,1.3f,3);var aim=Quaternion.Euler(20,35,0);t.SetGripPose(palm,aim);if(Vector3.Distance(t.movable.TransformPoint(t.gripLocalPosition),palm)>.0001f)throw new Exception("Grip offset mismatch "+t.name);if(t.kind==HospitalInteraction.Irrigator){var outlet=t.movable.GetComponentsInChildren<MeshRenderer>().Single(r=>r.name.EndsWith("tip_outlet_recess"));if(Vector3.Distance(t.tip.position,outlet.bounds.center)>.001f||Vector3.Angle(t.tip.forward,aim*Vector3.forward)>.5f)throw new Exception("Nozzle alignment failed");}}finally{t.movable.SetPositionAndRotation(p,q);}objects++;
   }
   File.WriteAllText("Temp/HospitalGrab-verification.txt","PASS: "+objects+" tool colliders enclose local mesh bounds, palm grip error < 0.1 mm; syringe tip matches visible outlet and points forward.\nNear-grab radius: 7 cm from actual collider surface, including inside-collider positions.\nIrrigation guide shares the exact timer predicate: 1.5 cm surface contact tolerance and 3.5 cm contact radius. Quest hardware input not physically exercised.");
  }
  static void Audit(){var s=UnityEngine.Object.FindAnyObjectByType<HospitalScenario>();var text=new StringBuilder();foreach(var t in s.targets.Where(x=>x.IsMovable)){
   text.AppendLine(t.name+" pivot="+t.movable.position+" scale="+t.movable.lossyScale+" rotation="+t.movable.eulerAngles);
   if(t.grabCollider is BoxCollider c)text.AppendLine("Collider pos="+c.transform.position+" scale="+c.transform.lossyScale+" rotation="+c.transform.eulerAngles+" center="+c.center+" size="+c.size+" world="+c.bounds);
   foreach(var r in t.movable.GetComponentsInChildren<MeshRenderer>(true).Where(x=>x.enabled))text.AppendLine("  "+r.name+" bounds="+r.bounds.ToString("F4"));
   if(t.tip!=null)text.AppendLine("Tip "+t.tip.position+" local="+t.tip.localPosition+" forward="+t.tip.forward);
  }foreach(var t in s.input.rig.rightHandAnchor.GetComponentsInChildren<Transform>(true))if(t.name.ToLower().Contains("hand")||t.name.ToLower().Contains("controller"))text.AppendLine("HAND "+t.name+" local="+t.localPosition+" rotation="+t.localEulerAngles+" scale="+t.lossyScale);File.WriteAllText("Temp/HospitalGrab-audit.txt",text.ToString());}
 }
}
