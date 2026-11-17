using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
public static partial class HospitalPatientTransfer {
 static void RenderPoseOnBed(bool top=false,bool woundFocus=false){
  var s=SceneManager.GetActiveScene();var patient=All<Transform>(s).Single(t=>t.name=="Hospital_Patient_Visual");var p=VisibleBounds(patient);
  var bed=All<Transform>(s).Where(t=>t.name.StartsWith("SM_MedicalBed_01a")&&t.GetComponentsInChildren<Renderer>().Length>0)
   .OrderBy(t=>Vector2.Distance(new Vector2(VisibleBounds(t).center.x,VisibleBounds(t).center.z),new Vector2(p.center.x,p.center.z))).First();
  var rs=patient.GetComponentsInChildren<Renderer>().Concat(patient.parent.Find("Blood").GetComponentsInChildren<Renderer>()).Concat(bed.GetComponentsInChildren<Renderer>()).Distinct().ToArray();
  var oldLayers=rs.Select(r=>(obj:r.gameObject,layer:r.gameObject.layer)).ToArray();
  var cameraObject=new GameObject("Bed pose verification camera");cameraObject.hideFlags=HideFlags.HideAndDontSave;SceneManager.MoveGameObjectToScene(cameraObject,s);
  var c=cameraObject.AddComponent<Camera>();c.enabled=false;c.scene=s;c.transform.position=p.center+(top?new Vector3(0f,2f,0f):new Vector3(1.6f,.42f,0f));c.transform.LookAt(p.center,top?Vector3.back:Vector3.up);
  c.orthographic=true;c.orthographicSize=top?1.08f:.95f;c.nearClipPlane=.05f;c.farClipPlane=8f;c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=new Color(.15f,.17f,.19f);c.cullingMask=1<<30;c.GetUniversalAdditionalCameraData().allowXRRendering=false;
  if(woundFocus){var b=patient.parent.Find("Blood").GetComponent<BoxCollider>().bounds;c.transform.position=b.center+new Vector3(.07f,.6f,.03f);c.transform.LookAt(b.center,Vector3.back);c.orthographicSize=.17f;}
  var rt=new RenderTexture(1500,1100,24);rt.Create();var previous=RenderTexture.active;var tex=new Texture2D(1500,1100,TextureFormat.RGB24,false);
  try{foreach(var x in oldLayers)x.obj.layer=30;RenderPipeline.SubmitRenderRequest(c,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1500,1100),0,0);tex.Apply();File.WriteAllBytes(woundFocus?"Documentation/HospitalPatientTransfer/Wound-closeup.png":top?"Documentation/HospitalPatientTransfer/Patient-on-bed-top.png":"Documentation/HospitalPatientTransfer/Patient-on-bed-side.png",tex.EncodeToPNG());}
  finally{foreach(var x in oldLayers)x.obj.layer=x.layer;RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(tex);rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(cameraObject);}
  var skin=patient.GetComponentsInChildren<MeshFilter>(true).First(f=>f.name=="radcare_patient_body");var hand=skin.sharedMesh.vertices.Select(v=>skin.transform.TransformPoint(v)).Where(v=>v.x>patient.position.x+.17f).ToArray();var mf=bed.GetComponentsInChildren<MeshFilter>().Where(f=>f.sharedMesh).OrderByDescending(f=>f.sharedMesh.vertexCount).First();var temp=new GameObject("Bed support probe");temp.hideFlags=HideFlags.HideAndDontSave;temp.transform.SetPositionAndRotation(mf.transform.position,mf.transform.rotation);temp.transform.localScale=mf.transform.lossyScale;var bc=temp.AddComponent<MeshCollider>();bc.sharedMesh=mf.sharedMesh;Physics.SyncTransforms();float mattress=float.NaN;if(hand.Length>0&&bc.Raycast(new Ray(new Vector3(p.center.x,2f,hand.Average(v=>v.z)),Vector3.down),out var hit,3))mattress=hit.point.y;File.WriteAllText(Out+"/arm-contact.txt","right hand vertices="+hand.Length+" minY="+(hand.Length>0?hand.Min(v=>v.y):0)+" maxY="+(hand.Length>0?hand.Max(v=>v.y):0)+" mattressY="+mattress+" bed="+bed.name);UnityEngine.Object.DestroyImmediate(temp);File.WriteAllText(Out+"/status.txt","BED RENDERED");
 }
}


