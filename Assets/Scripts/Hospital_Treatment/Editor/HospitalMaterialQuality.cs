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
namespace RadCare.HospitalTreatment.Editor {
 [InitializeOnLoad] public static class HospitalMaterialQuality {
  const string Folder="Assets/Scripts/Hospital_Treatment/SurfaceDetails";
  const string Output="Documentation/HospitalTreatment/VisualQuality";
  static HospitalMaterialQuality(){EditorApplication.update+=Poll;}
  static void Poll(){const string request="Temp/HospitalMaterialQuality.request";if(EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists(request))return;string command=File.ReadAllText(request).Trim();File.Delete(request);try{if(command=="preview"){var preview=EditorSceneManager.OpenPreviewScene("Assets/Scenes/Hospital_Tutorial.unity");try{Render(preview,"after");}finally{EditorSceneManager.ClosePreviewScene(preview);}}else Apply();}catch(Exception e){File.WriteAllText("Temp/HospitalMaterialQuality-error.txt",e.ToString());Debug.LogException(e);}}
  static string Profile(string path){
   string n=System.Text.RegularExpressions.Regex.Replace(Path.GetFileNameWithoutExtension(path).ToLowerInvariant(),"_[0-9a-f]{32}$","");
   if(n.Contains("screen")||n.Contains("lens")||n.Contains("shadow")||n.Contains("skin")||n.Contains("pink"))return null;
   if(n.Contains("steel")||n.Contains("metal")||n.Contains("satin")||n.Contains("stand_gray"))return "Metal";
   if(n.Contains("rubber")||n.Contains("tire"))return "Rubber";
   if(n.Contains("gauze")||n.Contains("cotton")||n.Contains("fiber")||n.Contains("fabric")||n.Contains("sheet")||n.Contains("hem")||n.Contains("tape")||n=="mat_used"||path.Contains("waterproof_drape"))return "Fabric";
   if(n.Contains("white")||n.Contains("polymer")||n.Contains("plastic")||n.Contains("teal")||n.Contains("shelf")||n.Contains("ivory")||n.Contains("bezel")||n.Contains("base_pale"))return "Polymer";
   return null;
  }
  static float Height(string kind,float u,float v){
   float a=2*Mathf.PI*u,b=2*Mathf.PI*v;
   if(kind=="Fabric")return .5f+.17f*Mathf.Sin(a*32)*Mathf.Cos(b*32)+.10f*Mathf.Cos(a*64)+.10f*Mathf.Cos(b*64);
   if(kind=="Metal")return .5f+.16f*Mathf.Sin(b*97+a*2)+.08f*Mathf.Sin(b*53)+.04f*Mathf.Cos(b*173+a);
   return .5f+.12f*Mathf.Sin(a*43+b*31)*Mathf.Cos(a*29-b*47)+.05f*Mathf.Cos(a*91+b*67);
  }
  static Texture2D Make(string kind,bool normal){
   const int size=512;var t=new Texture2D(size,size,TextureFormat.RGBA32,false,true);var colors=new Color[size*size];
   float strength=kind=="Fabric"?.65f:kind=="Metal"?.045f:kind=="Rubber"?.25f:.12f;
   float metal=kind=="Metal"?.85f:0;
   float smooth=kind=="Metal"?.68f:kind=="Polymer"?.48f:kind=="Rubber"?.18f:.12f;
   for(int y=0;y<size;y++)for(int x=0;x<size;x++){
    float u=(float)x/size,v=(float)y/size,h=Height(kind,u,v);
    if(normal){var n=new Vector3((Height(kind,u-1f/size,v)-Height(kind,u+1f/size,v))*strength,(Height(kind,u,v-1f/size)-Height(kind,u,v+1f/size))*strength,1).normalized;colors[y*size+x]=new Color(n.x*.5f+.5f,n.y*.5f+.5f,n.z*.5f+.5f,1);}
    else colors[y*size+x]=new Color(metal,0,0,Mathf.Clamp01(smooth+(h-.5f)*.035f));
   }
   t.SetPixels(colors);t.Apply();string path=Folder+"/"+kind+(normal?"_Normal":"_MetallicSmoothness")+".png";File.WriteAllBytes(path,t.EncodeToPNG());UnityEngine.Object.DestroyImmediate(t);
   AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
   var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;importer.sRGBTexture=false;importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Repeat;importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=2;importer.maxTextureSize=size;importer.textureCompression=TextureImporterCompression.Compressed;importer.alphaSource=TextureImporterAlphaSource.FromInput;
   var android=importer.GetPlatformTextureSettings("Android");android.overridden=true;android.maxTextureSize=size;android.format=TextureImporterFormat.ASTC_6x6;importer.SetPlatformTextureSettings(android);importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
  }
  public static void Apply(){
   if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stop Play before material upgrade");
   Directory.CreateDirectory(Folder);Directory.CreateDirectory(Output);
   var scene=EditorSceneManager.OpenPreviewScene("Assets/Scenes/Hospital_Tutorial.unity");
   try{
    if(!File.Exists(Output+"/Fresh_Dressing-before.png"))Render(scene,"before");
    var maps=new Dictionary<string,Texture2D[]>();foreach(var k in new[]{"Fabric","Metal","Polymer","Rubber"})maps[k]=new[]{Make(k,true),Make(k,false)};
    var roots=new[]{"Assets/RadCareHospital","Assets/RadCareTreatmentDetails","Assets/RadCareExpansion/radcare_patient_bed","Assets/Scripts/Hospital_Treatment/Models/FixationTape","Assets/Scripts/Hospital_Treatment/Materials/Guidance"};
    var report=new List<string>();
    foreach(var guid in AssetDatabase.FindAssets("t:Material",roots)){
     string path=AssetDatabase.GUIDToAssetPath(guid),profile=Profile(path);if(profile==null)continue;var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m.shader.name!="Universal Render Pipeline/Lit")continue;
     m.SetTexture("_BumpMap",maps[profile][0]);m.SetFloat("_BumpScale",1);m.EnableKeyword("_NORMALMAP");
     m.SetTexture("_MetallicGlossMap",maps[profile][1]);m.SetFloat("_Smoothness",1);m.SetFloat("_SmoothnessTextureChannel",0);m.EnableKeyword("_METALLICSPECGLOSSMAP");m.enableInstancing=true;EditorUtility.SetDirty(m);report.Add(profile+" : "+path);
    }
    AssetDatabase.SaveAssets();Render(scene,"after");File.WriteAllLines(Output+"/materials.txt",report);File.WriteAllText("Temp/HospitalMaterialQuality-result.txt",report.Count+" materials; 8 shared 512px maps; Android ASTC 6x6; no mesh, collider or scene transform changes.");
   }finally{EditorSceneManager.ClosePreviewScene(scene);}
  }
  static void Render(Scene scene,string suffix){
   var objects=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
   foreach(var name in new[]{"Irrigation_Cart","Needleless_Irrigation_Syringe","Fresh_Dressing","Patient_Bed"}){
    foreach(var r in objects.SelectMany(t=>t.GetComponents<Renderer>()))r.gameObject.layer=0;
    var target=objects.First(t=>t.name==name);var renderers=target.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.enabled).ToArray();if(renderers.Length==0)continue;var bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);
    File.WriteAllLines(Output+"/"+name+"-bindings.txt",renderers.SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Distinct().Select(m=>m.name+" : "+AssetDatabase.GetAssetPath(m)));
    foreach(var r in renderers)r.gameObject.layer=29;
    var go=new GameObject("Material QA camera");SceneManager.MoveGameObjectToScene(go,scene);var c=go.AddComponent<Camera>();c.scene=scene;c.cullingMask=1<<29;c.enabled=false;c.nearClipPlane=.005f;c.farClipPlane=100;c.orthographic=true;c.orthographicSize=Mathf.Max(bounds.size.magnitude*.48f,.06f);c.backgroundColor=new Color(.09f,.11f,.14f);c.clearFlags=CameraClearFlags.SolidColor;c.GetUniversalAdditionalCameraData().allowXRRendering=false;
    c.transform.position=bounds.center+new Vector3(1,2f,1).normalized*Mathf.Max(bounds.size.magnitude*2,.3f);c.transform.LookAt(bounds.center);
    var rt=new RenderTexture(1000,800,24);rt.Create();var prev=RenderTexture.active;Texture2D tex=null;
    try{RenderPipeline.SubmitRenderRequest(c,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;tex=new Texture2D(1000,800,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1000,800),0,0);tex.Apply();File.WriteAllBytes(Output+"/"+name+"-"+suffix+".png",tex.EncodeToPNG());}
    finally{RenderTexture.active=prev;if(tex!=null)UnityEngine.Object.DestroyImmediate(tex);rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);}
   }
  }
 }
}
