using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RadCare.HospitalTreatment.Editor
{
    [InitializeOnLoad]
    public static class HospitalScenarioInstaller
    {
        const string Request="Temp/HospitalScenario.request";
        const string RootName="Hospital_Treatment_Scenario";
        static readonly List<HospitalInteractable> targets=new List<HospitalInteractable>();
        static TMP_FontAsset font;
        static Transform root;
        static HospitalScenarioInstaller(){EditorApplication.update+=Poll;if(SessionState.GetBool("HospitalSmoke",false))EditorApplication.update+=Smoke;}
        static void Poll() {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists(Request))return;
            string command=File.ReadAllText(Request).Trim();File.Delete(Request);
            try {
                if(command=="install")Install();
                else if(command=="test")Test();
                else if(command=="finalize")PrepareEditView();
                else if(command=="repair-grab")RepairGrab();
                else if(command=="manual-waste")InstallManualWaste();
                else if(command=="object-glow")HospitalGlowInstaller.Install();
                else if(command=="raise-bin")RaiseBin();
                else if(command=="chart-screen")InstallChartScreen();
                else if(command=="monitor-interaction")ConfigureMonitorInteraction();
                else if(command=="assistant-measurement")ConfigureAssistantMeasurement();
                else if(command=="smoke") {SessionState.SetBool("HospitalSmoke",true);EditorApplication.isPlaying=true;EditorApplication.update+=Smoke;}
                else throw new Exception("Unknown hospital scenario command");
            }catch(Exception e){File.WriteAllText("Temp/HospitalScenario-error.txt",e.ToString());Debug.LogException(e);}
        }
        static double smokeStart;
        static void Smoke() {
            if(!EditorApplication.isPlaying)return;
            if(smokeStart==0)smokeStart=EditorApplication.timeSinceStartup;
            if(EditorApplication.timeSinceStartup-smokeStart<8)return;
            EditorApplication.update-=Smoke;smokeStart=0;SessionState.SetBool("HospitalSmoke",false);
            try {
                var s=UnityEngine.Object.FindAnyObjectByType<HospitalScenario>();
                if(s==null||!s.Ready)throw new Exception("Scenario failed to initialize in Play mode");
                if(TrainingNavigation.Instance==null||TrainingNavigation.Instance.enabled)throw new Exception("Legacy keyboard navigation still active");
                if(s.input.rig.GetComponent<OVRHeadsetEmulator>().enabled)throw new Exception("Keyboard headset emulation still active");
                Capture("start");
                Capture("monitor-confirm");
                var narration=s.GetComponent<HospitalNarration>();
                if(narration==null||narration.cues.Length!=33||narration.cues.Any(c=>c.clip==null||c.clip.length<.2f))throw new Exception("Narration clips not installed");
                narration.RefreshNarration();
                int safety=0;
                while(!s.Procedure.Complete&&safety++<50) {
                    s.RefreshArmLabels();
                    narration.RefreshNarration();int voiceCount=narration.SequenceCount;narration.RefreshNarration();
                    if(narration.SequenceCount!=voiceCount)throw new Exception("Narration repeats unchanged step");
                    var glow=s.GetComponent<HospitalObjectGlow>();glow.RefreshGlow();
                    foreach(var binding in glow.bindings)foreach(var renderer in binding.renderers)if(glow.IsGlowing(renderer)) {
                        var property=new MaterialPropertyBlock();renderer.GetPropertyBlock(property,0);
                        if(property.GetColor("_EmissionColor").maxColorComponent<=0)throw new Exception("Active object has no emission");
                        if(renderer.sharedMaterials.Any(m=>!m.IsKeywordEnabled("_EMISSION")||!m.HasProperty("_EmissionColor")))throw new Exception("Missing emission material variant: "+renderer.name+" / "+string.Join(",",renderer.sharedMaterials.Select(m=>m.name+":"+m.IsKeywordEnabled("_EMISSION"))));
                    }
                    foreach(var marker in s.targets)if(marker.GetComponent<MeshRenderer>()!=null)throw new Exception("Guidance marker mesh remains");
                    foreach(var target in s.targets) {
                        var canvas=target.GetComponentInChildren<Canvas>(true);
                        if(canvas!=null&&canvas.gameObject.activeInHierarchy&&Vector3.Distance(target.transform.position,s.wound.position)<=.65f) {
                            if(canvas.transform.position.y<s.wound.position.y+.35f)throw new Exception("Arm label intersects treatment area");
                            if(Vector3.Angle(canvas.transform.forward,canvas.transform.position-s.input.rig.centerEyeAnchor.position)>.1f)throw new Exception("Arm label does not face viewer");
                        }
                    }
                    var chart=s.GetComponent<HospitalChartScreen>();chart.RefreshScreen();
                    if(chart.Display==null||chart.Revision==0)throw new Exception("Chart display not rendered");
                    int index=s.Procedure.Index;var action=s.Procedure.Current.Action;
                    if(action==HospitalAction.RespondPain&&!chart.StateLabel.Contains("통증"))throw new Exception("Chart missed pain alert");
                    if(s.Procedure.IsWashing) {
                        var tool=s.targets.First(t=>t.kind==HospitalInteraction.Irrigator);
                        if(action==HospitalAction.WashSecond){if(s.input.Held!=null)throw new Exception("Syringe still held after monitor check");if(!s.input.TryGrab(tool))throw new Exception("Cannot pick syringe up again");}
                        tool.movable.position=Vector3.up*10;s.Irrigate(tool,true,20);
                        if(s.Procedure.Index!=index||s.Procedure.WashSeconds!=0)throw new Exception("Out-of-range irrigation accepted");
                        tool.SetHeldPose(s.IrrigationPoint+Vector3.up*.2f,Quaternion.LookRotation(Vector3.up,Vector3.forward));
                        s.Irrigate(tool,true,20);
                        if(s.IrrigationAligned||s.Procedure.Index!=index||s.Procedure.WashSeconds!=0)throw new Exception("Wrong nozzle direction counted");
                        tool.SetHeldPose(s.IrrigationPoint,Quaternion.LookRotation(-s.IrrigationNormal,Vector3.forward));
                        tool.movable.position+=s.IrrigationPoint+s.IrrigationNormal*.08f-tool.tip.position;
                        s.Irrigate(tool,true,20);
                        if(s.IrrigationAligned||s.Procedure.WashSeconds!=0)throw new Exception("Remote syringe use accepted");
                        tool.movable.position+=s.IrrigationPoint-tool.tip.position;
                        s.Irrigate(tool,false,20);
                        if(!s.IrrigationAligned||s.Irrigating||s.Procedure.WashSeconds!=0)throw new Exception("Aim/trigger guide mismatch");
                        var guide=s.GetComponent<HospitalIrrigationGuide>();guide.RefreshVisual();
                        if(!guide.Visible)throw new Exception("Irrigation guide missing");
                        s.Irrigate(tool,true,.1f);guide.RefreshVisual();
                        if(!s.Irrigating||s.Procedure.WashSeconds<=0)throw new Exception("Valid nozzle aim did not count");
                        if(s.stream.enabled)throw new Exception("Contact syringe still shoots a water jet");
                        s.RefreshArmLabels();
                        Capture("irrigation-guide");
                        for(int i=0;i<110&&s.Procedure.Index==index;i++){s.Procedure.Tick(.1f);s.Irrigate(tool,true,.1f);}
                    } else {
                        if(action==HospitalAction.ClearWaste) {
                            var clickTarget=s.targets.First(t=>t.action==HospitalAction.ClearWaste&&t.kind==HospitalInteraction.Carry);
                            s.Act(clickTarget);
                            if(s.Procedure.Index!=index)throw new Exception("Cleanup advanced through UI");
                            var held=s.input.Held;
                            if(held==null)throw new Exception("Irrigator was dropped before cleanup");
                            held.movable.position=held.homePosition;
                            if(!s.input.TryCompletePlacement(.3f)||s.input.Held!=null)throw new Exception("Tool return failed");
                        }
                        if(action==HospitalAction.RespondPain) {
                            s.Act(s.targets.First(t=>t.action==action));
                            if(s.Procedure.Index!=index)throw new Exception("Pain response accepted without stopping");
                            s.Irrigate(null,false);s.Act(s.targets.First(t=>t.action==action));
                            if(s.Procedure.Index!=index)throw new Exception("Pain response accepted while holding syringe");
                            var tool=s.input.Held;if(tool==null)throw new Exception("Syringe dropped before return");
                            tool.movable.position=tool.homePosition+Vector3.up;
                            if(s.input.TryCompletePlacement(.3f))throw new Exception("Syringe returned away from cart");
                            tool.movable.position=tool.homePosition;
                            if(s.input.TryCompletePlacement(.1f))throw new Exception("Syringe returned without dwell");
                            if(!s.input.TryCompletePlacement(.16f)||s.input.Held!=null)throw new Exception("Pain pause syringe return failed");
                            if(s.input.TryGrab(tool))throw new Exception("Syringe regrabbed before monitor check");
                        }
                        s.Irrigate(null,false);
                        if(action==HospitalAction.RespondPain)s.Irrigate(s.targets.First(t=>t.kind==HospitalInteraction.Irrigator),true);
                        if(action==HospitalAction.ProtectChoice) {
                            s.Act(s.targets.First(t=>t.kind==HospitalInteraction.WrongChoice));
                            if(s.Procedure.Index!=index)throw new Exception("Wrong clinical choice advanced scenario");
                        }
                        var t=s.targets.First(t=>t.action==action&&t.kind!=HospitalInteraction.WrongChoice&&(t.kind!=HospitalInteraction.Carry||action==HospitalAction.RemoveDressing));
                        if(t.kind==HospitalInteraction.Socket) {
                            if(action==HospitalAction.SecureDressing){s.Act(t);if(s.Procedure.Index!=index)throw new Exception("Tape accepted UI click");}
                            if(action==HospitalAction.SecureDressing){var original=s.GetComponentsInChildren<Transform>(true).First(x=>x.name=="Fixation_Tape");if(Vector3.Distance(t.transform.position,original.position)>.002f)throw new Exception("Tape floats above original fitted position");}
                            var item=s.targets.First(x=>x.itemId==t.itemId&&x.kind==HospitalInteraction.Carry);
                            if(s.input.Held==null){Physics.SyncTransforms();if(s.input.FindNearTarget(item.grabCollider.bounds.center)!=item)throw new Exception("Inside-collider near grab failed "+action);if(!s.input.TryGrab(item))throw new Exception("Cannot pick up "+action);}
                            item.movable.position=t.transform.position+Vector3.up*2;
                            if(s.input.TryCompletePlacement(1)||s.input.Held!=item)throw new Exception("Dropped outside designated target");
                            if(action==HospitalAction.SecureDressing){item.movable.position=t.transform.position+Vector3.up*.08f;if(s.input.TryCompletePlacement(.3f))throw new Exception("Tape snapped before contact");}
                            item.movable.position=t.transform.position;
                            if(s.input.TryCompletePlacement(.1f)||s.input.Held!=item)throw new Exception("Placed without dwell");
                            if(!s.input.TryCompletePlacement(.16f)||s.input.Held!=null)throw new Exception("Socket rejected "+action);
                            var shape=item.movable.GetComponent<HospitalPlacementShape>();
                            if(shape!=null)for(int part=0;part<shape.parts.Length;part++)if(shape.parts[part].sharedMesh!=shape.fittedMeshes[part])throw new Exception("Fitted placement shape not applied");
                        } else if(t.kind==HospitalInteraction.Carry||t.kind==HospitalInteraction.Irrigator) {
                            if(action==HospitalAction.RemoveDressing) {
                                Physics.SyncTransforms();
                                if(t.grabCollider==null||!t.grabCollider.enabled||t.GetComponent<Collider>().enabled)throw new Exception("Dressing grab still targets UI");
                                if(!Physics.RaycastAll(t.grabCollider.bounds.center+Vector3.up*.4f,Vector3.down,.8f,~0,QueryTriggerInteraction.Collide).Any(h=>h.collider==t.grabCollider))throw new Exception("Dressing physical surface cannot be selected");
                                Vector3 markerPosition=t.transform.position,home=t.movable.position;
                                if(s.input.FindNearTarget(t.grabCollider.bounds.center)!=t)throw new Exception("Dressing near grab failed");if(!s.input.TryGrab(t))throw new Exception("Dressing grab failed");t.SetHeldPose(home+Vector3.up*.3f,Quaternion.identity);
                                if(t.transform.position!=markerPosition||t.movable.position==home||t.grabCollider.enabled||t.GetComponentInChildren<Canvas>(true).gameObject.activeSelf)throw new Exception("Dressing/UI separation failed");
                                t.movable.position=home+Vector3.up*2;
                                if(s.input.TryCompletePlacement(1)||s.input.Held!=t||!t.IsHeld)throw new Exception("Dressing hold did not latch");
                            }else {
                                Physics.SyncTransforms();if(s.input.FindNearTarget(t.grabCollider.bounds.center)!=t)throw new Exception("Syringe near grab failed");if(!s.input.TryGrab(t))throw new Exception("Tool grab failed");
                                t.movable.position=t.homePosition;
                                if(s.input.TryCompletePlacement(1)||s.input.Held!=t)throw new Exception("Tool returned before cleanup");
                            }
                        }
                        else {
                            s.RefreshArmLabels();
                            if(HospitalChartScreen.UsesMonitor(t)) {
                                var screen=s.GetComponent<HospitalChartScreen>();var head=s.input.rig.centerEyeAnchor;var previousHead=head.position;
                                head.position=screen.ScreenCenter+screen.ScreenNormal*2;
                                if(screen.PickScreen(new Ray(head.position,-screen.ScreenNormal),out _)!=null)throw new Exception("Remote monitor selection accepted");
                                head.position=screen.ScreenCenter+screen.ScreenNormal*.6f;
                                if(screen.PickScreen(new Ray(screen.ScreenCenter+screen.ScreenNormal*.3f,-screen.ScreenNormal),out _)!=null)throw new Exception("Non-button chart area accepted");
                                var point=screen.ConfirmPoint;
                                var selected=s.input.FindRayTarget(new Ray(point+screen.ScreenNormal*.3f,-screen.ScreenNormal),out _);
                                if(selected!=t)throw new Exception("Physical monitor selection failed: "+action);
                                s.Act(selected);head.position=previousHead;
                            }else {
                            var button=t.GetComponentInChildren<Canvas>(true).GetComponent<RectTransform>();
                            var corners=new Vector3[4];button.GetWorldCorners(corners);
                            var center=(corners[0]+corners[2])*.5f;
                            var normal=Vector3.Cross(corners[1]-corners[0],corners[2]-corners[0]).normalized;
                            var ray=new Ray(center-normal*.3f,normal);
                            var selected=s.input.FindRayTarget(ray,out float distance);
                            if(selected!=t)throw new Exception("Displayed UI cannot be selected: "+action);
                            s.Act(selected);
                            }
                        }
                    }
                    if(HospitalScenario.IsMeasurement(action)) {
                        if(s.Procedure.Index!=index||s.AssistantSeconds<=0)throw new Exception("Measurement did not wait for assistant");
                        var request=s.targets.First(t=>t.action==action);s.Act(request);
                        if(s.Procedure.Index!=index)throw new Exception("Repeated request skipped measurement");
                        narration.RefreshNarration();if(!narration.CurrentKey.StartsWith("assistant_wait_"))throw new Exception("Nurse measurement speech missing");
                        s.AdvanceAssistantMeasurement(6);
                        narration.RefreshNarration();if(narration.CurrentKey!=(s.Procedure.Current.Stage==1?"assistant_before":"assistant_after"))throw new Exception("Nurse report speech missing");
                        if(!s.AssistantReportReady||s.Procedure.Index!=index)throw new Exception("Assistant report confirmation missing");
                        s.Act(request);
                    }
                    if(action==HospitalAction.RecordAfter&&s.Procedure.Current.Action!=HospitalAction.ApplyDressing)throw new Exception("Redundant treatment choice remains");
                    if(s.Procedure.Index!=index+(HospitalScenario.IsMeasurement(action)?3:1))throw new Exception("Integration stuck: "+action+" "+s.Feedback);
                }
                narration.RefreshNarration();if(narration.CurrentKey!="complete")throw new Exception("Completion narration missing");
                if(!s.Procedure.Complete||!File.Exists(s.SavedPath))throw new Exception("Record not saved");
                s.GetComponent<HospitalObjectGlow>().RefreshGlow();
                foreach(var b in s.GetComponent<HospitalObjectGlow>().bindings)foreach(var r in b.renderers)if(s.GetComponent<HospitalObjectGlow>().IsGlowing(r))throw new Exception("Glow remains after completion");
                s.GetComponent<HospitalChartScreen>().RefreshScreen();
                if(!s.GetComponent<HospitalChartScreen>().StateLabel.Contains("완료"))throw new Exception("Chart missed completion");
                var chartTexture=s.GetComponent<HospitalChartScreen>().Display;var previousTexture=RenderTexture.active;RenderTexture.active=chartTexture;
                var chartImage=new Texture2D(chartTexture.width,chartTexture.height,TextureFormat.RGB24,false);chartImage.ReadPixels(new Rect(0,0,chartTexture.width,chartTexture.height),0,0);chartImage.Apply();File.WriteAllBytes("Temp/HospitalChart-complete.png",chartImage.EncodeToPNG());UnityEngine.Object.DestroyImmediate(chartImage);RenderTexture.active=previousTexture;
                Capture("monitor-screen");
                Capture("complete");
                File.WriteAllText("Temp/HospitalScenario-smoke.json",JsonUtility.ToJson(new SmokeResult{ready=s.Ready,targets=s.targets.Length,stage=s.Procedure.Index,complete=s.Procedure.Complete,record=s.SavedPath},true));
                if(File.Exists("Temp/HospitalScenario-error.txt"))File.Delete("Temp/HospitalScenario-error.txt");
            }catch(Exception e){File.WriteAllText("Temp/HospitalScenario-error.txt",e.ToString());Debug.LogException(e);}
            finally {EditorApplication.isPlaying=false;}
        }
        [Serializable] class SmokeResult {public bool ready,complete;public int targets,stage;public string record;}
        static void Capture(string name) {
            // Render-only test camera; never installed as a runtime input fallback.
            var preview=new GameObject("Hospital Test Capture");preview.hideFlags=HideFlags.HideAndDontSave;
            var camera=preview.AddComponent<Camera>();camera.enabled=false;camera.fieldOfView=75;camera.nearClipPlane=.03f;
            preview.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().allowXRRendering=false;
            camera.transform.position=new Vector3(-1.12f,1.65f,-.1f);camera.transform.LookAt(new Vector3(.15f,1.25f,-.1f));
            if(name=="irrigation-guide"){var s=UnityEngine.Object.FindAnyObjectByType<HospitalScenario>();camera.fieldOfView=55;camera.transform.position=new Vector3(-.78f,1.60f,-.10f);camera.transform.LookAt(s.IrrigationPoint+Vector3.up*.06f);}
            if(name=="monitor-screen"||name=="monitor-confirm"){var chart=UnityEngine.Object.FindAnyObjectByType<HospitalChartScreen>();camera.fieldOfView=40;camera.transform.position=chart.ScreenCenter+chart.ScreenNormal*.7f;camera.transform.LookAt(chart.ScreenCenter,chart.screen.transform.TransformDirection(chart.localUp));}
            var guideLabel=name=="irrigation-guide"?UnityEngine.Object.FindAnyObjectByType<HospitalIrrigationGuide>().GetComponentInChildren<TMP_Text>():null;
            var guideRoot=guideLabel!=null?guideLabel.transform.parent:null;
            var guideRotation=guideRoot!=null?guideRoot.rotation:Quaternion.identity;
            if(guideRoot!=null)guideRoot.rotation=Quaternion.LookRotation(guideRoot.position-camera.transform.position);
            Canvas.ForceUpdateCanvases();
            var rt=new RenderTexture(1600,1000,24);rt.Create();var previous=RenderTexture.active;
            try {
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=rt});
                RenderTexture.active=rt;var tex=new Texture2D(1600,1000,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1600,1000),0,0);tex.Apply();
                File.WriteAllBytes("Temp/HospitalScenario-"+name+".png",tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);
            }finally{if(guideRoot!=null)guideRoot.rotation=guideRotation;RenderTexture.active=previous;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(preview);}
        }
        static GameObject Find(string name) => SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).First(t=>t.name==name).gameObject;
        static GameObject Node(string name,Transform parent) {var o=new GameObject(name);o.transform.SetParent(parent,false);return o;}
        static Material Mat(string name,Color color) {
            string path="Assets/Scripts/Hospital_Treatment/Materials/"+name+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m!=null)return m;
            m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,color=color};AssetDatabase.CreateAsset(m,path);return m;
        }
        static GameObject Box(string name,Vector3 pos,Vector3 size,Color color) {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(root);g.transform.position=pos;g.transform.localScale=size;
            g.GetComponent<Renderer>().sharedMaterial=Mat(name,color);return g;
        }
        static TMP_Text Text(Transform parent,string name,Vector3 position,Vector2 size,float scale,float fontSize) {
            var go=new GameObject(name,typeof(RectTransform),typeof(Canvas));go.transform.SetParent(parent,false);
            go.transform.localPosition=position;go.transform.localScale=Vector3.one*scale;
            var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
            var rect=(RectTransform)go.transform;rect.sizeDelta=size;
            var panel=new GameObject("Background",typeof(RectTransform),typeof(Image));panel.transform.SetParent(go.transform,false);
            var image=panel.GetComponent<Image>();image.color=new Color(.025f,.07f,.10f,.93f);image.raycastTarget=false;image.rectTransform.sizeDelta=size;
            var label=new GameObject("Text",typeof(RectTransform));label.transform.SetParent(go.transform,false);
            var t=label.AddComponent<TextMeshProUGUI>();t.font=font;t.fontSize=fontSize;t.color=Color.white;t.alignment=TextAlignmentOptions.Center;t.raycastTarget=false;
            t.rectTransform.sizeDelta=size-new Vector2(16,8);return t;
        }
        static HospitalInteractable Target(GameObject go,HospitalAction action,string label,HospitalInteraction kind,string id="",Transform movable=null) {
            var t=go.AddComponent<HospitalInteractable>();t.action=action;t.label=label;t.kind=kind;t.itemId=id;t.movable=movable??go.transform;targets.Add(t);return t;
        }
        static HospitalInteractable Button(HospitalAction action,string label,Vector3 position,HospitalInteraction kind=HospitalInteraction.Action,string id="") {
            var go=Box(action+"_"+kind,position,new Vector3(.025f,.075f,.26f),kind==HospitalInteraction.Socket?new Color(.85f,.65f,.10f):new Color(.1f,.5f,.65f));
            var t=Target(go,action,label,kind,id);
            // Labels use an unscaled anchor so collider dimensions do not distort text.
            var anchor=Node("LabelAnchor",root);anchor.transform.position=position+Vector3.left*.02f;anchor.transform.rotation=Quaternion.Euler(0,90,0);
            var text=Text(anchor.transform,"ActionLabel",Vector3.zero,new Vector2(340,70),.001f,21);text.text=label;
            anchor.transform.SetParent(go.transform,true);
            return t;
        }
        static HospitalInteractable Carry(GameObject movable,string id,HospitalAction action,string label,Vector3 grabPosition) {
            var marker=Button(action,label,grabPosition,HospitalInteraction.Carry,id);marker.movable=movable.transform;
            ConfigureGrab(marker);return marker;
        }
        static void ConfigureGrab(HospitalInteractable t) {
            if(t.transform.IsChildOf(t.movable))t.transform.SetParent(t.movable.parent,true);
            foreach(var collider in t.GetComponents<Collider>())collider.enabled=false;
            var marker=t.GetComponent<Renderer>();if(marker!=null)marker.enabled=false;
            if(t.grabCollider!=null)return;
            var meshRenderers=t.movable.GetComponentsInChildren<MeshRenderer>(true);
            if(meshRenderers.Length==0)throw new Exception("No physical mesh: "+t.name);
            // Bound the mesh in the item's own coordinates; exclude the now-detached UI marker.
            var bounds=new Bounds();bool first=true;
            foreach(var r in meshRenderers) {
                var mf=r.GetComponent<MeshFilter>();if(mf==null||mf.sharedMesh==null)continue;
                var b=mf.sharedMesh.bounds;
                for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2) {
                    var p=t.movable.InverseTransformPoint(r.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z))));
                    if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);
                }
            }
            var surface=Node("Physical_Grab_Surface",t.movable);surface.AddComponent<HospitalGrabSurface>().target=t;
            var box=surface.AddComponent<BoxCollider>();box.center=bounds.center;box.size=bounds.size;box.isTrigger=true;box.enabled=false;t.grabCollider=box;
        }
        static void ConfigureAssistantMeasurement(){
            var scene=SceneManager.GetActiveScene();if(EditorApplication.isPlaying||scene.path!="Assets/Scenes/Hospital_Tutorial.unity")throw new Exception("Hospital edit mode required");
            EditorSceneManager.SaveScene(scene,"Temp/Hospital-before-assistant-measurement.unity",true);
            var s=UnityEngine.Object.FindAnyObjectByType<HospitalScenario>();var nurse=Find("Assistant_Nurse").transform;
            var rs=nurse.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.enabled).ToArray();
            var bounds=rs[0].bounds;foreach(var r in rs)bounds.Encapsulate(r.bounds);
            var glow=s.GetComponent<HospitalObjectGlow>();
            foreach(var t in s.targets.Where(t=>HospitalScenario.IsMeasurement(t.action))){
                var point=new Vector3(bounds.center.x,bounds.min.y+bounds.size.y*.74f,bounds.center.z);
                var toward=Vector3.ProjectOnPlane(s.input.rig.transform.position-point,Vector3.up).normalized;
                t.transform.position=point+toward*.3f;t.label="보조자에게 측정 요청";
                var canvas=t.GetComponentInChildren<Canvas>(true);canvas.transform.position=t.transform.position;
                canvas.transform.rotation=Quaternion.LookRotation(-toward);canvas.GetComponentInChildren<TMP_Text>(true).text=t.label;
                var binding=glow.bindings.First(b=>b.target==t);binding.renderers=rs;
            }
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            File.WriteAllText("Temp/HospitalAssistant-installed.txt","Before/after measurement request and report confirmation bound to Assistant_Nurse. Six-second simulated measurement; three readings per report.");
        }
        static void ConfigureMonitorInteraction(){
            var scene=SceneManager.GetActiveScene();if(EditorApplication.isPlaying||scene.path!="Assets/Scenes/Hospital_Tutorial.unity")throw new Exception("Hospital edit mode required");
            EditorSceneManager.SaveScene(scene,"Temp/Hospital-before-monitor-interaction.unity",true);
            var s=UnityEngine.Object.FindAnyObjectByType<HospitalScenario>();var chart=s.GetComponent<HospitalChartScreen>();
            var filter=chart.screen.GetComponent<MeshFilter>();
            var importer=AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(filter.sharedMesh)) as ModelImporter;
            if(importer!=null&&!importer.isReadable){importer.isReadable=true;importer.SaveAndReimport();}
            var mesh=UnityEngine.Object.Instantiate(filter.sharedMesh);mesh.name="Chart screen upright UV";
            var size=mesh.bounds.size;int axis=size.x<size.y?(size.x<size.z?0:2):(size.y<size.z?1:2);
            var normal=chart.screen.transform.TransformDirection(axis==0?Vector3.right:axis==1?Vector3.up:Vector3.forward).normalized;
            if(Vector3.Dot(normal,s.input.rig.transform.position-chart.screen.bounds.center)<0)normal=-normal;
            var up=Vector3.ProjectOnPlane(Vector3.up,normal).normalized;var right=Vector3.Cross(normal,up).normalized;
            var world=mesh.vertices.Select(v=>chart.screen.transform.TransformPoint(v)).ToArray();
            float minX=world.Min(v=>Vector3.Dot(v,right)),maxX=world.Max(v=>Vector3.Dot(v,right));
            float minY=world.Min(v=>Vector3.Dot(v,up)),maxY=world.Max(v=>Vector3.Dot(v,up)),front=world.Max(v=>Vector3.Dot(v,normal));
            mesh.uv=world.Select(v=>new Vector2((Vector3.Dot(v,right)-minX)/(maxX-minX),(Vector3.Dot(v,up)-minY)/(maxY-minY))).ToArray();
            string path="Assets/Scripts/Hospital_Treatment/PlacementMeshes/ChartScreenUpright.asset";
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(saved==null){AssetDatabase.CreateAsset(mesh,path);saved=mesh;}else{saved.uv=mesh.uv;EditorUtility.SetDirty(saved);UnityEngine.Object.DestroyImmediate(mesh);}
            filter.sharedMesh=saved;
            chart.localCenter=chart.screen.transform.InverseTransformPoint(right*(minX+maxX)*.5f+up*(minY+maxY)*.5f+normal*(front+.001f));
            chart.localRight=chart.screen.transform.InverseTransformDirection(right);chart.localUp=chart.screen.transform.InverseTransformDirection(up);chart.localNormal=chart.screen.transform.InverseTransformDirection(normal);chart.screenSize=new Vector2(maxX-minX,maxY-minY);
            if(Vector3.Dot(up,Vector3.up)<.7f)throw new Exception("Screen up direction invalid");
            foreach(var t in s.targets.Where(HospitalChartScreen.UsesMonitor)){foreach(var c in t.GetComponents<Collider>())c.enabled=false;t.GetComponentInChildren<Canvas>(true).gameObject.SetActive(false);}
            s.chartText.transform.parent.gameObject.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            RaiseBin();File.WriteAllText("Temp/HospitalMonitor-interaction.txt","Upright projected screen UVs; physical screen selection within 75 cm controller range and 1.2 m head range. Bin rim 85 cm.");
        }
        static void InstallChartScreen(){
            var scene=SceneManager.GetActiveScene();if(EditorApplication.isPlaying||scene.path!="Assets/Scenes/Hospital_Tutorial.unity")throw new Exception("Hospital edit mode required");
            var s=UnityEngine.Object.FindAnyObjectByType<HospitalScenario>();
            var c=s.GetComponent<HospitalChartScreen>()??s.gameObject.AddComponent<HospitalChartScreen>();c.scenario=s;
            c.screen=Find("Chart_Tablet").GetComponentsInChildren<MeshRenderer>(true).First(r=>r.name.EndsWith("tablet_black_screen"));
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            File.WriteAllText("Temp/HospitalChart-installed.txt","Live texture bound to "+c.screen.name);
        }
        static void RaiseBin(){
            var scene=SceneManager.GetActiveScene();if(EditorApplication.isPlaying||scene.path!="Assets/Scenes/Hospital_Tutorial.unity")throw new Exception("Hospital edit mode required");
            EditorSceneManager.SaveScene(scene,"Temp/Hospital-before-raised-bin.unity",true);
            var s=UnityEngine.Object.FindAnyObjectByType<HospitalScenario>();
            var bin=Find("Clinical_Waste_Container").transform;
            var renderers=bin.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled).ToArray();
            var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            float floor=bounds.min.y;
            var rim=renderers.First(r=>r.name.EndsWith("rolled_open_rim"));
            float factor=(.85f-floor)/(rim.bounds.max.y-floor);
            bin.localScale=Vector3.Scale(bin.localScale,new Vector3(1,factor,1));
            var updated=renderers[0].bounds;foreach(var r in renderers)updated.Encapsulate(r.bounds);
            bin.position+=Vector3.up*(floor-updated.min.y);
            foreach(var t in s.targets.Where(t=>t.kind==HospitalInteraction.Socket&&(t.action==HospitalAction.DisposeDressing||t.action==HospitalAction.ClearWaste)))
                t.transform.position=new Vector3(rim.bounds.center.x,rim.bounds.max.y+.04f,rim.bounds.center.z);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            File.WriteAllText("Temp/HospitalBin-height.txt","Rim: "+rim.bounds.max.y+" m; floor: "+floor+" m; both disposal targets aligned to rim.");
        }
        static void InstallManualWaste() {
            var scene=SceneManager.GetActiveScene();
            if(EditorApplication.isPlaying||scene.path!="Assets/Scenes/Hospital_Tutorial.unity")throw new Exception("Hospital edit mode required");
            EditorSceneManager.SaveScene(scene,"Temp/Hospital-before-manual-waste.unity",true);
            var s=UnityEngine.Object.FindAnyObjectByType<HospitalScenario>();
            var item=s.targets.First(t=>t.action==HospitalAction.ClearWaste&&t.kind!=HospitalInteraction.Socket);
            item.kind=HospitalInteraction.Carry;item.itemId="used-consumables";item.movable=s.wasteVisual.transform;
            item.label="오염 소모품 집기";item.transform.position=item.movable.position+Vector3.up*.12f;
            item.GetComponentInChildren<TMP_Text>(true).text=item.label;
            ConfigureGrab(item);
            var box=(BoxCollider)item.grabCollider;var bounds=HospitalGrabCalibration.LocalBounds(item.movable,box.transform);
            box.center=bounds.center;box.size=Vector3.Max(bounds.size+Vector3.one*.004f,Vector3.one*.016f);
            item.gripLocalPosition=HospitalGrabCalibration.LocalBounds(item.movable,item.movable).center;item.heldEuler=Vector3.zero;
            if(!s.targets.Any(t=>t.action==HospitalAction.ClearWaste&&t.kind==HospitalInteraction.Socket)) {
                var source=s.targets.First(t=>t.action==HospitalAction.DisposeDressing&&t.kind==HospitalInteraction.Socket);
                var socket=UnityEngine.Object.Instantiate(source,source.transform.parent);
                socket.name="Used_Consumables_Disposal";socket.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);
                socket.action=HospitalAction.ClearWaste;socket.itemId=item.itemId;socket.label="오염 소모품 폐기함";
                socket.GetComponentInChildren<TMP_Text>(true).text=socket.label;
                s.targets=s.targets.Concat(new[]{socket}).ToArray();
            }
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            File.WriteAllText("Temp/HospitalManualWaste-installed.txt","Physical consumables carry + disposal socket installed; bin position preserved.");
        }
        [MenuItem("Tools/RadCare/Repair Hospital Grab Targets")]
        public static void RepairGrab() {
            var scene=SceneManager.GetActiveScene();
            if(scene.path!="Assets/Scenes/Hospital_Tutorial.unity"||EditorApplication.isPlaying)throw new Exception("Hospital edit mode required");
            Directory.CreateDirectory("Temp/HospitalGrabBackup");EditorSceneManager.SaveScene(scene,"Temp/HospitalGrabBackup/Hospital_Tutorial.unity",true);
            var s=UnityEngine.Object.FindAnyObjectByType<HospitalScenario>();
            foreach(var t in s.targets.Where(t=>t.IsMovable))ConfigureGrab(t);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            File.WriteAllText("Temp/HospitalGrab-repair.txt","Physical grab surfaces installed; movable models contain no instruction UI.");
        }
        [MenuItem("Tools/RadCare/Install Hospital Treatment Scenario")]
        public static void Install() {
            var scene=SceneManager.GetActiveScene();
            if(scene.path!="Assets/Scenes/Hospital_Tutorial.unity"||EditorApplication.isPlaying)throw new Exception("Open Hospital_Tutorial in edit mode.");
            if(scene.GetRootGameObjects().Any(g=>g.name==RootName))throw new Exception("Scenario already installed; edit existing bindings instead.");
            Directory.CreateDirectory("Temp/HospitalScenarioBackup");
            EditorSceneManager.SaveScene(scene,"Temp/HospitalScenarioBackup/Hospital_Tutorial.unity",true);
            Directory.CreateDirectory("Assets/Scripts/Hospital_Treatment/Materials");AssetDatabase.Refresh();
            font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Font/NanumGothic SDF.asset");
            if(font==null)throw new Exception("Korean font missing");
            targets.Clear();root=Node(RootName,null).transform;
            var scenario=root.gameObject.AddComponent<HospitalScenario>();
            var input=root.gameObject.AddComponent<HospitalInput>();scenario.input=input;input.scenario=scenario;
            input.rig=UnityEngine.Object.FindAnyObjectByType<OVRCameraRig>();
            // Disable only the old grab bridge in this scene. Existing movement/hand rendering stays available.
            var oldGrab=input.rig.GetComponent<QuestTriggerGrab>();if(oldGrab!=null)oldGrab.enabled=false;
            var hud=Node("Hospital HUD",root);input.hud=hud.transform;
            scenario.statusText=Text(hud.transform,"Progress",Vector3.zero,new Vector2(880,220),.001f,23);
            input.hint=Text(hud.transform,"Controls",new Vector3(0,-.18f,0),new Vector2(900,100),.001f,18);scenario.hintText=input.hint;
            var chart=Node("Electronic Chart Display",root);chart.transform.SetPositionAndRotation(new Vector3(-.05f,1.65f,.62f),Quaternion.Euler(0,90,0));
            scenario.chartText=Text(chart.transform,"PatientRecord",Vector3.zero,new Vector2(700,460),.001f,24);
            var chartActions=new[]{HospitalAction.Start,HospitalAction.Chart,HospitalAction.RecordBefore,HospitalAction.RespondPain,HospitalAction.ClearWaste,HospitalAction.RecordAfter,HospitalAction.ReviewOrder,HospitalAction.ProtectChoice,HospitalAction.SaveChart};
            var labels=new[]{"교육 시작","인계 내용 확인","처치 전 측정 기록","중단·상태 확인","오염 소모품 수거","전후 결과 비교·기록","의료진 지시 확인","보호 드레싱·관찰 선택","최종 기록 저장"};
            for(int i=0;i<chartActions.Length;i++)Button(chartActions[i],labels[i],new Vector3(-.10f,1.31f,.62f));
            Button(HospitalAction.ProtectChoice,"계속 강하게 세척",new Vector3(-.10f,1.21f,.62f),HospitalInteraction.WrongChoice);
            Vector3 woundPos=new Vector3(-.22f,1.12f,-.38f);
            var wound=Box("Simulated_Wound_Target",woundPos,new Vector3(.065f,.008f,.09f),new Color(.55f,.16f,.16f));scenario.wound=wound.transform;
            Button(HospitalAction.AskPain,"현재 통증 질문",new Vector3(.05f,1.38f,-.92f));
            Button(HospitalAction.ExplainPlan,"관찰·보고 계획 설명",new Vector3(.05f,1.38f,-.92f));
            var old=Box("Existing_Dressing",woundPos+Vector3.up*.014f,new Vector3(.09f,.009f,.12f),new Color(.85f,.81f,.70f));scenario.oldDressing=old;
            Carry(old,"old",HospitalAction.RemoveDressing,"기존 드레싱 집기",woundPos+Vector3.up*.10f);
            Button(HospitalAction.DisposeDressing,"드레싱 폐기함",new Vector3(1.8f,.75f,1.1f),HospitalInteraction.Socket,"old");
            // A visible designated receptacle is separate from the room's decorative bin.
            Box("Clinical_Waste_Container",new Vector3(1.8f,.35f,1.1f),new Vector3(.36f,.7f,.36f),new Color(.5f,.22f,.15f));
            Button(HospitalAction.AdjustLight,"상처 방향 조절",new Vector3(.7f,1.4f,-1.1f));scenario.lamp=Find("Examination_Light").transform;
            var spot=Node("Treatment Spot Light",scenario.lamp).AddComponent<Light>();spot.type=LightType.Spot;spot.range=3;spot.intensity=2;spot.spotAngle=55;spot.transform.position=new Vector3(.5f,1.9f,-.6f);spot.enabled=false;
            Button(HospitalAction.MeasureWound,"상처 측정",woundPos+new Vector3(-.10f,.10f,0));
            Button(HospitalAction.MeasureSkin,"주변 피부 측정",woundPos+new Vector3(0,.10f,.13f));
            Button(HospitalAction.MeasureReference,"대조 부위 측정",woundPos+new Vector3(0,.10f,.30f));
            var drape=Box("Waterproof_Drape",new Vector3(-1.25f,.8f,.5f),new Vector3(.20f,.006f,.24f),new Color(.1f,.55f,.65f));
            Carry(drape,"drape",HospitalAction.PlaceDrape,"방수포 집기",drape.transform.position+Vector3.up*.09f);
            Button(HospitalAction.PlaceDrape,"방수포 배치",woundPos+new Vector3(-.12f,-.07f,0),HospitalInteraction.Socket,"drape");
            var receiver=Find("Irrigation_Receiver");scenario.receiver=receiver.transform;
            Carry(receiver,"receiver",HospitalAction.PlaceReceiver,"체액 받이 집기",receiver.transform.position+Vector3.up*.12f);
            Button(HospitalAction.PlaceReceiver,"받이 배치",woundPos+new Vector3(-.24f,-.16f,0),HospitalInteraction.Socket,"receiver");
            var syringe=Find("Needleless_Irrigation_Syringe");
            var tool=Carry(syringe,"syringe",HospitalAction.SelectSterile,"멸균 세척 도구",syringe.transform.position+Vector3.up*.09f);tool.kind=HospitalInteraction.Irrigator;
            tool.tip=Node("Irrigation Tip",syringe.transform).transform;tool.tip.localPosition=new Vector3(0,0,.08f);
            scenario.wasteVisual=Box("Used_Consumables",new Vector3(-1.34f,.79f,-.98f),new Vector3(.09f,.01f,.08f),new Color(.7f,.65f,.48f));
            var fresh=Box("Fresh_Dressing",new Vector3(-1.25f,.81f,.72f),new Vector3(.09f,.009f,.12f),Color.white);scenario.newDressing=fresh;
            Carry(fresh,"fresh",HospitalAction.ApplyDressing,"새 드레싱 집기",fresh.transform.position+Vector3.up*.09f);
            Button(HospitalAction.ApplyDressing,"새 드레싱 적용",woundPos+Vector3.up*.015f,HospitalInteraction.Socket,"fresh");
            Button(HospitalAction.SecureDressing,"고정 테이프 적용",new Vector3(-1.25f,.94f,.87f));
            scenario.tapeVisual=Box("Fixation_Tape",woundPos+Vector3.up*.023f,new Vector3(.13f,.004f,.025f),new Color(.95f,.92f,.8f));scenario.tapeVisual.SetActive(false);
            scenario.stream=Line("Irrigation_Stream",new Color(.2f,.65f,1),.006f);scenario.stream.enabled=false;
            input.pointer=Line("Controller_Pointer",new Color(.2f,1,.8f),.002f);
            scenario.targets=targets.ToArray();
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Test();File.WriteAllText("Temp/HospitalScenario-installed.txt","Installed "+targets.Count+" targets in "+scene.path);
        }
        static LineRenderer Line(string name,Color color,float width) {
            var l=Node(name,root).AddComponent<LineRenderer>();l.sharedMaterial=Mat(name,color);l.startWidth=l.endWidth=width;l.positionCount=2;l.useWorldSpace=true;return l;
        }
        static void PrepareEditView() {
            if(EditorApplication.isPlaying||SceneManager.GetActiveScene().path!="Assets/Scenes/Hospital_Tutorial.unity")throw new Exception("Hospital edit mode required");
            var s=UnityEngine.Object.FindAnyObjectByType<HospitalScenario>();
            foreach(var t in s.targets) {
                bool visible=t.action==HospitalAction.Start;
                if(t.itemId=="fresh"&&t.kind==HospitalInteraction.Carry) {
                    t.label="새 드레싱 집기";t.GetComponentInChildren<TMP_Text>(true).text=t.label;
                }
                foreach(var c in t.GetComponents<Collider>())c.enabled=visible;
                var r=t.GetComponent<Renderer>();if(r!=null)r.enabled=visible;
                var label=t.GetComponentInChildren<TMP_Text>(true);if(label!=null)label.transform.parent.gameObject.SetActive(visible);
            }
            s.chartText.text="SIM-P001 · 가상 성인 · 왼쪽 전완\n현장 초기 처치 및 임시 드레싱\n\nPlay 후 ‘교육 시작’을 선택하세요.\n7단계 · 권장 4분 30초\n측정치는 교육용 모의 데이터입니다.";
            s.statusText.text="Hospital Treatment · Play로 시작";
            s.input.pointer.enabled=false;s.stream.enabled=false;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Test();File.WriteAllText("Temp/HospitalScenario-finalized.txt","Scene saved; legacy scripts unchanged; 26 targets connected.");
        }
        [MenuItem("Tools/RadCare/Test Hospital Treatment Progression")]
        public static void Test() {
            int assertions=0;Action<bool,string> check=(ok,msg)=>{assertions++;if(!ok)throw new Exception(msg);};
            var p=new HospitalProcedure();p.Tick(30);check(p.Elapsed==0,"Timer before start");
            check(!p.Try(HospitalAction.SaveChart)&&p.Index==0,"Out of order accepted");
            while(!p.Complete) {
                var a=p.Current.Action;int index=p.Index;
                if(p.IsWashing) {
                    check(!p.Try(a),"Wash bypass");p.Irrigate(20,false);check(p.Index==index,"Invalid geometry advanced wash");
                    p.Irrigate(9,true);check(p.Index==index,"Wash completed early");p.Irrigate(1,true);
                }else check(p.Try(a),"Correct action rejected: "+a);
                check(p.Index==index+1,"Step increment");p.Tick(1);
            }
            check(p.Entries.Count==HospitalProcedure.Steps.Length,"Missing event history");
            float elapsed=p.Elapsed;p.Tick(10);check(p.Elapsed==elapsed,"Completed timer changed");check(!p.Try(HospitalAction.Start),"Completed replay accepted");
            var longRun=new HospitalProcedure();longRun.Try(HospitalAction.Start);longRun.Tick(400);check(longRun.Index==1,"Timeout skipped tasks");
            var blocked=new HospitalProcedure();check(!blocked.Irrigate(100,true)&&blocked.Index==0,"Irrigation outside wash stage");
            var scenario=UnityEngine.Object.FindAnyObjectByType<HospitalScenario>();
            check(scenario!=null&&scenario.targets.Length>=26,"Scene bindings missing");
            check(HospitalProcedure.Steps.Where(s=>s.Action!=HospitalAction.WashFirst&&s.Action!=HospitalAction.WashSecond).All(s=>scenario.targets.Any(t=>t.action==s.Action)),"An action has no scene target");
            check(scenario.targets.All(t=>t!=null&&t.GetComponent<Collider>()!=null),"Target missing collider");
            File.WriteAllText("Temp/HospitalScenario-tests.txt","PASS: "+assertions+" assertions; sequential gates, wash duration, invalid wash, completion, overtime and scene bindings.");
        }
    }
}
