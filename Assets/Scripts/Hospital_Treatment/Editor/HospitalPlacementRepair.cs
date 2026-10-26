using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace RadCare.HospitalTreatment.Editor {
[InitializeOnLoad] public static class HospitalPlacementRepair {
 static HospitalPlacementRepair(){EditorApplication.update+=Poll;}
 static void Poll(){const string p="Temp/HospitalPlacement.request";if(EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists(p))return;var cmd=File.ReadAllText(p).Trim();File.Delete(p);try{if(EditorApplication.isPlaying)throw new Exception("Edit mode required");if(cmd=="validate"){Validate();return;}if(cmd=="fix")Fix();if(cmd=="refine")Refine();Audit();Render();if(cmd=="fix"||cmd=="refine"||cmd=="preview")RenderPlaced();}catch(Exception e){File.WriteAllText("Temp/HospitalPlacement-error.txt",e.ToString());}}
 static Transform Find(string name)=>SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<Transform>(true)).First(t=>t.name==name);
 static void Audit(){var text=new System.Text.StringBuilder();foreach(var n in new[]{"Patient_Supine","Patient_Bed","Clinical_Waste_Container","Irrigation_Cart","Clean_Dressing_Cart","Irrigation_Receiver"}){text.AppendLine(n);foreach(var r in Find(n).GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.enabled))text.AppendLine(r.name+" min="+r.bounds.min.ToString("F4")+" max="+r.bounds.max.ToString("F4"));}foreach(var f in Find("Patient_Supine").GetComponentsInChildren<MeshFilter>()) {
 var go=new GameObject("probe");go.hideFlags=HideFlags.HideAndDontSave;go.transform.SetPositionAndRotation(f.transform.position,f.transform.rotation);go.transform.localScale=f.transform.lossyScale;var mc=go.AddComponent<MeshCollider>();mc.sharedMesh=f.sharedMesh;Physics.SyncTransforms();
 for(float z=-.65f;z<-.19f;z+=.05f)for(float x=-.09f;x<.03f;x+=.04f)if(mc.Raycast(new Ray(new Vector3(x,2,z),Vector3.down),out var h,2))text.AppendLine("PROBE "+f.name+" "+h.point.ToString("F3")+" normal="+h.normal.ToString("F2"));UnityEngine.Object.DestroyImmediate(go);
 }File.WriteAllText("Temp/HospitalPlacement-audit.txt",text.ToString());}

 const string MeshFolder="Assets/Scripts/Hospital_Treatment/PlacementMeshes";
 static System.Collections.Generic.List<MeshCollider> surfaces;
 static Bounds BoundsOf(Transform t){var rs=t.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.enabled).ToArray();var b=rs[0].bounds;foreach(var r in rs.Skip(1))b.Encapsulate(r.bounds);return b;}
 static void CreateSurfaces(){surfaces=new System.Collections.Generic.List<MeshCollider>();foreach(var f in Find("Patient_Supine").GetComponentsInChildren<MeshFilter>()){var g=new GameObject("Temporary surface");g.hideFlags=HideFlags.HideAndDontSave;g.transform.SetPositionAndRotation(f.transform.position,f.transform.rotation);g.transform.localScale=f.transform.lossyScale;var c=g.AddComponent<MeshCollider>();c.sharedMesh=f.sharedMesh;surfaces.Add(c);}Physics.SyncTransforms();}
 static float Surface(float x,float z){float h=.7945f;foreach(var c in surfaces)if(c.Raycast(new Ray(new Vector3(x,2,z),Vector3.down),out var hit,2))h=Mathf.Max(h,hit.point.y);return h;}
 static System.Collections.Generic.Dictionary<Vector2Int,float> heightCache=new System.Collections.Generic.Dictionary<Vector2Int,float>();
 static float GridHeight(int ix,int iz,bool cloth){var key=new Vector2Int(ix+(cloth?10000:0),iz);if(heightCache.TryGetValue(key,out var v))return v;float x=ix*.005f,z=iz*.005f,h=Surface(x,z);int radius=cloth?5:2;float step=cloth?.012f:.008f;float slope=cloth?1.2f:1.8f;for(int i=-radius;i<=radius;i++)for(int j=-radius;j<=radius;j++){float d=Mathf.Sqrt(i*i+j*j)*step;if(d>radius*step)continue;h=Mathf.Max(h,Surface(x+i*step,z+j*step)-d*slope);}heightCache[key]=h;return h;}
 static float SmoothSurface(float x,float z,bool cloth){float gx=x*200,gz=z*200;int ix=Mathf.FloorToInt(gx),iz=Mathf.FloorToInt(gz);return Mathf.Lerp(Mathf.Lerp(GridHeight(ix,iz,cloth),GridHeight(ix+1,iz,cloth),gx-ix),Mathf.Lerp(GridHeight(ix,iz+1,cloth),GridHeight(ix+1,iz+1,cloth),gx-ix),gz-iz);}
 static void Refine(){foreach(var name in new[]{"radcare_wound_pad","radcare_used_dressing","radcare_sterile_dressing","radcare_waterproof_drape","radcare_fixation_tape"}){var importer=(ModelImporter)AssetImporter.GetAtPath("Assets/RadCareTreatmentDetails/"+name+"/asset.fbx");if(!importer.isReadable){importer.isReadable=true;importer.SaveAndReimport();}}var s=UnityEngine.Object.FindAnyObjectByType<HospitalScenario>();CreateSurfaces();heightCache.Clear();try{foreach(var pair in new[]{new[]{"Simulated_Wound_Target","radcare_wound_pad"},new[]{"Existing_Dressing","radcare_used_dressing"},new[]{"Fixation_Tape","radcare_fixation_tape"},new[]{"Waterproof_Drape","radcare_waterproof_drape"},new[]{"Fresh_Dressing","radcare_sterile_dressing"}}){var original=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/RadCareTreatmentDetails/"+pair[1]+"/"+pair[1]+".prefab");foreach(var f in Find(pair[0]).GetComponentsInChildren<MeshFilter>(true)){var source=original.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(x=>x.name==f.name);if(source!=null)f.sharedMesh=source.sharedMesh;}}
 var wp=s.wound.position;Fit(s.wound,wp,.003f,false);Fit(s.oldDressing.transform,s.oldDressing.transform.position,.010f,false);Fit(Find("Waterproof_Drape"),wp,.004f,true);Fit(s.newDressing.transform,Target(s,HospitalAction.ApplyDressing,HospitalInteraction.Socket).transform.position,.010f,true);Fit(s.tapeVisual.transform,s.tapeVisual.transform.position,.019f,false);
 foreach(var t in s.targets.Where(t=>t.IsMovable&&t.grabCollider is BoxCollider)){var c=(BoxCollider)t.grabCollider;var b=HospitalGrabCalibration.LocalBounds(t.movable,c.transform);c.center=b.center;c.size=Vector3.Max(b.size+Vector3.one*.004f,Vector3.one*.016f);}
 File.WriteAllText("Temp/HospitalPlacement-surface.txt","At center: "+Surface(-.068f,-.405f)+" smoothed: "+SmoothSurface(-.068f,-.405f,true)+" left: "+SmoothSurface(-.2f,-.405f,true));EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());AssetDatabase.SaveAssets();}finally{foreach(var c in surfaces)UnityEngine.Object.DestroyImmediate(c.gameObject);}}
 static void Subdivide(Mesh mesh){
 var v=mesh.vertices.ToList();var uv=mesh.uv.ToList();var triangles=new System.Collections.Generic.List<int>();var edges=new System.Collections.Generic.Dictionary<long,int>();
 int Mid(int a,int b){long key=((long)Mathf.Min(a,b)<<32)|(uint)Mathf.Max(a,b);if(edges.TryGetValue(key,out var n))return n;n=v.Count;v.Add((v[a]+v[b])*.5f);uv.Add((uv[a]+uv[b])*.5f);edges[key]=n;return n;}
 var ts=mesh.triangles;for(int i=0;i<ts.Length;i+=3){int a=ts[i],b=ts[i+1],c=ts[i+2],ab=Mid(a,b),bc=Mid(b,c),ca=Mid(c,a);triangles.AddRange(new[]{a,ab,ca,ab,b,bc,ca,bc,c,ab,bc,ca});}mesh.Clear();mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);
 }
 static void Fit(Transform t,Vector3 position,float clearance,bool onPlacement){
  var home=t.position;var rotation=t.rotation;t.SetPositionAndRotation(position,Quaternion.identity);
  var parts=t.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.GetComponent<MeshRenderer>()!=null&&f.GetComponent<MeshRenderer>().enabled).ToArray();
  float bottom=BoundsOf(t).min.y;var fitted=new Mesh[parts.Length];
  for(int j=0;j<parts.Length;j++) {var f=parts[j];var source=f.sharedMesh;if(!source.isReadable)throw new Exception("Mesh must be readable: "+f.name);var mesh=new Mesh{indexFormat=UnityEngine.Rendering.IndexFormat.UInt32,vertices=source.vertices,uv=source.uv,triangles=source.triangles};mesh.name=t.name+"_Fitted_"+j;if(!f.name.EndsWith("woven_fibers")&&t.name!="Fixation_Tape"){Subdivide(mesh);if(t.name=="Waterproof_Drape"||t.name=="Simulated_Wound_Target")Subdivide(mesh);}var vs=mesh.vertices;
   for(int i=0;i<vs.Length;i++){var w=f.transform.TransformPoint(vs[i]);float originalHeight=w.y-bottom;float support=SmoothSurface(w.x,w.z,t.name=="Waterproof_Drape");w.y=support+clearance+originalHeight;vs[i]=f.transform.InverseTransformPoint(w);}mesh.vertices=vs;mesh.RecalculateNormals();mesh.RecalculateBounds();mesh.RecalculateTangents();if(mesh.vertexCount==0||mesh.triangles.Length==0)throw new Exception("Empty fitted mesh "+f.name);
   string path=MeshFolder+"/"+mesh.name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(existing!=null){existing.Clear();existing.indexFormat=mesh.indexFormat;existing.vertices=mesh.vertices;existing.uv=mesh.uv;existing.triangles=mesh.triangles;existing.normals=mesh.normals;existing.tangents=mesh.tangents;existing.bounds=mesh.bounds;EditorUtility.SetDirty(existing);UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;}else AssetDatabase.CreateAsset(mesh,path);fitted[j]=mesh;if(!onPlacement)f.sharedMesh=mesh;
  }
  if(onPlacement){var shape=t.GetComponent<HospitalPlacementShape>();if(shape==null)shape=t.gameObject.AddComponent<HospitalPlacementShape>();shape.parts=parts;shape.fittedMeshes=fitted;t.SetPositionAndRotation(home,rotation);EditorUtility.SetDirty(shape);}
 }
 static HospitalInteractable Target(HospitalScenario s,HospitalAction a,HospitalInteraction k)=>s.targets.Single(t=>t.action==a&&t.kind==k);
 static void MoveCarry(HospitalScenario s,string name,Vector3 p){var t=Find(name);var delta=p-t.position;t.position=p;var marker=s.targets.FirstOrDefault(x=>x.movable==t&&x.IsMovable);if(marker!=null)marker.transform.position+=delta;}
 static void Solid(string name,Transform root,Vector3 p,Vector3 size,Material mat){var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(root,false);g.transform.position=p;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=mat;UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());}
 static void Fix(){
  var scene=SceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/Hospital_Tutorial.unity")throw new Exception("Hospital scene required");
  if(Directory.Exists(MeshFolder))throw new Exception("Placement repair already applied; inspect instead of applying twice");
  EditorSceneManager.SaveScene(scene,"Temp/Hospital-before-placement-repair.unity",true);Directory.CreateDirectory(MeshFolder);AssetDatabase.Refresh();
  var s=UnityEngine.Object.FindAnyObjectByType<HospitalScenario>();CreateSurfaces();
  try {
   var wp=new Vector3(-.068f,Surface(-.068f,-.405f)+.004f,-.405f);s.wound.position=wp;Fit(s.wound,wp,.003f,false);
   MoveCarry(s,"Existing_Dressing",wp+Vector3.up*.012f);Fit(s.oldDressing.transform,s.oldDressing.transform.position,.010f,false);
   Target(s,HospitalAction.RemoveDressing,HospitalInteraction.Carry).transform.position=wp+Vector3.up*.12f;
   Target(s,HospitalAction.PlaceDrape,HospitalInteraction.Socket).transform.position=wp;
   Target(s,HospitalAction.ApplyDressing,HospitalInteraction.Socket).transform.position=wp+Vector3.up*.012f;
   Fit(Find("Waterproof_Drape"),wp,.004f,true);Fit(s.newDressing.transform,wp+Vector3.up*.012f,.010f,true);
   Fit(s.tapeVisual.transform,wp+Vector3.up*.02f,.019f,false);
   Target(s,HospitalAction.MeasureWound,HospitalInteraction.Action).transform.position=wp+new Vector3(-.12f,.17f,0);
   Target(s,HospitalAction.MeasureSkin,HospitalInteraction.Action).transform.position=wp+new Vector3(-.12f,.17f,.15f);
   Target(s,HospitalAction.MeasureReference,HospitalInteraction.Action).transform.position=wp+new Vector3(-.12f,.17f,.30f);
   // Place supplies on the shelf; the decorative duplicate kit moves to the lower shelf.
   var kit=Find("Dressing_Kit");kit.position+=Vector3.down*.49f;
   MoveCarry(s,"Waterproof_Drape",new Vector3(-1.32f,.768f,.72f));
   MoveCarry(s,"Fresh_Dressing",new Vector3(-1.07f,.765f,.74f));
   // Used swabs stay beside the basin, not underneath it.
   MoveCarry(s,"Used_Consumables",new Vector3(-1.43f,.773f,-.88f));
   var receiverSocket=Target(s,HospitalAction.PlaceReceiver,HospitalInteraction.Socket);receiverSocket.transform.position=new Vector3(-.50f,.822f,-.405f);receiverSocket.transform.rotation=Quaternion.Euler(0,90,0);
   var support=new GameObject("Bedside_Receiver_Support");support.transform.SetParent(Find("Hospital_Treatment_Scenario"),false);
   var mat=Find("Patient_Bed").GetComponentsInChildren<MeshRenderer>().First(r=>r.name.Contains("rail_bar")).sharedMaterial;
   Solid("Receiver_Support_Tray",support.transform,new Vector3(-.50f,.811f,-.405f),new Vector3(.29f,.016f,.40f),mat);
   Solid("Receiver_Support_Bracket_A",support.transform,new Vector3(-.403f,.78f,-.55f),new Vector3(.21f,.025f,.025f),mat);
   Solid("Receiver_Support_Bracket_B",support.transform,new Vector3(-.403f,.78f,-.26f),new Vector3(.21f,.025f,.025f),mat);
   // Lower the working-side rail so it does not cross the tray or obstruct the patient's arm.
   foreach(var r in Find("Patient_Bed").GetComponentsInChildren<MeshRenderer>())if(r.name.Contains("rail_")&&r.bounds.center.x<0){r.transform.position+=Vector3.down*.28f;}
   var bin=Find("Clinical_Waste_Container");var rim=bin.GetComponentsInChildren<MeshRenderer>().Single(r=>r.name.EndsWith("rolled_open_rim")).bounds;
   var socket=Target(s,HospitalAction.DisposeDressing,HospitalInteraction.Socket);socket.transform.position=new Vector3(rim.center.x,rim.max.y+.04f,rim.center.z);socket.transform.SetParent(bin,true);
   Physics.SyncTransforms();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
   File.WriteAllText("Temp/HospitalPlacement-fixed.txt","Preserved waste-bin model world pose; disposal target follows actual rim. Fitted wound/drape/dressings to patient surface; supplies on cart; basin supported; working-side rail lowered.\nWound="+wp+"\nDisposal="+socket.transform.position);
  }finally{foreach(var c in surfaces)UnityEngine.Object.DestroyImmediate(c.gameObject);}
 }
 static void Validate(){
 var s=UnityEngine.Object.FindAnyObjectByType<HospitalScenario>();int count=0,triangles=0;
 foreach(var guid in AssetDatabase.FindAssets("t:Mesh",new[]{MeshFolder})){var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(AssetDatabase.GUIDToAssetPath(guid));if(mesh.vertexCount==0||mesh.triangles.Length==0||mesh.vertices.Any(v=>float.IsNaN(v.x)||float.IsInfinity(v.x)||float.IsNaN(v.y)||float.IsInfinity(v.y)||float.IsNaN(v.z)||float.IsInfinity(v.z)))throw new Exception("Invalid fitted mesh: "+mesh.name);count++;triangles+=mesh.triangles.Length/3;}
 var bin=Find("Clinical_Waste_Container");var rim=bin.GetComponentsInChildren<MeshRenderer>().Single(r=>r.name.EndsWith("rolled_open_rim")).bounds;var disposal=Target(s,HospitalAction.DisposeDressing,HospitalInteraction.Socket).transform;
 if(disposal.parent!=bin||Mathf.Abs(disposal.position.x-rim.center.x)>.001f||Mathf.Abs(disposal.position.z-rim.center.z)>.001f)throw new Exception("Disposal target does not follow moved bin");
 foreach(var shape in UnityEngine.Object.FindObjectsByType<HospitalPlacementShape>(FindObjectsSortMode.None))if(shape.parts.Length!=shape.fittedMeshes.Length||shape.fittedMeshes.Any(m=>m==null))throw new Exception("Placement shape binding failed");
 var receiver=Target(s,HospitalAction.PlaceReceiver,HospitalInteraction.Socket).transform.position;var tray=Find("Receiver_Support_Tray").GetComponent<Renderer>().bounds;
 if(Mathf.Abs(receiver.y-tray.max.y)>.01f||Vector3.Distance(receiver,s.wound.position)>=.55f||receiver.y>=s.wound.position.y)throw new Exception("Receiver support/irrigation alignment failed");
 File.WriteAllText("Temp/HospitalPlacement-verification.txt","PASS: "+count+" fitted meshes ("+triangles+" triangles), finite/nonempty geometry and placement references.\nPASS: disposal target follows user's moved bin rim; world rim center="+rim.center+".\nPASS: receiver supported within 1 cm, below wound and within irrigation reach.\nVisual review: start, top, side, close-up and bins. Static fitted cloth; not cloth physics. Quest hardware input not exercised.");
 }
 static void RenderPlaced(){var s=UnityEngine.Object.FindAnyObjectByType<HospitalScenario>();var drape=Find("Waterproof_Drape");var receiver=s.receiver;var dp=drape.position;var dr=drape.rotation;var rp=receiver.position;var rr=receiver.rotation;bool old=s.oldDressing.activeSelf;var shape=drape.GetComponent<HospitalPlacementShape>();var original=shape.parts.Select(f=>f.sharedMesh).ToArray();try{s.oldDressing.SetActive(false);var ds=Target(s,HospitalAction.PlaceDrape,HospitalInteraction.Socket);drape.SetPositionAndRotation(ds.transform.position,ds.transform.rotation);shape.Apply();var rs=Target(s,HospitalAction.PlaceReceiver,HospitalInteraction.Socket);receiver.SetPositionAndRotation(rs.transform.position,rs.transform.rotation);Render();foreach(var n in new[]{"top","side","bins","close"})File.Copy("Temp/HospitalPlacement-"+n+".png","Temp/HospitalPlacement-placed-"+n+".png",true);}finally{for(int i=0;i<original.Length;i++)shape.parts[i].sharedMesh=original[i];drape.SetPositionAndRotation(dp,dr);receiver.SetPositionAndRotation(rp,rr);s.oldDressing.SetActive(old);EditorSceneManager.SaveScene(SceneManager.GetActiveScene());}}
 static void Render(){var go=new GameObject("Placement preview");go.hideFlags=HideFlags.HideAndDontSave;var c=go.AddComponent<Camera>();c.enabled=false;c.nearClipPlane=.01f;c.farClipPlane=20;c.GetUniversalAdditionalCameraData().allowXRRendering=false;var rt=new RenderTexture(1400,1100,24);rt.Create();var prev=RenderTexture.active;try{foreach(var name in new[]{"top","side","bins","close"}){c.orthographic=true;c.orthographicSize=name=="close"?.34f:name=="bins"?.8f:1.35f;c.transform.position=name=="close"?new Vector3(-.8f,1.65f,-.12f):name=="top"?new Vector3(.15f,4,-.2f):name=="side"?new Vector3(-2.5f,1.7f,-.2f):new Vector3(.5f,1.5f,2.8f);c.transform.LookAt(name=="close"?new Vector3(-.12f,1,-.405f):name=="bins"?new Vector3(1.6f,.4f,1.1f):new Vector3(.15f,1,-.2f),name=="top"?Vector3.forward:Vector3.up);RenderPipeline.SubmitRenderRequest(c,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;var tex=new Texture2D(1400,1100,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1400,1100),0,0);tex.Apply();File.WriteAllBytes("Temp/HospitalPlacement-"+name+".png",tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);}}finally{RenderTexture.active=prev;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);}}
}}
