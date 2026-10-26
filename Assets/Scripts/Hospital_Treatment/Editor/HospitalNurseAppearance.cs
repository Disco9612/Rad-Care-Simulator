using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace RadCare.HospitalTreatment.Editor {
 [InitializeOnLoad] public static class HospitalNurseAppearance {
  static HospitalNurseAppearance(){EditorApplication.update+=Poll;}
  static Transform Nurse()=>SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).First(t=>t.name=="Assistant_Nurse");
  static void Poll(){if(EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists("Temp/HospitalNurse.request"))return;
   var command=File.ReadAllText("Temp/HospitalNurse.request").Trim();File.Delete("Temp/HospitalNurse.request");
   try{if(EditorApplication.isPlaying)throw new Exception("Edit mode required");if(command=="audit")Audit();else if(command=="scrubs")Scrubs();else if(command=="fit")Fit();else Install();}catch(Exception e){File.WriteAllText("Temp/HospitalNurse-error.txt",e.ToString());}
  }
  static void Audit(){var n=Nurse();var text=new System.Text.StringBuilder();foreach(var r in n.GetComponentsInChildren<MeshRenderer>()){var b=HospitalGrabCalibration.LocalBounds(r.transform,n);text.AppendLine(r.name+" center="+b.center.ToString("F4")+" size="+b.size.ToString("F4"));}File.WriteAllText("Temp/HospitalNurse-audit.txt",text.ToString());Capture(false);Capture(true);}
  const string Folder="Assets/Scripts/Hospital_Treatment/Materials/Nurse";
  static void Scrubs(){
   var scene=SceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/Hospital_Tutorial.unity")throw new Exception("Hospital scene required");
   EditorSceneManager.SaveScene(scene,"Temp/Hospital-before-scrubs.unity",true);
   const string folder="Assets/Scripts/Hospital_Treatment/Models/NurseScrubs";
   AssetDatabase.ImportAsset(folder+"/nurse_scrubs.fbx",ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
   var importer=(ModelImporter)AssetImporter.GetAtPath(folder+"/nurse_scrubs.fbx");importer.isReadable=true;importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.SaveAndReimport();
   var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(folder+"/nurse_scrubs.fbx");var source=UnityEngine.Object.Instantiate(prefab);var n=Nurse();
   try{foreach(var suffix in new[]{"_body","_outfit"}){
    var src=source.GetComponentsInChildren<MeshFilter>().First(m=>m.name.EndsWith(suffix));var dst=n.GetComponentsInChildren<MeshFilter>().First(m=>m.name.EndsWith(suffix));
    var matrix=dst.transform.worldToLocalMatrix*n.localToWorldMatrix*source.transform.worldToLocalMatrix*src.transform.localToWorldMatrix;
    var mesh=UnityEngine.Object.Instantiate(src.sharedMesh);mesh.name="ClinicalScrubsBalanced"+suffix;var vertices=mesh.vertices;var normals=mesh.normals;
    for(int i=0;i<vertices.Length;i++){vertices[i]=matrix.MultiplyPoint3x4(vertices[i]);if(normals.Length==vertices.Length)normals[i]=matrix.inverse.transpose.MultiplyVector(normals[i]).normalized;}
    mesh.vertices=vertices;if(normals.Length==vertices.Length)mesh.normals=normals;mesh.RecalculateBounds();mesh.RecalculateTangents();
    string path=folder+"/"+mesh.name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(existing){EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;}else AssetDatabase.CreateAsset(mesh,path);
    dst.sharedMesh=mesh;
    if(suffix=="_outfit"){var material=Material("Nurse_Scrub_Teal",new Color(.025f,.29f,.21f));dst.GetComponent<MeshRenderer>().sharedMaterials=Enumerable.Repeat(material,mesh.subMeshCount).ToArray();EditorUtility.SetDirty(material);}
   }}finally{UnityEngine.Object.DestroyImmediate(source);}
   var outfit=n.GetComponentsInChildren<MeshFilter>().First(m=>m.name.EndsWith("_outfit"));
   var probe=outfit.gameObject.AddComponent<MeshCollider>();probe.sharedMesh=outfit.sharedMesh;
   if(probe.Raycast(new Ray(n.TransformPoint(new Vector3(-.085f,1.29f,1)),-n.forward),out var hit,2)){
    float z=n.InverseTransformPoint(hit.point).z+.006f;
    foreach(string name in new[]{"Nurse_ID_Card","ID_Clip","Nurse_Badge_Label"}){var t=n.Find("Clinical_Uniform_Details/"+name);var p=t.localPosition;p.z=z+(name=="Nurse_Badge_Label"?.002f:0);t.localPosition=p;}
   }
   UnityEngine.Object.DestroyImmediate(probe);
   AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Audit();
   File.WriteAllText("Temp/HospitalNurse-scrubs.txt","Short sleeves, bound V neckline and relaxed shirt/trouser geometry installed. Existing renderer identities, nurse transform, mask, badge and assistant interaction references retained.");
  }
  static Material Material(string name,Color color){var path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.15f);m.SetFloat("_Cull",0);m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",Color.white*.001f);return m;}
  static GameObject Cube(Transform root,string name,Vector3 position,Vector3 size,Material mat){var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(root,false);g.transform.localPosition=position;g.transform.localScale=size;g.GetComponent<MeshRenderer>().sharedMaterial=mat;UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());return g;}
  static void Line(Transform root,string name,Vector3[] points,float width,Material mat){var g=new GameObject(name);g.transform.SetParent(root,false);var l=g.AddComponent<LineRenderer>();l.useWorldSpace=false;l.positionCount=points.Length;l.SetPositions(points);l.widthMultiplier=width;l.numCornerVertices=4;l.numCapVertices=4;l.sharedMaterial=mat;}
  static void Install(){
   var scene=SceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/Hospital_Tutorial.unity")throw new Exception("Hospital scene required");
   EditorSceneManager.SaveScene(scene,"Temp/Hospital-before-nurse-uniform.unity",true);
   var n=Nurse();if(n.Find("Clinical_Uniform_Details")!=null)throw new Exception("Uniform details already installed");
   Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
   var uniform=Material("Nurse_Scrub_Blue",new Color(.27f,.60f,.78f));
   var white=Material("Medical_White",new Color(.91f,.95f,.96f));var mask=Material("Mask_Nonwoven_Blue",new Color(.43f,.77f,.86f));
   var outfit=n.GetComponentsInChildren<MeshRenderer>().First(r=>r.name.EndsWith("_outfit"));outfit.sharedMaterial=uniform;
   n.GetComponentsInChildren<MeshRenderer>().First(r=>r.name.EndsWith("_shoes")).sharedMaterial=white;
   var root=new GameObject("Clinical_Uniform_Details").transform;root.SetParent(n,false);
   var verts=new List<Vector3>();var uv=new List<Vector2>();var tris=new List<int>();int cols=32,rows=18;
   for(int y=0;y<=rows;y++)for(int x=0;x<=cols;x++){float u=x/(float)cols*2-1,v=y/(float)rows;
    float py=Mathf.Lerp(1.444f,1.538f,v)+u*u*(.008f-.018f*v);
    float pz=.173f-.052f*u*u+.0018f*Mathf.Sin(v*Mathf.PI*6)*(1-u*u);
    verts.Add(new Vector3(u*.074f,py,pz));uv.Add(new Vector2(x/(float)cols,v));
    if(x<cols&&y<rows){int a=y*(cols+1)+x;tris.AddRange(new[]{a,a+1,a+cols+1,a+1,a+cols+2,a+cols+1});}
   }
   var mesh=new Mesh{name="Pleated surgical mask"};mesh.SetVertices(verts);mesh.SetUVs(0,uv);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
   AssetDatabase.CreateAsset(mesh,Folder+"/SurgicalMask.asset");var cloth=new GameObject("Surgical_Mask");cloth.transform.SetParent(root,false);cloth.AddComponent<MeshFilter>().sharedMesh=mesh;cloth.AddComponent<MeshRenderer>().sharedMaterial=mask;
   foreach(int sign in new[]{-1,1}){
    var points=new List<Vector3>();for(int i=0;i<=24;i++){float a=i*Mathf.PI*2/24;points.Add(new Vector3(sign*(.077f+.010f*Mathf.Sin(a)),1.493f+.031f*Mathf.Cos(a),.082f+.039f*Mathf.Sin(a)));}
    Line(root,"Mask_Ear_Loop_"+sign,points.ToArray(),.002f,white);
   }
   Line(root,"Mask_Nose_Strip",new[]{new Vector3(-.032f,1.535f,.166f),new Vector3(0,1.539f,.175f),new Vector3(.032f,1.535f,.166f)},.003f,white);
   for(int j=1;j<=3;j++){var points=new List<Vector3>();for(int i=0;i<=24;i++){float u=i/12f-1;points.Add(new Vector3(u*.073f,1.451f+j*.019f,.175f-.052f*u*u));}Line(root,"Mask_Pleat_"+j,points.ToArray(),.001f,mask);}
   var probe=outfit.gameObject.AddComponent<MeshCollider>();probe.sharedMesh=outfit.GetComponent<MeshFilter>().sharedMesh;
   float z=.18f;if(probe.Raycast(new Ray(n.TransformPoint(new Vector3(-.085f,1.29f,1)), -n.forward),out var hit,2))z=n.InverseTransformPoint(hit.point).z+.008f;
   UnityEngine.Object.DestroyImmediate(probe);
   Cube(root,"Nurse_ID_Card",new Vector3(-.085f,1.29f,z),new Vector3(.070f,.045f,.003f),white);
   Cube(root,"ID_Clip",new Vector3(-.085f,1.318f,z),new Vector3(.012f,.015f,.004f),uniform);
   var canvasGo=new GameObject("Nurse_Badge_Label",typeof(RectTransform),typeof(Canvas));canvasGo.transform.SetParent(root,false);canvasGo.transform.localPosition=new Vector3(-.085f,1.29f,z+.002f);canvasGo.transform.localRotation=Quaternion.Euler(0,180,0);canvasGo.transform.localScale=Vector3.one*.0003f;canvasGo.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;((RectTransform)canvasGo.transform).sizeDelta=new Vector2(210,130);
   var labelGo=new GameObject("Text",typeof(RectTransform));labelGo.transform.SetParent(canvasGo.transform,false);var label=labelGo.AddComponent<TextMeshProUGUI>();label.font=UnityEngine.Object.FindAnyObjectByType<HospitalScenario>().chartText.font;label.text="간호사\nNURSE";label.fontSize=40;label.color=new Color(.02f,.18f,.3f);label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;label.rectTransform.sizeDelta=new Vector2(210,130);
   EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Audit();
   File.WriteAllText("Temp/HospitalNurse-verified.txt","Hospital uniform, ID badge, pleated face mask, nose strip and ear loops saved; non-interactive details; original assistant targets preserved.");
  }
  static void Fit(){
   var n=Nurse();foreach(int sign in new[]{-1,1}){
    var line=n.Find("Clinical_Uniform_Details/Mask_Ear_Loop_"+sign).GetComponent<LineRenderer>();
    var points=new[]{new Vector3(sign*.074f,1.523f,.121f),new Vector3(sign*.083f,1.544f,.047f),new Vector3(sign*.088f,1.527f,.018f),new Vector3(sign*.084f,1.495f,.035f),new Vector3(sign*.074f,1.462f,.121f)};
    line.positionCount=points.Length;line.SetPositions(points);
   }
   var scene=SceneManager.GetActiveScene();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Audit();
  }
  static void Capture(bool close){var n=Nurse();var g=new GameObject("Nurse preview camera");var c=g.AddComponent<Camera>();c.enabled=false;c.fieldOfView=close?28:42;c.nearClipPlane=.01f;c.farClipPlane=30;g.AddComponent<UniversalAdditionalCameraData>().allowXRRendering=false;
   var layers=n.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t,t=>t.gameObject.layer);foreach(var t in layers.Keys)t.gameObject.layer=31;c.cullingMask=1<<31;c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=new Color(.22f,.26f,.28f);
   var target=n.TransformPoint(new Vector3(0,close?1.6f:.87f,0));g.transform.position=target+n.forward*(close?1.1f:2.7f);g.transform.LookAt(target);
   var rt=new RenderTexture(900,1100,24);rt.Create();var prev=RenderTexture.active;
   try{RenderPipeline.SubmitRenderRequest(c,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;var tex=new Texture2D(900,1100,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,900,1100),0,0);tex.Apply();File.WriteAllBytes(close?"Temp/HospitalNurse-face.png":"Temp/HospitalNurse-full.png",tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);}finally{foreach(var pair in layers)pair.Key.gameObject.layer=pair.Value;RenderTexture.active=prev;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(g);}
  }
 }
}


