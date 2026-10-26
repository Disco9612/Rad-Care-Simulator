using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace RadCare.HospitalTreatment.Editor {
 [InitializeOnLoad] public static class HospitalTapeInstaller {
  const string Folder="Assets/Scripts/Hospital_Treatment/Models/FixationTape";
  static HospitalTapeInstaller(){EditorApplication.update+=Poll;}
  static void Poll(){
   const string request="Temp/HospitalTape.request";
   if(EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists(request))return;
   File.Delete(request);try{Install();}catch(Exception e){File.WriteAllText("Temp/HospitalTape-error.txt",e.ToString());}
  }
  static Mesh Strip(bool fitted){
   var vertices=new System.Collections.Generic.List<Vector3>();var uv=new System.Collections.Generic.List<Vector2>();var triangles=new System.Collections.Generic.List<int>();
   for(int i=0;i<=28;i++){
    float x=-.07f+i*.005f;float edge=Mathf.Clamp01((.07f-Mathf.Abs(x))/.004f);float half=.009f+.0035f*edge;
    float y=fitted?-.007f*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.035f,.07f,Mathf.Abs(x))):.002f*Mathf.Sin(i*Mathf.PI/28);
    vertices.Add(new Vector3(x,y,-half));vertices.Add(new Vector3(x,y,half));uv.Add(new Vector2(i/28f,0));uv.Add(new Vector2(i/28f,1));
    if(i<28){int a=i*2;triangles.AddRange(new[]{a,a+1,a+2,a+1,a+3,a+2});}
   }
   var mesh=new Mesh{name=fitted?"Applied medical tape":"Precut medical tape"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
  }
  static T Save<T>(T asset,string name) where T:UnityEngine.Object {
   string path=Folder+"/"+name;var existing=AssetDatabase.LoadAssetAtPath<T>(path);if(existing!=null){UnityEngine.Object.DestroyImmediate(asset);return existing;}
   AssetDatabase.CreateAsset(asset,path);return asset;
  }
  static void Install(){
   var scene=SceneManager.GetActiveScene();if(EditorApplication.isPlaying||scene.path!="Assets/Scenes/Hospital_Tutorial.unity")throw new Exception("Hospital edit mode required");
   var s=UnityEngine.Object.FindAnyObjectByType<HospitalScenario>();
   if(s.targets.Any(t=>t.itemId=="fixation_tape"&&t.kind==HospitalInteraction.Carry)){var dressing=s.targets.First(t=>t.action==HospitalAction.ApplyDressing&&t.kind==HospitalInteraction.Socket);var target=s.targets.First(t=>t.action==HospitalAction.SecureDressing&&t.kind==HospitalInteraction.Socket);var previous=s.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Fixation_Tape");if(previous==null)throw new Exception("Original fitted tape reference missing");target.transform.SetPositionAndRotation(previous.position,previous.rotation);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);File.WriteAllText("Temp/HospitalTape-installed.txt","Repaired tape socket to dressing surface at "+target.transform.position);return;}
   Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
   var flat=Save(Strip(false),"TapeFlat.asset");var fitted=Save(Strip(true),"TapeApplied.asset");
   var texture=new Texture2D(128,32,TextureFormat.RGB24,false);texture.name="Medical tape woven fabric";
   for(int y=0;y<32;y++)for(int x=0;x<128;x++){float shade=(x%3==0||y%3==0)?.86f:1;texture.SetPixel(x,y,new Color(.94f*shade,.91f*shade,.82f*shade));}texture.Apply();
   var tex=Save(texture,"TapeWeave.asset");var mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Medical tape ivory fabric"};mat.SetTexture("_BaseMap",tex);mat.SetFloat("_Smoothness",.12f);mat.SetFloat("_Cull",0);mat.EnableKeyword("_EMISSION");mat=Save(mat,"TapeFabric.mat");
   var fresh=s.targets.First(t=>t.action==HospitalAction.ApplyDressing&&t.kind==HospitalInteraction.Carry);
   var dressingSocket=s.targets.First(t=>t.action==HospitalAction.ApplyDressing&&t.kind==HospitalInteraction.Socket);
   var socket=s.targets.First(t=>t.action==HospitalAction.SecureDressing);socket.kind=HospitalInteraction.Socket;socket.itemId="fixation_tape";socket.label="테이프를 드레싱 위에 붙이세요";
   socket.transform.SetPositionAndRotation(dressingSocket.transform.position,dressingSocket.transform.rotation);
   float top=.01f;
   var dressingShape=fresh.movable.GetComponent<HospitalPlacementShape>();
   foreach(var filter in fresh.movable.GetComponentsInChildren<MeshFilter>()){
    var mesh=filter.sharedMesh; if(dressingShape!=null){int index=Array.IndexOf(dressingShape.parts,filter);if(index>=0)mesh=dressingShape.fittedMeshes[index];}
    var matrix=Matrix4x4.Scale(fresh.movable.lossyScale)*fresh.movable.worldToLocalMatrix*filter.transform.localToWorldMatrix;
    if(mesh!=null)foreach(var v in mesh.vertices)top=Mathf.Max(top,matrix.MultiplyPoint3x4(v).y);
   }
   var originalTape=s.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Fixation_Tape");if(originalTape==null)throw new Exception("Original fitted tape reference missing");socket.transform.SetPositionAndRotation(originalTape.position,originalTape.rotation);
   foreach(var canvas in socket.GetComponentsInChildren<Canvas>(true))UnityEngine.Object.DestroyImmediate(canvas.gameObject);
   if(s.tapeVisual!=null)s.tapeVisual.SetActive(false);
   var root=new GameObject("Fixation_Tape_Grab");root.transform.SetParent(s.transform,true);
   var carry=root.AddComponent<HospitalInteractable>();carry.action=HospitalAction.SecureDressing;carry.kind=HospitalInteraction.Carry;carry.itemId="fixation_tape";carry.label="고정 테이프 집기";
   var model=new GameObject("Precut_Fixation_Tape");model.transform.SetParent(root.transform,false);model.transform.position=fresh.movable.position+new Vector3(0,.012f,.16f);
   var mf=model.AddComponent<MeshFilter>();mf.sharedMesh=flat;var mr=model.AddComponent<MeshRenderer>();mr.sharedMaterial=mat;
   var collider=model.AddComponent<BoxCollider>();collider.center=new Vector3(0,.001f,0);collider.size=new Vector3(.14f,.008f,.025f);collider.isTrigger=true;
   model.AddComponent<HospitalGrabSurface>().target=carry;carry.grabCollider=collider;carry.movable=model.transform;carry.gripLocalPosition=new Vector3(-.05f,0,0);
   var shape=model.AddComponent<HospitalPlacementShape>();shape.parts=new[]{mf};shape.fittedMeshes=new[]{fitted};
   s.tapeVisual=model;s.targets=s.targets.Concat(new[]{carry}).ToArray();
   var glow=s.GetComponent<HospitalObjectGlow>();glow.bindings=glow.bindings.Where(b=>b.target!=socket).Concat(new[]{new HospitalObjectGlow.Binding{target=carry,renderers=new Renderer[]{mr}},new HospitalObjectGlow.Binding{target=socket,renderers=fresh.movable.GetComponentsInChildren<Renderer>()}}).ToArray();
   EditorUtility.SetDirty(s);EditorUtility.SetDirty(glow);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
   File.WriteAllText("Temp/HospitalTape-installed.txt",$"Physical tape installed. Source {model.transform.position}; socket {socket.transform.position}; dressing surface {top}; proximity 0.045m, dwell 0.25s. Targets {s.targets.Length}.");
  }
 }
}
