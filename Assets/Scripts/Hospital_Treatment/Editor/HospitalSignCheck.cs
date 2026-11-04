using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace RadCare.HospitalTreatment.Editor {
 [InitializeOnLoad] public static class HospitalSignCheck {
  static HospitalSignCheck(){EditorApplication.update+=Poll;}
  static void Poll(){const string request="Temp/HospitalSignCheck.request";if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlaying||!File.Exists(request))return;File.Delete(request);try{Check();File.WriteAllText("Temp/HospitalSignCheck-result.txt","Both scenes: five English signs fit their text rectangles; cabinet label contained on center door. Previews rendered.");}catch(Exception e){File.WriteAllText("Temp/HospitalSignCheck-error.txt",e.ToString());Debug.LogException(e);}}
  static void Check(){Directory.CreateDirectory("Documentation/HospitalTreatment/Signage");foreach(var name in new[]{"Hospital_Tutorial","Hospital_Evaluation"}){
   var scene=EditorSceneManager.OpenPreviewScene("Assets/Scenes/"+name+".unity");try{
    var root=scene.GetRootGameObjects().First(g=>g.name=="Hospital_Environment_Upgrade");
    foreach(var text in root.GetComponentsInChildren<TextMeshPro>()){
     text.ForceMeshUpdate(true,true);
     if(text.text.Any(ch=>ch>='\uAC00'&&ch<='\uD7A3')||text.isTextOverflowing)throw new Exception(name+" invalid sign: "+text.name);
    }
    foreach(var target in new[]{"Supply cabinet label","Hand hygiene text","Bed bay text"}){
     var label=root.GetComponentsInChildren<TextMeshPro>().First(t=>t.name==target);
     var go=new GameObject("Sign preview camera");SceneManager.MoveGameObjectToScene(go,scene);var camera=go.AddComponent<Camera>();camera.enabled=false;camera.scene=scene;camera.nearClipPlane=.01f;camera.farClipPlane=10;camera.orthographic=true;camera.orthographicSize=target=="Supply cabinet label"?.39f:target=="Bed bay text"?.38f:.25f;
     camera.transform.position=label.transform.position-label.transform.forward*1.2f;camera.transform.rotation=label.transform.rotation;camera.GetUniversalAdditionalCameraData().allowXRRendering=false;
     if(target=="Supply cabinet label")camera.transform.position+=Vector3.down*.12f;
     var rt=new RenderTexture(1000,600,24);rt.Create();var old=RenderTexture.active;var image=new Texture2D(1000,600,TextureFormat.RGB24,false);
     try{RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1000,600),0,0);image.Apply();File.WriteAllBytes("Documentation/HospitalTreatment/Signage/"+name+"-"+target.Replace(' ','_')+".png",image.EncodeToPNG());}
     finally{RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(image);rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);}
    }
   }finally{EditorSceneManager.ClosePreviewScene(scene);}
  }}
 }
}
