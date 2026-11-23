using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
public static partial class EmergencyLightingWorkbench {
 [MenuItem("Tools/RadCare/Emergency Lighting/Fix Hand Bloom")]
 static void FixHandBloom(){
 if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop play mode before saving scenes.");
 var initial=SceneManager.GetActiveScene();if(initial.isDirty)throw new System.InvalidOperationException("Save scene edits first.");
 string returnPath=string.IsNullOrEmpty(initial.path)?Scenes[0]:initial.path;
 var shader=Shader.Find("RadCare/BloomSafeHand");if(!shader||ShaderUtil.ShaderHasError(shader))throw new System.Exception("Hand shader missing or invalid");
 var report=new System.Collections.Generic.List<string>();
 foreach(var path in Scenes){var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path);
 int count=0;
 foreach(var r in All<SkinnedMeshRenderer>(scene).Where(r=>PathOf(r.transform).Contains("/OVRHandVisualLeft/")||PathOf(r.transform).Contains("/OVRHandVisualRight/"))){
 var mats=r.sharedMaterials;
 for(int i=0;i<mats.Length;i++){
 var source=mats[i];if(!source || (source.shader.name!="Interaction/OculusHand"&&source.shader!=shader))continue;
 if(source.shader!=shader){
 string target=AssetsRoot+"/BloomSafe_"+source.name+".mat";
 var mat=AssetDatabase.LoadAssetAtPath<Material>(target);
 if(!mat){mat=new Material(source);AssetDatabase.CreateAsset(mat,target);}
 mat.shader=shader;EditorUtility.SetDirty(mat);mats[i]=mat;
 }
 count++;
 }
 r.sharedMaterials=mats;
 }
 if(count!=4)throw new System.Exception("Expected four primary hand renderers in "+scene.name+", found "+count);
 UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
 report.Add(scene.name+": four primary hand renderers use bounded HDR output; PASS");
 }
 AssetDatabase.SaveAssets();UnityEditor.SceneManagement.EditorSceneManager.OpenScene(returnPath);
 File.WriteAllLines("Temp/hand-fix.txt",report);
 TestHandBloom(shader);
 }
 static void TestHandBloom(Shader shader){
 var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);go.hideFlags=HideFlags.HideAndDontSave;go.layer=31;go.transform.position=new Vector3(0,-1000,0);
 var cameraGo=new GameObject("Hand HDR regression camera");cameraGo.hideFlags=HideFlags.HideAndDontSave;var cam=cameraGo.AddComponent<Camera>();cam.enabled=false;cam.transform.position=go.transform.position+Vector3.back*2;cam.cullingMask=1<<31;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Color.black;cam.allowHDR=true;
 var data=UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(cam);data.allowXRRendering=false;data.renderPostProcessing=false;
 var rt=new RenderTexture(128,128,24,RenderTextureFormat.ARGBFloat);rt.Create();var tex=new Texture2D(128,128,TextureFormat.RGBAFloat,false,true);var old=RenderTexture.active;var results=new System.Collections.Generic.List<string>();
 try{foreach(var tested in new[]{Shader.Find("Interaction/OculusHand"),shader}){
 var mat=new Material(tested);try{mat.SetColor("_ColorTop",Color.white*32);mat.SetColor("_ColorBottom",Color.white*32);mat.SetFloat("_Opacity",1);mat.SetFloat("_WristFade",1);mat.SetFloat("_OutlineOpacity",0);go.GetComponent<Renderer>().sharedMaterial=mat;
 UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(cam,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,128,128),0,0);tex.Apply();
 float peak=tex.GetPixels().Max(c=>Mathf.Max(c.r,c.g,c.b));results.Add(tested.name+": HDR peak="+peak);
 if(tested==shader && (peak>1.01f||peak<.5f))throw new System.Exception("Hand HDR regression failed: "+peak);
 if(tested!=shader && peak<2)throw new System.Exception("Baseline HDR control did not render");
 }finally{Object.DestroyImmediate(mat);}}
 }finally{RenderTexture.active=old;Object.DestroyImmediate(go);Object.DestroyImmediate(cameraGo);Object.DestroyImmediate(tex);rt.Release();Object.DestroyImmediate(rt);}
 File.WriteAllLines("Temp/hand-hdr-test.txt",results);
 }

 static void AuditHands(){
 var s=SceneManager.GetActiveScene();var lines=new System.Collections.Generic.List<string>();
 foreach(var r in All<Renderer>(s).Where(r=>PathOf(r.transform).Contains("Hand")||PathOf(r.transform).Contains("Interactor"))){
 lines.Add(PathOf(r.transform)+" | "+r.GetType().Name+" enabled="+r.enabled);
 foreach(var m in r.sharedMaterials.Where(m=>m)){lines.Add(" MAT "+AssetDatabase.GetAssetPath(m)+" shader="+m.shader.name);foreach(var prop in new[]{"_Color","_BaseColor","_EmissionColor","_GlowColor","_SpecColor"})if(m.HasProperty(prop))lines.Add(prop+"="+m.GetColor(prop));}
 }
 foreach(var c in All<MonoBehaviour>(s).Where(c=>c && c.GetType().Name.Contains("Glow"))){lines.Add("COMP "+PathOf(c.transform)+" "+c.GetType().Name+" "+EditorJsonUtility.ToJson(c));}
 File.WriteAllLines("Temp/hand-audit.txt",lines);
 }
}
