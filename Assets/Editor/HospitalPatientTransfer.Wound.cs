using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine.SceneManagement;
public static partial class HospitalPatientTransfer {
static void InstallWound(){
 string dir=Folder+"/Wound";var shader=Shader.Find("RadCare/ConformingWound");if(!shader)throw new Exception("Wound shader missing");
 string mp=dir+"/ArmWound.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(mp);if(!mat){mat=new Material(shader);AssetDatabase.CreateAsset(mat,mp);}mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/3D Asset/Blood/Blood_01.png"));mat.SetFloat("_Opacity",1);EditorUtility.SetDirty(mat);
 var report=new List<string>();Directory.CreateDirectory("Documentation/HospitalPatientTransfer/BeforeWound");
 foreach(string path in EmergencyLightingWorkbench.Scenes){
 string backup="Documentation/HospitalPatientTransfer/BeforeWound/"+Path.GetFileName(path);if(!File.Exists(backup))File.Copy(path,backup);
 var s=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);var patient=All<Transform>(s).Single(t=>t.name=="Patient"&&t.parent==null);var visual=patient.Find("Hospital_Patient_Visual");var blood=patient.Find("Blood");var skin=visual.GetComponentsInChildren<MeshCollider>(true).Single(c=>c.name=="Patient_ScanCollider_radcare_patient_body");
 blood.gameObject.SetActive(true);visual.Find("Hospital_Wound_Visual").gameObject.SetActive(false);
 foreach(var r in blood.GetComponentsInChildren<Renderer>(true))r.enabled=false;
 var previous=blood.Find("ConformingWound");if(previous)UnityEngine.Object.DestroyImmediate(previous.gameObject);
 Physics.SyncTransforms();Vector3 desired=new Vector3(patient.position.x+.27f,2f,patient.position.z-.10f);
 // Match the exposed right forearm using the regenerated patient's body bounds.
 var bounds=visual.GetComponentsInChildren<MeshRenderer>().Single(r=>r.name=="radcare_patient_body").bounds;
 desired=new Vector3(bounds.center.x+.22f,2f,bounds.min.z+bounds.size.z*.55f);
 if(!skin.Raycast(new Ray(desired,Vector3.down),out var center,3))throw new Exception("Right forearm surface missing "+desired);
 blood.SetPositionAndRotation(center.point,Quaternion.identity);blood.localScale=Vector3.one;
 int n=20;var vs=new Vector3[(n+1)*(n+1)];var uv=new Vector2[vs.Length];var valid=new bool[vs.Length];var triangles=new List<int>();
 for(int z=0;z<=n;z++)for(int x=0;x<=n;x++){int i=z*(n+1)+x;uv[i]=new Vector2((float)x/n,(float)z/n);var ray=new Ray(center.point+new Vector3((uv[i].x-.5f)*.055f,1,(uv[i].y-.5f)*.085f),Vector3.down);if(skin.Raycast(ray,out var h,2)){vs[i]=blood.InverseTransformPoint(h.point+h.normal*.0012f);valid[i]=true;}}
 for(int z=0;z<n;z++)for(int x=0;x<n;x++){int a=z*(n+1)+x,b=a+1,c=a+n+1,d=c+1;if(valid[a]&&valid[b]&&valid[c])triangles.AddRange(new[]{a,c,b});if(valid[b]&&valid[c]&&valid[d])triangles.AddRange(new[]{b,c,d});}
 if(triangles.Count<1800)throw new Exception("Wound footprint extends off arm");var mesh=new Mesh{name="ConformingRightArmWound"};mesh.vertices=vs;mesh.uv=uv;mesh.triangles=triangles.ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();
 string asset=dir+"/"+s.name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(asset);if(old){EditorUtility.CopySerialized(mesh,old);UnityEngine.Object.DestroyImmediate(mesh);mesh=old;EditorUtility.SetDirty(mesh);}else AssetDatabase.CreateAsset(mesh,asset);
 var go=new GameObject("ConformingWound",typeof(MeshFilter),typeof(MeshRenderer),typeof(DeconWoundAppearance));go.transform.SetParent(blood,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=mat;go.GetComponent<MeshRenderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
 var box=blood.GetComponent<BoxCollider>();box.enabled=true;box.isTrigger=true;box.center=mesh.bounds.center;box.size=mesh.bounds.size+new Vector3(.018f,.025f,.018f);
 foreach(var c in blood.GetComponents<Collider>())if(c!=box)c.enabled=false;
 var scan=patient.Find("Patient_WoundScanCollider");scan.gameObject.SetActive(true);scan.SetPositionAndRotation(blood.position,Quaternion.identity);scan.localScale=Vector3.one;var sb=scan.GetComponent<BoxCollider>();sb.enabled=true;sb.isTrigger=true;sb.center=box.center;sb.size=box.size;
 int tools=0;foreach(var tool in All<MonoBehaviour>(s).Where(t=>t&&(t is Forceps||t is EvaluationForceps))){if(new SerializedObject(tool).FindProperty("targetObject").objectReferenceValue!=blood.gameObject)throw new Exception("Forceps reference mismatch");tools++;}
 int fluids=0;foreach(var component in All<MonoBehaviour>(s).Where(t=>t&&(t is ParticleContactDetector||t is EvaluationParticleContactDetector))){var so=new SerializedObject(component);var ps=so.FindProperty("liquidParticle").objectReferenceValue as ParticleSystem;if(!ps)ps=component.GetComponent<ParticleSystem>();if(!ps)throw new Exception("Liquid particles missing");var trigger=ps.trigger;trigger.enabled=true;for(int i=0;i<trigger.colliderCount;i++)trigger.SetCollider(i,null);trigger.SetCollider(0,box);if(trigger.GetCollider(0)!=box)throw new Exception("Saline target missing");fluids++;}
 Physics.SyncTransforms();if(!Physics.OverlapSphere(box.bounds.center,.004f,~0,QueryTriggerInteraction.Collide).Contains(box))throw new Exception("Wound trigger missing");
 blood.gameObject.SetActive(false);Physics.SyncTransforms();if(go.activeInHierarchy||!scan.gameObject.activeInHierarchy)throw new Exception("Treatment state invalid");blood.gameObject.SetActive(true);
 if(tools!=1||fluids<1)throw new Exception("Missing treatment tools");EditorSceneManager.MarkSceneDirty(s);EditorSceneManager.SaveScene(s);report.Add(s.name+": PASS right arm surface, saline trigger, original forceps reference, wound removal and rescan; center="+center.point);
 }
 AssetDatabase.SaveAssets();File.WriteAllLines("Documentation/HospitalPatientTransfer/wound-validation.txt",report);EditorSceneManager.OpenScene(EmergencyLightingWorkbench.Scenes[0]);File.WriteAllText(Out+"/status.txt","WOUND INSTALLED");
}
}

