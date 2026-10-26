using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Local editor-only requests are deliberately limited to this one scene.
[InitializeOnLoad]
public static class HospitalSceneLayout
{
    const string ScenePath = "Assets/Scenes/Hospital_Tutorial.unity";
    const string RequestPath = "Temp/HospitalLayout.request";
    const string ReportPath = "Temp/HospitalLayout-inspection.json";
    const string LayoutName = "Hospital_Treatment_Layout";
    public static void OpenAndArrange() {
        EditorSceneManager.OpenScene(ScenePath);
        Arrange();
        RenderPreview();
    }
    [Serializable] class Placement { public string name; public Vector3 center, size; }
    [Serializable] class LayoutReport {
        public string status, scene, backup;
        public int prefabInstances, missingScripts;
        public Vector3 learnerPosition;
        public Placement[] placements;
        public string[] limitations;
    }
    [Serializable] class ObjectInfo {
        public string name; public bool active; public Vector3 position, rotation, scale;
        public string[] components; public ObjectInfo[] children;
    }
    [Serializable] class Inspection {
        public string scene; public bool playing, dirty; public ObjectInfo[] roots;
    }
    static HospitalSceneLayout() {
        EditorApplication.delayCall += Inspect;
        EditorApplication.update += Poll;
    }
    static ObjectInfo Describe(Transform t, int depth) {
        return new ObjectInfo { name=t.name,active=t.gameObject.activeSelf,position=t.position,
            rotation=t.eulerAngles,scale=t.lossyScale,
            components=t.GetComponents<Component>().Select(c=>c==null?"MISSING":c.GetType().Name).ToArray(),
            children=depth>0?t.Cast<Transform>().Select(c=>Describe(c,depth-1)).ToArray():null };
    }
    [MenuItem("Tools/RadCare/Inspect Hospital Scene")]
    public static void Inspect() {
        var s=SceneManager.GetActiveScene();
        Directory.CreateDirectory("Temp");
        File.WriteAllText(ReportPath,JsonUtility.ToJson(new Inspection {
            scene=s.path,playing=EditorApplication.isPlaying,dirty=s.isDirty,
            roots=s.GetRootGameObjects().Select(o=>Describe(o.transform,3)).ToArray()
        },true));
    }
    static void Poll() {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists(RequestPath))return;
        var command=File.ReadAllText(RequestPath).Trim();
        File.Delete(RequestPath);
        try {
            if(command=="inspect")Inspect();
            else if(command=="layout")Arrange();
            else if(command=="render")RenderPreview();
            else throw new InvalidOperationException("Unknown layout command");
        } catch(Exception e) { File.WriteAllText("Temp/HospitalLayout-error.txt",e.ToString());Debug.LogException(e); }
    }
    static Bounds BoundsOf(GameObject obj) {
        var rs=obj.GetComponentsInChildren<MeshRenderer>();
        if(rs.Length==0)throw new InvalidOperationException("No geometry: "+obj.name);
        var b=rs[0].bounds;foreach(var r in rs.Skip(1))b.Encapsulate(r.bounds);return b;
    }
    static MeshRenderer Part(GameObject obj,string suffix) {
        return obj.GetComponentsInChildren<MeshRenderer>().First(r=>r.name.EndsWith(suffix));
    }
    static GameObject Group(string name,Transform parent) {
        var o=new GameObject(name);Undo.RegisterCreatedObjectUndo(o,"Hospital layout");
        if(parent!=null)o.transform.SetParent(parent,false);return o;
    }
    static GameObject Place(string asset,string label,Transform parent,Vector3 position,float yaw=0) {
        string folder=asset=="radcare_patient_bed"||asset=="radcare_treatment_room"||asset=="radcare_patient_supine"
            ||asset=="radcare_assistant_nurse"||asset=="radcare_kidney_basin"?"RadCareExpansion":"RadCareHospital";
        string dir=asset=="radcare_patient_supine"?"radcare_patient":asset;
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/"+folder+"/"+dir+"/"+asset+".prefab");
        if(prefab==null)throw new InvalidOperationException("Missing prefab: "+asset);
        var anchor=Group(label,parent);
        var model=(GameObject)PrefabUtility.InstantiatePrefab(prefab,SceneManager.GetActiveScene());
        Undo.RegisterCreatedObjectUndo(model,"Hospital layout");
        // Keep each imported model's axis correction under a clean scene-space anchor.
        model.transform.SetParent(anchor.transform,false);
        anchor.transform.SetPositionAndRotation(position,Quaternion.Euler(0,yaw,0));
        var b=BoundsOf(anchor);anchor.transform.position+=Vector3.up*(position.y-b.min.y);
        return anchor;
    }
    static void SetActive(GameObject obj,bool value) {
        Undo.RecordObject(obj,"Hospital layout");obj.SetActive(value);
    }
    static void AddSolid(MeshRenderer r) {
        var col=Undo.AddComponent<BoxCollider>(r.gameObject);
        col.center=r.GetComponent<MeshFilter>().sharedMesh.bounds.center;
        col.size=r.GetComponent<MeshFilter>().sharedMesh.bounds.size;
        Undo.RecordObject(r.gameObject,"Hospital collision");
        r.gameObject.layer=LayerMask.NameToLayer("ColliderObject");
    }
    [MenuItem("Tools/RadCare/Arrange Hospital Treatment Assets")]
    public static void Arrange() {
        var scene=SceneManager.GetActiveScene();
        if(scene.path!=ScenePath||EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Open Hospital_Tutorial in Edit mode first.");
        if(scene.GetRootGameObjects().Any(o=>o.name==LayoutName))
            throw new InvalidOperationException("Layout already exists; adjust its anchors or Undo before rebuilding.");
        string backup="Temp/HospitalLayoutBackup/Hospital_Tutorial-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";
        Directory.CreateDirectory(Path.GetDirectoryName(backup));
        if(!EditorSceneManager.SaveScene(scene,backup,true))throw new IOException("Could not back up the open scene.");
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Arrange hospital treatment room");
        try {
            var previous=scene.GetRootGameObjects();
            var root=Group(LayoutName,null);
            var environment=Group("00_Environment",root.transform);
            var assessment=Group("01_Patient_Assessment",root.transform);
            var irrigation=Group("02_Irrigation_Preparation",root.transform);
            var dressing=Group("03_Dressing_Preparation",root.transform);
            var recording=Group("04_Chart_And_Monitor",root.transform);
            var room=Place("radcare_treatment_room","Treatment_Room",environment.transform,Vector3.zero);
            var floor=Part(room,"floor_slab");
            room.transform.position-=Vector3.up*floor.bounds.max.y;
            foreach(var suffix in new[]{"floor_slab","back_wall","left_wall","cabinet","disposal_bin"})
                AddSolid(Part(room,suffix));
            var bed=Place("radcare_patient_bed","Patient_Bed",assessment.transform,new Vector3(.15f,0,-.20f));
            float mattress=Part(bed,"sheet_top").bounds.max.y;
            var patient=Place("radcare_patient_supine","Patient_Supine",assessment.transform,new Vector3(.15f,mattress+.004f,-.20f));
            Place("radcare_assistant_nurse","Assistant_Nurse",assessment.transform,new Vector3(1.08f,0,-.05f),-90);
            var lamp=Place("radcare_exam_light","Examination_Light",assessment.transform,new Vector3(.93f,0,-1.38f));
            Vector3 head=Part(lamp,"head_housing").bounds.center-lamp.transform.position;head.y=0;
            Vector3 desired=bed.transform.position-lamp.transform.position;desired.y=0;
            lamp.transform.rotation=Quaternion.Euler(0,Vector3.SignedAngle(head,desired,Vector3.up),0);
            var washCart=Place("radcare_treatment_cart","Irrigation_Cart",irrigation.transform,new Vector3(-1.25f,0,-.98f));
            float washTop=Part(washCart,"shelf_upper").bounds.max.y+.003f;
            Place("radcare_kidney_basin","Irrigation_Receiver",irrigation.transform,new Vector3(-1.16f,washTop,-.98f));
            var syringe=Place("radcare_irrigation_syringe","Needleless_Irrigation_Syringe",irrigation.transform,new Vector3(-1.46f,washTop,-.98f));
            syringe.transform.rotation=Quaternion.Euler(90,0,0);
            var sb=BoundsOf(syringe);
            syringe.transform.position+=new Vector3(-1.46f-sb.center.x,washTop-sb.min.y,-.98f-sb.center.z);
            var dressCart=Place("radcare_treatment_cart","Clean_Dressing_Cart",dressing.transform,new Vector3(-1.25f,0,.72f));
            float dressingTop=Part(dressCart,"shelf_upper").bounds.max.y+.003f;
            Place("radcare_dressing_kit","Dressing_Kit",dressing.transform,new Vector3(-1.25f,dressingTop,.72f));
            float lower=Part(washCart,"shelf_lower").bounds.max.y+.003f;
            Place("radcare_irrigation_basin","Spare_Receiver",irrigation.transform,new Vector3(-1.25f,lower,-.98f));
            var counter=Part(room,"counter").bounds;
            Place("radcare_patient_monitor","Patient_Monitor",recording.transform,new Vector3(counter.center.x,counter.max.y+.003f,-1.62f),-90);
            Place("radcare_chart_tablet","Chart_Tablet",recording.transform,new Vector3(counter.center.x,counter.max.y+.003f,-.93f),-90);
            var rig=previous.First(o=>o.GetComponent<OVRCameraRig>()!=null);
            Undo.RecordObject(rig.transform,"Hospital learner position");
            rig.transform.SetPositionAndRotation(new Vector3(-1.12f,0,-.10f),Quaternion.Euler(0,90,0));
            // Preserve the previous scenario and serialized bindings, but don't run its unrelated briefing.
            foreach(var old in previous.Where(o=>o.name=="GameManager"||o.name=="SoundManager"))SetActive(old,false);
            foreach(var canvas in rig.GetComponentsInChildren<Canvas>(true).Where(c=>c.name=="Unity Canvas"))SetActive(canvas.gameObject,false);
            var lights=Group("05_Room_Lighting",root.transform);
            var key=Undo.AddComponent<Light>(Group("Soft_Key",lights.transform));
            key.type=LightType.Directional;key.intensity=1.05f;key.shadows=LightShadows.Soft;key.transform.rotation=Quaternion.Euler(48,-35,0);
            var fill=Undo.AddComponent<Light>(Group("Soft_Fill",lights.transform));
            fill.type=LightType.Directional;fill.intensity=.32f;fill.transform.rotation=Quaternion.Euler(30,140,0);
            var placements=new[]{room,bed,patient,washCart,dressCart}.Select(o=> {
                var b=BoundsOf(o);return new Placement{name=o.name,center=b.center,size=b.size};
            }).ToArray();
            int missing=root.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
            if(missing!=0)throw new InvalidOperationException("Missing scripts in added content.");
            foreach(var r in root.GetComponentsInChildren<MeshRenderer>())
                if(r.sharedMaterials.Any(m=>m==null||m.shader==null||m.shader.name!="Universal Render Pipeline/Lit"))
                    throw new InvalidOperationException("Missing/incorrect material: "+r.name);
            Physics.SyncTransforms();
            if(!Physics.Raycast(new Vector3(-1.12f,1,-.10f),Vector3.down,out var hit,2,1<<LayerMask.NameToLayer("ColliderObject"))||Mathf.Abs(hit.point.y)>.01f)
                throw new InvalidOperationException("Learner floor alignment failed.");
            EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save Hospital_Tutorial.");
            Undo.CollapseUndoOperations(undo);
            Selection.activeGameObject=root;
            if(SceneView.lastActiveSceneView!=null)SceneView.lastActiveSceneView.LookAt(new Vector3(0,.9f,-.1f),Quaternion.Euler(30,140,0),5.7f,false,true);
            File.WriteAllText("Temp/HospitalLayout-result.json",JsonUtility.ToJson(new LayoutReport {
                status="passed",scene=scene.path,backup=backup,prefabInstances=root.GetComponentsInChildren<Transform>().Count(t=>PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)),
                missingScripts=missing,learnerPosition=rig.transform.position,placements=placements,
                limitations=new[]{"Asset layout only; no new clinical progression or tool-grab behavior.","Previous scenario managers and HUD preserved but disabled.","Only room floor, walls, cabinet and bin have new static colliders; object interactions and headset traversal require testing."}
            },true));
            Inspect();
        } catch {
            Undo.RevertAllDownToGroup(undo);
            throw;
        }
    }
    [MenuItem("Tools/RadCare/Render Hospital Layout Preview")]
    public static void RenderPreview() {
        var scene=SceneManager.GetActiveScene();
        if(scene.path!=ScenePath||EditorApplication.isPlaying)throw new InvalidOperationException("Hospital edit mode required.");
        Directory.CreateDirectory("Temp/HospitalLayoutPreviews");
        var go=new GameObject("Temporary_Layout_Preview");
        go.hideFlags=HideFlags.HideAndDontSave;
        var camera=go.AddComponent<Camera>();camera.enabled=false;camera.cameraType=CameraType.Game;
        camera.GetUniversalAdditionalCameraData().allowXRRendering=false;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.10f,.13f,.17f);
        camera.nearClipPlane=.03f;camera.farClipPlane=40;
        var rt=new RenderTexture(1400,1000,24,RenderTextureFormat.ARGB32);rt.Create();
        bool previousAsyncCompilation=ShaderUtil.allowAsyncCompilation;
        try {
            ShaderUtil.allowAsyncCompilation=false;
            foreach(var view in new[]{"overview","learner"}) {
                camera.orthographic=view=="overview";camera.orthographicSize=3.35f;camera.fieldOfView=75;
                camera.transform.position=view=="overview"?new Vector3(-6.2f,4.5f,6.3f):new Vector3(-1.12f,1.65f,-.1f);
                camera.transform.LookAt(view=="overview"?new Vector3(0,.9f,-.10f):new Vector3(.15f,.92f,-.20f));
                for(int i=0;i<3;i++)RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
                var prev=RenderTexture.active;RenderTexture.active=rt;
                var tex=new Texture2D(1400,1000,TextureFormat.RGB24,false);
                tex.ReadPixels(new Rect(0,0,1400,1000),0,0);tex.Apply();
                File.WriteAllBytes("Temp/HospitalLayoutPreviews/"+view+".png",tex.EncodeToPNG());
                RenderTexture.active=prev;UnityEngine.Object.DestroyImmediate(tex);
            }
        } finally {ShaderUtil.allowAsyncCompilation=previousAsyncCompilation;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);}
    }
}
