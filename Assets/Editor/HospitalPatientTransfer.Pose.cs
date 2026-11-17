using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static partial class HospitalPatientTransfer {
    static float Ramp(float a, float b, float x) {
        float t = Mathf.Clamp01((x-a)/(b-a));
        return t*t*(3f-2f*t);
    }
    static bool DescendsFrom(Transform child, Transform ancestor) => child == ancestor || child.IsChildOf(ancestor);
    static void RelaxPose(bool preserveWound = false) {
        if (EditorApplication.isPlaying || Lightmapping.isRunning) throw new Exception("Edit mode required");
        var active = SceneManager.GetActiveScene();
        if (active.isDirty) EditorSceneManager.SaveScene(active);
        string restore = active.path;
        var sourceImporter = (ModelImporter)AssetImporter.GetAtPath("Assets/RadCareExpansion/radcare_patient/asset.fbx");
        bool originalReadable = sourceImporter.isReadable;
        if (!originalReadable) { sourceImporter.isReadable = true; sourceImporter.SaveAndReimport(); }
        try {
            string prefabPath = Folder+"/Hospital_Patient_Visual.prefab";
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try {
                foreach (string part in new [] {"radcare_patient_body", "radcare_patient_outfit"}) {
                    
                    var filter = root.GetComponentsInChildren<MeshFilter>(true).Single(x => x.name == part);
                    var sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/RadCareExpansion/radcare_patient/asset.fbx");
                    var sourceFilter = sourceModel.GetComponentsInChildren<MeshFilter>(true).Single(x=>x.name==part);
                    var mesh = UnityEngine.Object.Instantiate(sourceFilter.sharedMesh);
                    mesh.name = "Decon_Relaxed_"+part;
                    var vertices = mesh.vertices;
                    for (int i=0;i<vertices.Length;i++) {
                        Vector3 inRoot = root.transform.InverseTransformPoint(filter.transform.TransformPoint(vertices[i]));
                        float arm = Ramp(.125f,.205f,Mathf.Abs(inRoot.x))*Ramp(-.60f,-.10f,inRoot.z);
                        inRoot.y -= .23f*arm;
                        if (part=="radcare_patient_body" && inRoot.z>-.10f)
                            inRoot.y -= .35f*(inRoot.z+.10f)*Ramp(.17f,.21f,Mathf.Abs(inRoot.x));
                        inRoot.x += Mathf.Sign(inRoot.x)*.035f*Ramp(.15f,.205f,Mathf.Abs(inRoot.x))*Ramp(-.52f,-.10f,inRoot.z);
                        vertices[i] = filter.transform.InverseTransformPoint(root.transform.TransformPoint(inRoot));
                    }
                    mesh.vertices = vertices;
                    mesh.RecalculateNormals();
                    mesh.RecalculateBounds();
                    mesh.RecalculateTangents();
                    string assetPath = Folder+"/"+mesh.name+".asset";
                    var old = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
                    if (old) {
                        EditorUtility.CopySerialized(mesh, old);
                        UnityEngine.Object.DestroyImmediate(mesh);
                        filter.sharedMesh = old;
                    } else {
                        AssetDatabase.CreateAsset(mesh,assetPath);
                        filter.sharedMesh = mesh;
                    }
                }
                PrefabUtility.SaveAsPrefabAsset(root,prefabPath);
            } finally { PrefabUtility.UnloadPrefabContents(root); }
        } finally {
            if (!originalReadable) { var restoredImporter = (ModelImporter)AssetImporter.GetAtPath("Assets/RadCareExpansion/radcare_patient/asset.fbx"); restoredImporter.isReadable = false; restoredImporter.SaveAndReimport(); }
        }
        AssetDatabase.SaveAssets();
        var report = new List<string>();
        foreach (string path in EmergencyLightingWorkbench.Scenes) {
            var s = EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
            var patient = All<Transform>(s).Single(t=>t.name=="Patient"&&t.parent==null);
            var visual = patient.Find("Hospital_Patient_Visual");
            var blood = patient.Find("Blood");
            var wound = visual.Find("Hospital_Wound_Visual");
            if (!visual||!wound||!blood) throw new Exception("Transferred patient missing in "+path);
            foreach (string part in new [] {"radcare_patient_body", "radcare_patient_outfit"}) {
                var sceneFilter = visual.GetComponentsInChildren<MeshFilter>(true).Single(x=>x.name==part);
                sceneFilter.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Decon_Relaxed_"+part+".asset");
            }
            foreach (var c in visual.GetComponentsInChildren<MeshCollider>(true).Where(c=>c.name.StartsWith("Patient_ScanCollider_"))) {
                var parentFilter = c.transform.parent.GetComponent<MeshFilter>();
                if (!parentFilter) throw new Exception("Scan collider lost mesh parent");
                c.sharedMesh = parentFilter.sharedMesh;
            }
            Physics.SyncTransforms();
            if (preserveWound) {
                EditorSceneManager.MarkSceneDirty(s);
                EditorSceneManager.SaveScene(s);
                report.Add(s.name+": straight arm mesh applied; wound and Blood transforms retained");
                continue;
            }
            Vector3 desired = patient.TransformPoint(new Vector3(.3515f,.1021f,.0672f));
            var candidates = new List<RaycastHit>();
            for (float dx=-.18f;dx<=.18f;dx+=.02f)
            for (float dz=-.32f;dz<=.32f;dz+=.02f) {
                Vector3 at = new Vector3(desired.x+dx,2f,desired.z+dz);
                foreach (var hit in Physics.RaycastAll(new Ray(at,Vector3.down),3f)) {
                    if (hit.point.x<=patient.position.x+.15f || !DescendsFrom(hit.collider.transform,visual) || DescendsFrom(hit.collider.transform,wound)) continue;
                    if (!hit.collider.name.StartsWith("Patient_ScanCollider_")) continue;
                    candidates.Add(hit);
                }
            }
            if (candidates.Count==0) { File.WriteAllLines(Out+"/right-arm-search.txt",visual.GetComponentsInChildren<MeshCollider>(true).Select(c=>c.name+" enabled="+c.enabled+" active="+c.gameObject.activeInHierarchy+" bounds="+c.bounds.ToString()+" mesh="+(c.sharedMesh?c.sharedMesh.name:"null"))); throw new Exception("No right arm collider near wound: "+s.name+" "+desired); }
            var armHits = candidates.Where(h=>h.point.x>patient.position.x+.19f && h.collider.name.Contains("patient_body"))
                .OrderBy(h=>Vector2.Distance(new Vector2(h.point.x,h.point.z),new Vector2(desired.x,desired.z)))
                .ThenByDescending(h=>h.point.y).ToArray();
            var chosen = armHits.Length>0?armHits[0]:candidates.OrderBy(h=>Vector2.Distance(new Vector2(h.point.x,h.point.z),new Vector2(desired.x,desired.z))).First();
            Vector3 surface = chosen.point;
            if (Vector2.Distance(new Vector2(surface.x,surface.z),new Vector2(desired.x,desired.z))>.32f) throw new Exception("Right arm too far from original wound: "+surface+" target="+desired);            wound.position += surface+Vector3.up*.006f-VisibleBounds(wound).center;
            var wb = VisibleBounds(wound);
            blood.SetPositionAndRotation(wb.center,Quaternion.identity);
            var box = blood.GetComponent<BoxCollider>();
            box.size = Vector3.Max(wb.size+new Vector3(.025f,.025f,.025f),new Vector3(.075f,.035f,.085f));
            var scan = patient.Find("Patient_WoundScanCollider");
            scan.SetPositionAndRotation(blood.position,Quaternion.identity);
            scan.GetComponent<BoxCollider>().size=box.size;
            Physics.SyncTransforms();
            ValidateScene(s);
            EditorSceneManager.MarkSceneDirty(s);
            EditorSceneManager.SaveScene(s);
            report.Add(s.name+": right wound="+wb.center.ToString("F4")+"; right arm surface="+surface.ToString("F4")+"; hand displacement up to 0.24m toward mattress; validation PASS");
        }
        EditorSceneManager.OpenScene(restore,OpenSceneMode.Single);
        File.WriteAllLines(Out+"/arm-pose.txt",report);
        File.WriteAllText(Out+"/status.txt","POSE COMPLETE");
    }
}





