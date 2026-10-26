using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
namespace RadCare.HospitalTreatment.Editor {
 public static class HospitalGlowInstaller {
  public static void Install(){
   var scene=SceneManager.GetActiveScene();
   if(EditorApplication.isPlaying||scene.path!="Assets/Scenes/Hospital_Tutorial.unity")throw new Exception("Hospital edit mode required");
   EditorSceneManager.SaveScene(scene,"Temp/Hospital-before-object-glow.unity",true);
   var s=UnityEngine.Object.FindAnyObjectByType<HospitalScenario>();
   var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
   Transform Find(string name)=>all.First(t=>t.name==name);
   Renderer[] Renderers(Transform root)=>root.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.enabled).Cast<Renderer>().ToArray();
   var glow=s.GetComponent<HospitalObjectGlow>()??s.gameObject.AddComponent<HospitalObjectGlow>();glow.scenario=s;
   var bindings=new List<HospitalObjectGlow.Binding>();
   foreach(var t in s.targets){
    Transform obj=null;
    if(t.IsMovable)obj=t.movable;
    else if(t.kind==HospitalInteraction.Socket){
     if(t.action==HospitalAction.DisposeDressing||t.action==HospitalAction.ClearWaste)obj=Find("Clinical_Waste_Container");
     else if(t.action==HospitalAction.PlaceReceiver)obj=Find("Receiver_Support_Tray");
     else obj=s.wound;
    }else switch(t.action){
     case HospitalAction.AskPain:case HospitalAction.ExplainPlan:obj=Find("Patient_Supine");break;
     case HospitalAction.AdjustLight:obj=s.lamp;break;
     case HospitalAction.MeasureWound:case HospitalAction.MeasureSkin:case HospitalAction.MeasureReference:obj=s.wound;break;
     case HospitalAction.SecureDressing:obj=s.tapeVisual.transform;break;
     default:obj=Find("Chart_Tablet");break;
    }
    var rs=Renderers(obj);if(rs.Length==0)throw new Exception("No actual object renderers: "+t.name+" -> "+obj.name);
    bindings.Add(new HospitalObjectGlow.Binding{target=t,renderers=rs});
   }
   glow.bindings=bindings.ToArray();glow.returnCart=Renderers(Find("Irrigation_Cart"));
   const string folder="Assets/Scripts/Hospital_Treatment/Materials/Guidance";Directory.CreateDirectory(folder);AssetDatabase.Refresh();
   var copies=new Dictionary<Material,Material>();var report=new System.Text.StringBuilder();
   foreach(var r in bindings.SelectMany(b=>b.renderers).Concat(glow.returnCart).Distinct()){
    var mats=r.sharedMaterials;
    for(int i=0;i<mats.Length;i++){
     var src=mats[i];if(src==null||!src.HasProperty("_EmissionColor"))throw new Exception("Emission unsupported: "+r.name);
     if(!copies.TryGetValue(src,out var dst)){
      string id=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(src));
      string path=folder+"/"+src.name+"_"+id+".mat";
      dst=AssetDatabase.LoadAssetAtPath<Material>(path);
      if(dst==null){dst=new Material(src);AssetDatabase.CreateAsset(dst,path);}
      dst.EnableKeyword("_EMISSION");dst.SetColor("_EmissionColor",Color.white*.001f);
      if(src.HasProperty("_BaseMap"))dst.SetTexture("_EmissionMap",src.GetTexture("_BaseMap"));
      dst.globalIlluminationFlags=MaterialGlobalIlluminationFlags.None;EditorUtility.SetDirty(dst);copies.Add(src,dst);
      report.AppendLine(src.name+" | "+src.shader.name+" | emission enabled | base texture preserved");
     }
     mats[i]=dst;
    }
    r.sharedMaterials=mats;
   }
   foreach(var t in s.targets){
    var mesh=t.GetComponent<MeshRenderer>();if(mesh!=null)UnityEngine.Object.DestroyImmediate(mesh);
    var filter=t.GetComponent<MeshFilter>();if(filter!=null)UnityEngine.Object.DestroyImmediate(filter);
   }
   EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
   File.WriteAllText("Temp/HospitalGlow-material-audit.txt",report.ToString()+"Bindings: "+bindings.Count+"; materials: "+copies.Count);
  }
 }
}
