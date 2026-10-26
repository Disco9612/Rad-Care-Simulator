using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using TMPro;
using UnityEngine.UI;
namespace RadCare.HospitalTreatment.Editor {
 [InitializeOnLoad] public static class HospitalEnvironmentBuilder {
  static HospitalEnvironmentBuilder(){EditorApplication.update+=Poll;}
  static Transform Find(string name)=>SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).First(t=>t.name==name);
  static void Poll(){if(EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists("Temp/HospitalEnvironment.request"))return;var command=File.ReadAllText("Temp/HospitalEnvironment.request").Trim();File.Delete("Temp/HospitalEnvironment.request");try{if(EditorApplication.isPlaying||SceneManager.GetActiveScene().path!="Assets/Scenes/Hospital_Tutorial.unity")throw new Exception("Hospital edit mode required");if(command=="audit")Audit();else Build();}catch(Exception e){File.WriteAllText("Temp/HospitalEnvironment-error.txt",e.ToString());}}
  static void Audit(){var text=new System.Text.StringBuilder();foreach(var r in Find("Treatment_Room").GetComponentsInChildren<MeshRenderer>().Where(r=>!r.name.Contains("curtain")))text.AppendLine(r.name+" center="+r.bounds.center.ToString("F3")+" size="+r.bounds.size.ToString("F3"));File.WriteAllText("Temp/HospitalEnvironment-audit.txt",text.ToString());}
  const string AssetRoot="Assets/Scripts/Hospital_Treatment/Environment";
  static Transform root;
  static Material wall, trim, blue, metal, dark, floor, light, white;
  static Material Mat(string name, Color color, float smooth=.25f, float emission=0){
   var path=AssetRoot+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
   if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
   m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",smooth);
   if(emission>0){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*emission);}
   EditorUtility.SetDirty(m);return m;
  }
  static GameObject Box(string name,Vector3 p,Vector3 s,Material m,bool solid=false){
   var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(root,false);g.transform.position=p;g.transform.localScale=s;g.GetComponent<Renderer>().sharedMaterial=m;
   if(!solid)UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());else {int layer=LayerMask.NameToLayer("ColliderObject");if(layer>=0)g.layer=layer;}
   g.isStatic=true;return g;
  }
  static void Label(string name,string value,Vector3 p,float yaw,float width,float height,float size,Color color){
   var g=new GameObject(name);g.transform.SetParent(root,false);g.transform.position=p;g.transform.rotation=Quaternion.Euler(0,yaw,0);
   var t=g.AddComponent<TextMeshPro>();t.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Font/NanumGothic SDF.asset");t.text=value;t.fontSize=size;t.color=color;t.alignment=TextAlignmentOptions.Center;t.rectTransform.sizeDelta=new Vector2(width,height);t.textWrappingMode=TextWrappingModes.NoWrap;t.enableAutoSizing=true;t.fontSizeMin=.1f;t.fontSizeMax=size;t.raycastTarget=false;
  }
  static void Tube(string name,Vector3 a,Vector3 b,float radius,Material m){
   var g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name=name;g.transform.SetParent(root,false);g.transform.position=(a+b)*.5f;g.transform.up=(b-a).normalized;g.transform.localScale=new Vector3(radius*2,(b-a).magnitude*.5f,radius*2);g.GetComponent<Renderer>().sharedMaterial=m;UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());g.isStatic=true;
  }
  static void Lamp(string name,Vector3 p,Vector3 size,bool illuminate){
   Box(name+" frame",p,size+new Vector3(.05f,.015f,.05f),trim);Box(name+" diffuser",p-Vector3.up*.012f,size,light);
   if(illuminate){var g=new GameObject(name+" illumination");g.transform.SetParent(root,false);g.transform.position=p-Vector3.up*.25f;var l=g.AddComponent<Light>();l.type=LightType.Point;l.color=new Color(.91f,.96f,1);l.intensity=1.7f;l.range=5;l.shadows=LightShadows.None;}
  }
  static void Build(){
   var scene=SceneManager.GetActiveScene();
   if(scene.GetRootGameObjects().Any(g=>g.name=="Hospital_Environment_Upgrade")){Refine();CaptureAll();return;}
   // Preserve user-authored placements before any architecture is added.
   EditorSceneManager.SaveScene(scene);Directory.CreateDirectory("Temp/HospitalBackups");File.Copy(scene.path,"Temp/HospitalBackups/Hospital_Tutorial-before-environment-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity");
   var previous=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToDictionary(t=>t.GetInstanceID(),t=>t.localToWorldMatrix);
   Directory.CreateDirectory(AssetRoot);AssetDatabase.Refresh();
   wall=Mat("Warm_Clinical_Wall",new Color(.83f,.87f,.86f));white=Mat("Clean_White",new Color(.91f,.94f,.94f),.4f);
   trim=Mat("Soft_Grey_Trim",new Color(.53f,.63f,.65f));blue=Mat("Hospital_Blue",new Color(.16f,.39f,.46f));metal=Mat("Brushed_Metal",new Color(.56f,.63f,.65f),.65f);metal.SetFloat("_Metallic",.7f);
   dark=Mat("Recess_Shadow",new Color(.055f,.105f,.12f));floor=Mat("Hospital_Vinyl",new Color(.43f,.55f,.56f),.3f);light=Mat("LED_Diffuser",new Color(.9f,.96f,1),.3f,1.6f);
   root=new GameObject("Hospital_Environment_Upgrade").transform;
   Box("West full wall",new Vector3(-2.34f,1.34f,0),new Vector3(.12f,2.68f,4.8f),wall,true);
   // Front wall contains a door and a real opening onto a shallow hospital corridor.
   Box("Entry left pier",new Vector3(-1.95f,1.34f,2.34f),new Vector3(.9f,2.68f,.12f),wall,true);
   Box("Door header",new Vector3(-.9f,2.49f,2.34f),new Vector3(1.2f,.38f,.12f),wall,true);
   Box("Door window divider",new Vector3(-.1f,1.34f,2.34f),new Vector3(.4f,2.68f,.12f),wall,true);
   Box("Window end pier",new Vector3(2.1f,1.34f,2.34f),new Vector3(.6f,2.68f,.12f),wall,true);
   Box("Window sill wall",new Vector3(.95f,.52f,2.34f),new Vector3(1.7f,1.04f,.12f),wall,true);
   Box("Window header",new Vector3(.95f,2.48f,2.34f),new Vector3(1.7f,.4f,.12f),wall,true);
   Box("Acoustic ceiling",new Vector3(0,2.73f,0),new Vector3(4.8f,.1f,4.8f),white,true);
   for(int i=0;i<=8;i++){float q=-2.4f+i*.6f;Box("Ceiling grid X "+i,new Vector3(q,2.674f,0),new Vector3(.012f,.008f,4.8f),trim);Box("Ceiling grid Z "+i,new Vector3(0,2.674f,q),new Vector3(4.8f,.008f,.012f),trim);}
   Lamp("Main ceiling LED",new Vector3(-.6f,2.65f,-.6f),new Vector3(.55f,.02f,1.15f),true);
   Lamp("Entry ceiling LED",new Vector3(.6f,2.65f,1.2f),new Vector3(1.15f,.02f,.55f),true);
   Box("Vent backing",new Vector3(1.5f,2.654f,-1.5f),new Vector3(.47f,.025f,.47f),dark);
   for(int i=0;i<10;i++)Box("HVAC louvre "+i,new Vector3(1.5f,2.63f,-1.71f+i*.046f),new Vector3(.46f,.024f,.022f),white);
   Tube("Ceiling detector",new Vector3(-1.5f,2.62f,1.5f),new Vector3(-1.5f,2.67f,1.5f),.07f,white);
   // Finish existing surfaces without moving their geometry or colliders.
   foreach(var r in Find("Treatment_Room").GetComponentsInChildren<MeshRenderer>()){
    if(r.name.EndsWith("back_wall")||r.name.EndsWith("left_wall"))r.sharedMaterial=wall;
    if(r.name.EndsWith("floor_slab"))r.sharedMaterial=floor;
   }
   Box("West protective wall panel",new Vector3(-2.269f,.62f,0),new Vector3(.018f,1.12f,4.55f),white);
   Box("West wall bumper",new Vector3(-2.235f,.88f,0),new Vector3(.055f,.12f,4.55f),blue);
   Box("West coved skirting",new Vector3(-2.25f,.06f,0),new Vector3(.06f,.12f,4.6f),trim);
   Box("Back lower wall finish",new Vector3(0,.43f,-2.271f),new Vector3(4.55f,.70f,.015f),white);
   for(int i=-1;i<=1;i++)Box("Vinyl welded seam "+i,new Vector3(i*1.2f,.001f,0),new Vector3(.003f,.001f,4.55f),trim);
   // Entry leaf and its inset observation panel. These are environmental objects, not task targets.
   Box("Blue clinical door",new Vector3(-.9f,1.14f,2.345f),new Vector3(1.12f,2.28f,.075f),blue,true);
   foreach(float x in new[]{-1.49f,-.31f})Box("Door jamb",new Vector3(x,1.16f,2.27f),new Vector3(.055f,2.32f,.09f),metal);
   Box("Door top frame",new Vector3(-.9f,2.31f,2.27f),new Vector3(1.23f,.055f,.09f),metal);
   Box("Door vision frame",new Vector3(-.9f,1.67f,2.288f),new Vector3(.4f,.66f,.025f),metal);
   Box("Door frosted vision panel",new Vector3(-.9f,1.67f,2.269f),new Vector3(.34f,.60f,.012f),trim);
   Box("Door kick plate",new Vector3(-.9f,.22f,2.292f),new Vector3(1.03f,.32f,.015f),metal);
   Tube("Door pull",new Vector3(-.48f,.92f,2.21f),new Vector3(-.48f,1.18f,2.21f),.015f,metal);
   Box("Treatment room sign",new Vector3(-.9f,2.48f,2.255f),new Vector3(1.05f,.23f,.02f),blue);
   Label("Treatment room name","처치실 02  |  TREATMENT",new Vector3(-.9f,2.48f,2.24f),0,1,.2f,1.7f,Color.white);
   foreach(float x in new[]{.08f,.95f,1.82f})Box("Observation mullion",new Vector3(x,1.65f,2.29f),new Vector3(.035f,1.25f,.065f),metal);
   foreach(float y in new[]{1.04f,2.27f})Box("Observation frame",new Vector3(.95f,y,2.29f),new Vector3(1.79f,.035f,.065f),metal);
   // Invisible solid pane keeps room bounds consistent without expensive transparent sorting in VR.
   var pane=new GameObject("Observation window collision");pane.transform.SetParent(root,false);pane.transform.position=new Vector3(.95f,1.65f,2.34f);pane.AddComponent<BoxCollider>().size=new Vector3(1.7f,1.22f,.04f);pane.layer=LayerMask.NameToLayer("ColliderObject");
   Box("Corridor floor",new Vector3(0,-.05f,3.3f),new Vector3(4.8f,.1f,1.8f),floor,true);
   Box("Corridor opposite wall",new Vector3(0,1.34f,4.2f),new Vector3(4.8f,2.68f,.12f),wall,true);
   Box("Corridor ceiling",new Vector3(0,2.73f,3.3f),new Vector3(4.8f,.1f,1.8f),white);
   foreach(float x in new[]{-2.34f,2.34f})Box("Corridor end",new Vector3(x,1.34f,3.3f),new Vector3(.12f,2.68f,1.8f),wall);
   Box("Corridor handrail",new Vector3(0,.9f,4.09f),new Vector3(4.5f,.10f,.06f),blue);
   Box("Corridor room door",new Vector3(1.15f,1.12f,4.12f),new Vector3(.96f,2.24f,.05f),blue);
   Label("Corridor wayfinding","진료구역  →\nCLINICAL UNIT",new Vector3(-.55f,1.85f,4.09f),0,1.5f,.5f,3,new Color(.12f,.27f,.3f));
   Lamp("Corridor LED",new Vector3(.6f,2.64f,3.3f),new Vector3(1.15f,.02f,.45f),true);
   // Wall-mounted hand hygiene station occupies the unused west/front corner.
   Box("Handwash cabinet",new Vector3(-2.03f,.43f,1.15f),new Vector3(.45f,.78f,.78f),white,true);
   Box("Sink basin bottom",new Vector3(-2.03f,.81f,1.15f),new Vector3(.43f,.04f,.70f),metal);
   foreach(float z in new[]{.78f,1.52f})Box("Sink side rim",new Vector3(-2.03f,.87f,z),new Vector3(.50f,.09f,.055f),white);
   foreach(float x in new[]{-2.25f,-1.79f})Box("Sink end rim",new Vector3(x,.87f,1.15f),new Vector3(.055f,.09f,.74f),white);
   Tube("Sink drain",new Vector3(-2.03f,.831f,1.15f),new Vector3(-2.03f,.835f,1.15f),.025f,dark);
   Tube("Tap riser",new Vector3(-2.19f,.91f,1.15f),new Vector3(-2.19f,1.1f,1.15f),.018f,metal);
   Tube("Tap spout",new Vector3(-2.19f,1.1f,1.15f),new Vector3(-2.02f,1.1f,1.15f),.018f,metal);
   Box("Paper towel dispenser",new Vector3(-2.19f,1.5f,1.63f),new Vector3(.16f,.28f,.27f),white);
   Box("Towel opening",new Vector3(-2.10f,1.4f,1.63f),new Vector3(.015f,.035f,.19f),dark);
   Box("Hygiene sign",new Vector3(-2.25f,1.77f,1.13f),new Vector3(.03f,.32f,.65f),blue);
   Label("Hand hygiene text","손 위생\nHAND HYGIENE",new Vector3(-2.23f,1.77f,1.13f),-90,.6f,.28f,2,Color.white);
   Box("Supply wall cabinet",new Vector3(2.12f,1.89f,-1.2f),new Vector3(.28f,.5f,1.17f),white);
   for(int i=0;i<3;i++){float z=-1.59f+i*.39f;Box("Supply cabinet front",new Vector3(1.971f,1.89f,z),new Vector3(.018f,.46f,.37f),wall);Box("Supply cabinet pull",new Vector3(1.95f,1.76f,z),new Vector3(.02f,.025f,.12f),metal);}
   Label("Supply cabinet label","멸균 물품  /  STERILE SUPPLIES",new Vector3(1.95f,2.02f,-1.2f),90,1.1f,.14f,1.5f,new Color(.12f,.27f,.3f));
   Box("Bed bay sign",new Vector3(-.22f,2.28f,-2.255f),new Vector3(1.1f,.26f,.025f),blue);
   Label("Bed bay text","02   병원 처치실",new Vector3(-.22f,2.28f,-2.234f),180,1,.22f,2.5f,Color.white);
   AssetDatabase.SaveAssets();
   foreach(var t in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)))if(previous.TryGetValue(t.GetInstanceID(),out var before)&&t.localToWorldMatrix!=before)throw new Exception("Existing transform changed: "+t.name);
   Refine();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);CaptureAll();
   File.WriteAllText("Temp/HospitalEnvironment-result.txt","Hospital enclosure, ceiling, corridor, door, handwash station, storage and signage installed. Existing transforms unchanged: "+previous.Count+". New renderers: "+root.GetComponentsInChildren<Renderer>().Length);
  }
  static void Refine(){
   root=Find("Hospital_Environment_Upgrade");
   foreach(var t in root.GetComponentsInChildren<TextMeshPro>()){t.enableAutoSizing=true;t.fontSizeMin=.1f;t.fontSizeMax=3;t.margin=new Vector4(.025f,.015f,.025f,.015f);t.ForceMeshUpdate();}
   Find("Corridor wayfinding").GetComponent<TextMeshPro>().color=new Color(.12f,.27f,.3f);
   foreach(var l in root.GetComponentsInChildren<Light>())l.intensity=.95f;
   EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
  }
  static void CaptureAll(){
   Directory.CreateDirectory("Documentation/HospitalTreatment/PlacementPreviews");
   Capture("hospital-interior",new Vector3(-1.85f,1.65f,1.85f),new Vector3(.3f,1.25f,-.6f));
   Capture("hospital-entry",new Vector3(.7f,1.65f,-1.8f),new Vector3(-.1f,1.45f,2.3f));
   Capture("hospital-player",new Vector3(-1.12f,1.65f,-.1f),new Vector3(.6f,1.4f,-.2f));
  }
  static void Capture(string name,Vector3 position,Vector3 look){
   var g=new GameObject("Environment preview");var c=g.AddComponent<Camera>();c.enabled=false;c.transform.position=position;c.transform.LookAt(look);c.fieldOfView=78;c.nearClipPlane=.02f;c.farClipPlane=30;g.AddComponent<UniversalAdditionalCameraData>().allowXRRendering=false;
   var rt=new RenderTexture(1400,1000,24);rt.Create();var prev=RenderTexture.active;var tex=new Texture2D(1400,1000,TextureFormat.RGB24,false);
   try{RenderPipeline.SubmitRenderRequest(c,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1400,1000),0,0);tex.Apply();File.WriteAllBytes("Documentation/HospitalTreatment/PlacementPreviews/"+name+".png",tex.EncodeToPNG());}
   finally{RenderTexture.active=prev;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(g);}
  }
 }
}
