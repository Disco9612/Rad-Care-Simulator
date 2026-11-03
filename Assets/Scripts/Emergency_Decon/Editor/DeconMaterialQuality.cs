using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
public static class DeconMaterialQuality {
 const string Folder="Assets/Scripts/Emergency_Decon/SurfaceDetails";
 const string Output="Documentation/EmergencyDecon/VisualQuality";
 [InitializeOnLoadMethod] static void Initialize(){EditorApplication.update+=Poll;}
 static void Poll(){const string request="Temp/DeconMaterialQuality.request";if(EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists(request))return;File.Delete(request);try{Apply();}catch(Exception e){File.WriteAllText("Temp/DeconMaterialQuality-error.txt",e.ToString());Debug.LogException(e);}}
 static Texture2D Read(string path){var t=new Texture2D(2,2,TextureFormat.RGBA32,false,true);if(!t.LoadImage(File.ReadAllBytes(path)))throw new Exception(path);return t;}
 static Texture2D Pack(string path,string metallicPath=null,bool rma=false,bool steel=false){
  string output=Folder+"/"+Path.GetFileNameWithoutExtension(path)+"_URP.png";
  var rough=Read(path);var metal=metallicPath==null?null:Read(metallicPath);int w=Mathf.Min(1024,rough.width),h=Mathf.Min(1024,rough.height);var t=new Texture2D(w,h,TextureFormat.RGBA32,false,true);var colors=new Color[w*h];
  for(int y=0;y<h;y++)for(int x=0;x<w;x++){float u=(x+.5f)/w,v=(y+.5f)/h;Color r=rough.GetPixelBilinear(u,v);float m=steel?.9f:rma?r.g:metal==null?0:metal.GetPixelBilinear(u,v).r;colors[y*w+x]=new Color(m,rma?r.b:1,0,Mathf.Clamp(1-r.r,.05f,.85f));}
  t.SetPixels(colors);t.Apply();File.WriteAllBytes(output,t.EncodeToPNG());UnityEngine.Object.DestroyImmediate(t);UnityEngine.Object.DestroyImmediate(rough);if(metal!=null)UnityEngine.Object.DestroyImmediate(metal);
  AssetDatabase.ImportAsset(output,ImportAssetOptions.ForceSynchronousImport);var importer=(TextureImporter)AssetImporter.GetAtPath(output);importer.sRGBTexture=false;importer.mipmapEnabled=true;importer.maxTextureSize=1024;importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.wrapMode=TextureWrapMode.Repeat;importer.filterMode=FilterMode.Trilinear;var a=importer.GetPlatformTextureSettings("Android");a.overridden=true;a.maxTextureSize=1024;a.format=TextureImporterFormat.ASTC_6x6;importer.SetPlatformTextureSettings(a);importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(output);
 }
 static string Emission(Material m)=>m.GetColor("_EmissionColor").ToString("R")+"|"+AssetDatabase.GetAssetPath(m.GetTexture("_EmissionMap"))+"|"+m.GetTextureScale("_EmissionMap")+"|"+m.GetTextureOffset("_EmissionMap")+"|"+m.IsKeywordEnabled("_EMISSION")+"|"+m.globalIlluminationFlags;
 public static void Apply(){
  if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Edit mode required");Directory.CreateDirectory(Folder);Directory.CreateDirectory(Output);
  var paths=new[]{"Assets/Scenes/Emergency_Decon_Tutorial.unity","Assets/Scenes/Emergency_Decon_Tutorial_Hand.unity","Assets/Scenes/Emergency_Decon_Evaluation.unity"};var materials=AssetDatabase.GetDependencies(paths,true).Where(p=>p.EndsWith(".mat")).Select(p=>AssetDatabase.LoadAssetAtPath<Material>(p)).Where(m=>m!=null&&m.shader.name=="Universal Render Pipeline/Lit").ToArray();
  var before=materials.ToDictionary(m=>m,Emission);var report=new List<string>();var packed=new Dictionary<string,Texture2D>();
  foreach(var m in materials){string path=AssetDatabase.GetAssetPath(m);if(path.ToLowerInvariant().Contains("placeholder"))continue;string basePath=AssetDatabase.GetAssetPath(m.GetTexture("_BaseMap")),rough=null,metal=null;bool rma=false,steel=false;
   if(path.Contains("Tent_Unreal/")&&basePath.EndsWith("_ALB.PNG",StringComparison.OrdinalIgnoreCase)){rough=basePath.Substring(0,basePath.Length-8)+"_RMA.PNG";rma=true;}
   else if(path.EndsWith("SurvayMeter2/emf_detector.mat")){rough="Assets/3D Asset/SurvayMeter2/Texture/emf_detector_Roughness.png";metal="Assets/3D Asset/SurvayMeter2/Texture/emf_detector_Metallic.png";}
   else if(path.EndsWith("/Patient.mat")){rough=basePath.Replace("_BaseColor.png","_Roughness.png");metal=basePath.Replace("_BaseColor.png","_Metallic.png");}
   else if(Path.GetFileName(path)=="Steel_Stainlessl_Shiny_instruments.mat"){rough=basePath;steel=true;}
   if(rough!=null&&File.Exists(rough)){
    if(!packed.TryGetValue(rough,out var map)){map=Pack(rough,metal,rma,steel);packed[rough]=map;}
    m.SetTexture("_MetallicGlossMap",map);m.SetFloat("_SmoothnessTextureChannel",0);m.SetFloat("_Smoothness",1);m.EnableKeyword("_METALLICSPECGLOSSMAP");
    if(rma){m.SetTexture("_OcclusionMap",map);m.SetFloat("_OcclusionStrength",.65f);m.EnableKeyword("_OCCLUSIONMAP");}
    if(steel){m.SetTexture("_BaseMap",null);m.SetTexture("_MainTex",null);}
    EditorUtility.SetDirty(m);report.Add(path+" <- "+rough);
   }else if(Path.GetFileName(path)=="Saline_Bottle_M.mat"){m.SetFloat("_Smoothness",.35f);EditorUtility.SetDirty(m);report.Add(path+" : polymer smoothness 0.35");}
  }
  foreach(var pair in before)if(Emission(pair.Key)!=pair.Value)throw new Exception("Emission changed: "+pair.Key.name);
  AssetDatabase.SaveAssets();File.WriteAllLines(Output+"/materials.txt",report);Preview(paths[0]);File.WriteAllText("Temp/DeconMaterialQuality-result.txt",report.Count+" materials improved; "+packed.Count+" packed maps; emission properties/keywords/GI unchanged for "+before.Count+" materials across all three scenes. Placeholder materials untouched.");
 }
 static void Preview(string path){var scene=EditorSceneManager.OpenPreviewScene(path);try{
  var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Renderer>(true)).ToArray();foreach(var r in all)r.gameObject.layer=0;
  var behaviours=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MonoBehaviour>(true)).Where(b=>b!=null).ToArray();
  foreach(var name in new[]{"SurveyMeter","SalineBottle","Forceps"}){
   var target=behaviours.FirstOrDefault(b=>b.GetType().Name==name);if(target==null)continue;var rs=target.GetComponentsInChildren<Renderer>(true).Where(r=>r.enabled&&!(r is ParticleSystemRenderer)&&!(r is LineRenderer)).ToArray();if(rs.Length==0)continue;
   var bounds=rs[0].bounds;foreach(var r in rs){bounds.Encapsulate(r.bounds);r.gameObject.layer=29;}
   var go=new GameObject("Decon QA camera");SceneManager.MoveGameObjectToScene(go,scene);var c=go.AddComponent<Camera>();c.scene=scene;c.enabled=false;c.cullingMask=1<<29;c.orthographic=true;c.orthographicSize=Mathf.Max(.08f,bounds.size.magnitude*.48f);c.nearClipPlane=.005f;c.farClipPlane=100;c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=new Color(.08f,.1f,.13f);c.GetUniversalAdditionalCameraData().allowXRRendering=false;c.transform.position=bounds.center+new Vector3(1,1,1).normalized*Mathf.Max(.5f,bounds.size.magnitude*2);c.transform.LookAt(bounds.center);
   var clones=new List<Material>();var originals=rs.Select(r=>r.sharedMaterials).ToArray();
   try{Capture(c,name+"-normal");for(int i=0;i<rs.Length;i++){var ms=originals[i].Select(m=>{var copy=new Material(m);clones.Add(copy);copy.EnableKeyword("_EMISSION");if(copy.HasProperty("_EmissionColor"))copy.SetColor("_EmissionColor",Color.white);return copy;}).ToArray();rs[i].sharedMaterials=ms;}Capture(c,name+"-glow");}
   finally{for(int i=0;i<rs.Length;i++){rs[i].sharedMaterials=originals[i];rs[i].gameObject.layer=0;}foreach(var m in clones)UnityEngine.Object.DestroyImmediate(m);UnityEngine.Object.DestroyImmediate(go);}
  }
 }finally{EditorSceneManager.ClosePreviewScene(scene);}}
 static void Capture(Camera c,string name){var rt=new RenderTexture(900,700,24);rt.Create();var prev=RenderTexture.active;var tex=new Texture2D(900,700,TextureFormat.RGB24,false);try{RenderPipeline.SubmitRenderRequest(c,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,900,700),0,0);tex.Apply();File.WriteAllBytes(Output+"/"+name+".png",tex.EncodeToPNG());}finally{RenderTexture.active=prev;UnityEngine.Object.DestroyImmediate(tex);rt.Release();UnityEngine.Object.DestroyImmediate(rt);}}
}
