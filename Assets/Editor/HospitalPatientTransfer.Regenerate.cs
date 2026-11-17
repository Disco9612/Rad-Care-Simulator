using System;using System.IO;using System.Linq;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine.SceneManagement;
public static partial class HospitalPatientTransfer {
static void ApplyRegenerated(){
 const string path="Assets/EmergencyDeconHospitalPatient/Regenerated/patient_straight.fbx";
 var imp=(ModelImporter)AssetImporter.GetAtPath(path);imp.isReadable=true;imp.SaveAndReimport();
 var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);
 var original=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/RadCareExpansion/radcare_patient/asset.fbx");
 var poseRoot=PrefabUtility.LoadPrefabContents(Folder+"/Hospital_Patient_Visual.prefab");
 var meshes=new System.Collections.Generic.Dictionary<string,Mesh>();
 foreach(var f in model.GetComponentsInChildren<MeshFilter>(true)){
 var old=original.GetComponentsInChildren<MeshFilter>(true).Single(x=>x.name==f.name);
 var m=UnityEngine.Object.Instantiate(f.sharedMesh);var vs=m.vertices;
 for(int i=0;i<vs.Length;i++)vs[i]=old.transform.InverseTransformPoint(f.transform.TransformPoint(vs[i]));
 var target=poseRoot.GetComponentsInChildren<MeshFilter>(true).Single(x=>x.name==f.name);
 for(int i=0;i<vs.Length;i++){var v=poseRoot.transform.InverseTransformPoint(target.transform.TransformPoint(vs[i]));v.x=-.0044257044f-v.x;v.y=.22451252f-v.y;vs[i]=target.transform.InverseTransformPoint(poseRoot.transform.TransformPoint(v));}
 m.vertices=vs;m.RecalculateNormals();m.RecalculateBounds();m.RecalculateTangents();
 string ap=Folder+"/Regenerated/v4_"+f.name+".asset";var prev=AssetDatabase.LoadAssetAtPath<Mesh>(ap);
 if(prev){EditorUtility.CopySerialized(m,prev);UnityEngine.Object.DestroyImmediate(m);m=prev;}else AssetDatabase.CreateAsset(m,ap);EditorUtility.SetDirty(m);meshes[f.name]=m;
 }
 var root=poseRoot;
 foreach(var f in root.GetComponentsInChildren<MeshFilter>(true))if(meshes.ContainsKey(f.name))f.sharedMesh=meshes[f.name];
 root.transform.Find("Hospital_Wound_Visual").gameObject.SetActive(false);
 PrefabUtility.SaveAsPrefabAsset(root,Folder+"/Hospital_Patient_Visual.prefab");PrefabUtility.UnloadPrefabContents(root);AssetDatabase.SaveAssets();
 foreach(var p in EmergencyLightingWorkbench.Scenes){var s=EditorSceneManager.OpenScene(p,OpenSceneMode.Single);var patient=All<Transform>(s).Single(t=>t.name=="Patient"&&t.parent==null);var visual=patient.Find("Hospital_Patient_Visual");
 foreach(var f in visual.GetComponentsInChildren<MeshFilter>(true))if(meshes.ContainsKey(f.name))f.sharedMesh=meshes[f.name];
 foreach(var c in visual.GetComponentsInChildren<MeshCollider>(true))if(c.name.StartsWith("Patient_ScanCollider_"))c.sharedMesh=c.transform.parent.GetComponent<MeshFilter>().sharedMesh;
 patient.Find("Blood").gameObject.SetActive(false);patient.Find("Patient_WoundScanCollider").gameObject.SetActive(false);visual.Find("Hospital_Wound_Visual").gameObject.SetActive(false);
 EditorSceneManager.MarkSceneDirty(s);EditorSceneManager.SaveScene(s);}
 EditorSceneManager.OpenScene(EmergencyLightingWorkbench.Scenes[0]);File.WriteAllText(Out+"/status.txt","REGENERATED APPLIED");
}
}



