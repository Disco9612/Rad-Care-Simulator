using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RadCare.HospitalTreatment.Editor
{
    [InitializeOnLoad]
    public static class HospitalDetailAssets
    {
        const string Folder="Assets/RadCareTreatmentDetails";
        static readonly string[] Names={"radcare_used_dressing","radcare_sterile_dressing","radcare_waterproof_drape","radcare_used_swabs","radcare_fixation_tape","radcare_clinical_waste_bin","radcare_wound_pad"};
        static readonly string[] Anchors={"Existing_Dressing","Fresh_Dressing","Waterproof_Drape","Used_Consumables","Fixation_Tape","Clinical_Waste_Container","Simulated_Wound_Target"};
        static readonly Vector3[] Sizes={new Vector3(.09f,.009f,.12f),new Vector3(.09f,.006f,.12f),new Vector3(.32f,.012f,.34f),new Vector3(.10f,.022f,.075f),new Vector3(.13f,.004f,.025f),new Vector3(.36f,.70f,.40f),new Vector3(.065f,.006f,.09f)};
        [Serializable] class MaterialDef {public string id,texture;public float roughness,metallic;}
        [Serializable] class Assignment {public string part,material;}
        [Serializable] class Definitions {public MaterialDef[] materials;public Assignment[] assignments;}
        [Serializable] class Result {public string name;public int triangles,meshParts;public Vector3 size;public bool urp,physicalGrab;}
        [Serializable] class Results {public string scene;public Result[] assets;}
        static HospitalDetailAssets(){EditorApplication.update+=Poll;}
        static void Poll() {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists("Temp/HospitalDetails.request"))return;
            string command=File.ReadAllText("Temp/HospitalDetails.request").Trim();File.Delete("Temp/HospitalDetails.request");
            try{if(command=="finish")Finish();else if(command=="preview")Preview();else Install();}catch(Exception e){File.WriteAllText("Temp/HospitalDetails-error.txt",e.ToString());Debug.LogException(e);}
        }
        static Bounds BoundsOf(GameObject go) {
            var rs=go.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.enabled).ToArray();
            if(rs.Length==0)throw new Exception("No visible mesh: "+go.name);
            var b=rs[0].bounds;foreach(var r in rs.Skip(1))b.Encapsulate(r.bounds);return b;
        }
        public static void Finish() {
            if(EditorApplication.isPlaying)throw new Exception("Edit mode required");
            foreach(string name in Names) {
                string dir=Folder+"/"+name;
                foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{dir})) {
                    var m=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                    if(new[]{"MAT_cotton","MAT_fiber","MAT_used","MAT_teal","MAT_hem","MAT_tape"}.Contains(Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid)))) {
                        // Sub-millimetre fibers exceed the room shadow map precision; avoid self-shadow speckling.
                        m.SetFloat("_ReceiveShadows",0);m.EnableKeyword("_RECEIVE_SHADOWS_OFF");EditorUtility.SetDirty(m);
                    }
                }
                string path=dir+"/"+name+".prefab";var prefab=PrefabUtility.LoadPrefabContents(path);
                foreach(var r in prefab.GetComponentsInChildren<MeshRenderer>())if(r.name.EndsWith("woven_fibers"))r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                var renderers=prefab.GetComponentsInChildren<MeshRenderer>();
                if(renderers.Any(r=>r.name.EndsWith("woven_fibers"))) {
                    var lod=prefab.GetComponent<LODGroup>();if(lod==null)lod=prefab.AddComponent<LODGroup>();
                    lod.SetLODs(new[]{new LOD(.25f,renderers),new LOD(0,renderers.Where(r=>!r.name.EndsWith("woven_fibers")).ToArray())});
                    lod.RecalculateBounds();
                }
                PrefabUtility.SaveAsPrefabAsset(prefab,path);PrefabUtility.UnloadPrefabContents(prefab);
            }
            var s=UnityEngine.Object.FindAnyObjectByType<HospitalScenario>();s.stream.enabled=false;s.input.pointer.enabled=false;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
            const string export="C:/Users/ts/Documents/ChatGPT/RadCareProject/GeneratedAssets/HospitalTreatment_2026-10-20/TreatmentDetails/RadCareTreatmentDetails_URP.unitypackage";
            AssetDatabase.ExportPackage(Folder,export,ExportPackageOptions.Recurse);
            File.WriteAllText("Temp/HospitalDetails-finished.txt",export);
            if(File.Exists("Temp/HospitalDetails-error.txt"))File.Delete("Temp/HospitalDetails-error.txt");
        }
        public static void Preview() {
            var s=UnityEngine.Object.FindAnyObjectByType<HospitalScenario>();
            var go=new GameObject("Temporary Detail Camera");go.hideFlags=HideFlags.HideAndDontSave;
            var camera=go.AddComponent<Camera>();camera.enabled=false;camera.nearClipPlane=.01f;camera.farClipPlane=8;camera.orthographic=true;camera.orthographicSize=.09f;
            go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().allowXRRendering=false;
            camera.transform.position=s.oldDressing.transform.position+new Vector3(-.10f,.20f,-.10f);camera.transform.LookAt(s.oldDressing.transform.position);
            var rt=new RenderTexture(800,800,24);rt.Create();var previous=RenderTexture.active;
            var fiber=s.oldDressing.GetComponentsInChildren<MeshRenderer>().Single(r=>r.name.EndsWith("woven_fibers"));
            try {
                foreach(string view in new[]{"with-fiber","without-fiber"}) {
                    fiber.enabled=view=="with-fiber";
                    UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=rt});
                    RenderTexture.active=rt;var tex=new Texture2D(800,800,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,800,800),0,0);tex.Apply();File.WriteAllBytes("Temp/HospitalDetails-"+view+".png",tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);
                }
            }finally{fiber.enabled=true;RenderTexture.active=previous;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);}
        }
        [MenuItem("Tools/RadCare/Import Treatment Detail Assets")]
        public static void Install() {
            var scene=SceneManager.GetActiveScene();
            if(scene.path!="Assets/Scenes/Hospital_Tutorial.unity"||EditorApplication.isPlaying)throw new Exception("Hospital edit mode required");
            var s=UnityEngine.Object.FindAnyObjectByType<HospitalScenario>();
            Directory.CreateDirectory("Temp/HospitalDetailsBackup");EditorSceneManager.SaveScene(scene,"Temp/HospitalDetailsBackup/Hospital_Tutorial.unity",true);
            var results=new List<Result>();
            for(int i=0;i<Names.Length;i++) {
                string dir=Folder+"/"+Names[i];
                var importer=(ModelImporter)AssetImporter.GetAtPath(dir+"/asset.fbx");
                if(importer==null)throw new Exception("Missing Studio export: "+Names[i]);
                importer.useFileScale=true;importer.globalScale=1;importer.bakeAxisConversion=true;importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.SaveAndReimport();
                var defs=JsonUtility.FromJson<Definitions>(File.ReadAllText(dir+"/materials.json"));
                var materials=new Dictionary<string,Material>();
                foreach(var d in defs.materials) {
                    string path=dir+"/"+d.id+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
                    if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
                    m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(dir+"/"+d.texture));m.SetColor("_BaseColor",Color.white);
                    m.SetFloat("_Metallic",d.metallic);m.SetFloat("_Smoothness",1-d.roughness);EditorUtility.SetDirty(m);materials.Add(d.id,m);
                }
                var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(dir+"/asset.fbx"));
                model.name=Names[i];model.transform.rotation=Quaternion.Euler(0,180,0)*model.transform.rotation;
                foreach(var r in model.GetComponentsInChildren<MeshRenderer>())r.sharedMaterial=materials[defs.assignments.Single(a=>a.part==r.name).material];
                var prefab=PrefabUtility.SaveAsPrefabAsset(model,dir+"/"+Names[i]+".prefab");UnityEngine.Object.DestroyImmediate(model);
                var anchor=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<Transform>(true)).First(t=>t.name==Anchors[i]);
                if(anchor.Find("Studio_Model")!=null)throw new Exception("Detail model already installed: "+anchor.name);
                bool wasActive=anchor.gameObject.activeSelf;anchor.gameObject.SetActive(true);
                // Keep the existing anchor and all scenario references; replace only its visible primitive.
                var primitive=anchor.GetComponent<MeshRenderer>();if(primitive!=null)primitive.enabled=false;
                foreach(var c in anchor.GetComponents<Collider>())c.enabled=false;
                anchor.localScale=Vector3.one;
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);instance.name="Studio_Model";
                instance.transform.SetParent(anchor,false);
                var bounds=BoundsOf(instance);instance.transform.position+=anchor.position-bounds.center;
                var interaction=s.targets.FirstOrDefault(t=>t.movable==anchor&&t.IsMovable);
                if(interaction!=null) {
                    var collider=(BoxCollider)interaction.grabCollider;
                    collider.transform.localPosition=Vector3.zero;collider.transform.localRotation=Quaternion.identity;collider.transform.localScale=Vector3.one;
                    var b=HospitalGrabCalibration.LocalBounds(instance.transform,collider.transform);collider.center=b.center;collider.size=Vector3.Max(b.size+Vector3.one*.004f,Vector3.one*.016f);collider.enabled=false;
                    if(anchor.GetComponentInChildren<Canvas>(true)!=null)throw new Exception("UI was reattached to movable model");
                }
                var rs=instance.GetComponentsInChildren<MeshRenderer>(true);
                bool urp=rs.All(r=>r.sharedMaterials.All(m=>m!=null&&m.shader.name=="Universal Render Pipeline/Lit"&&m.GetTexture("_BaseMap")!=null));
                if(!urp)throw new Exception("Material check failed: "+Names[i]);
                if((BoundsOf(instance).size-Sizes[i]).magnitude>.002f)throw new Exception("Dimension mismatch: "+Names[i]+" "+BoundsOf(instance).size);
                results.Add(new Result{name=Names[i],triangles=instance.GetComponentsInChildren<MeshFilter>(true).Sum(f=>f.sharedMesh.triangles.Length/3),meshParts=rs.Length,size=BoundsOf(instance).size,urp=urp,physicalGrab=interaction!=null});
                anchor.gameObject.SetActive(wasActive);
            }
            // The fenestrated sheet's opening should sit around the wound rather than beside it.
            var drapeSocket=s.targets.Single(t=>t.kind==HospitalInteraction.Socket&&t.action==HospitalAction.PlaceDrape);
            drapeSocket.transform.position=s.wound.position+Vector3.down*.015f;
            Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            File.WriteAllText("Temp/HospitalDetails-verification.json",JsonUtility.ToJson(new Results{scene=scene.path,assets=results.ToArray()},true));
            HospitalScenarioInstaller.Test();
        }
    }
}
