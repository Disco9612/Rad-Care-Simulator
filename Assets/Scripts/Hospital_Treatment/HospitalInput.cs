using TMPro;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Input;
using System.Linq;
using UnityEngine;

namespace RadCare.HospitalTreatment
{
    public sealed class HospitalInput : MonoBehaviour
    {
        public HospitalScenario scenario;
        public OVRCameraRig rig;
        public Transform hud;
        public TMP_Text hint;
        public LineRenderer pointer;
        public HospitalInteractable Held { get; private set; }
        TrainingNavigation navigation;
        bool navigationWasEnabled, turnArmed=true, controlsArmed, menuArmed;
        float menuSeconds;
        OVRHeadsetEmulator emulator;
        bool emulatorWasEnabled;
        bool useArmed;
        float placementSeconds;
        HospitalInteractable pendingSocket;
        HandGrabInteractor[] palms;
        bool heldByLeft;
        sealed class GrabState { public bool ready,pending; }
        readonly GrabState rightGrab=new GrabState(),leftGrab=new GrabState();
        public const float NearGrabDistance=.07f;
        void Start() {
            palms=rig.GetComponentsInChildren<HandGrabInteractor>(true).Where(h=>h.Hand!=null).ToArray();
            // Disable legacy keyboard shortcuts only during this scenario, without changing their sources.
            navigation=TrainingNavigation.Instance;
            if(navigation!=null){navigationWasEnabled=navigation.enabled;navigation.enabled=false;}
            emulator=rig.GetComponent<OVRHeadsetEmulator>();
            if(emulator!=null){emulatorWasEnabled=emulator.enabled;emulator.enabled=false;}
            foreach(var canvas in rig.GetComponentsInChildren<Canvas>(true))
                if(canvas.name=="Return to Lobby")canvas.gameObject.SetActive(false);
        }
        void Update() {
            if(!scenario.Ready)return;
            Transform hand=rig.rightHandAnchor,head=rig.centerEyeAnchor;
            hud.SetPositionAndRotation(head.TransformPoint(new Vector3(0,.42f,1.35f)),head.rotation);
            if(scenario.Evaluation?.IsRestarting==true){pointer.enabled=false;hint.text=scenario.Evaluation.RetryMessage;return;}
            bool rightConnected=OVRInput.IsControllerConnected(OVRInput.Controller.RTouch);
            bool leftConnected=OVRInput.IsControllerConnected(OVRInput.Controller.LTouch);
            if(!Application.isFocused||!OVRManager.hasInputFocus||(!rightConnected&&!leftConnected)) {
                scenario.Irrigate(null,false);pointer.enabled=false;
                placementSeconds=0;pendingSocket=null;useArmed=false;
                controlsArmed=false;menuArmed=false;menuSeconds=0;turnArmed=false;
                ResetGrabStates();
                hint.text="Quest 컨트롤러 연결과 추적 상태를 확인하세요.";return;
            }
            bool trigger=OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger,OVRInput.Controller.RTouch);
            bool grip=OVRInput.Get(OVRInput.Button.PrimaryHandTrigger,OVRInput.Controller.RTouch);
            bool leftTrigger=leftConnected&&OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger,OVRInput.Controller.LTouch);
            bool leftGrip=leftConnected&&OVRInput.Get(OVRInput.Button.PrimaryHandTrigger,OVRInput.Controller.LTouch);
            if(!controlsArmed) {
                controlsArmed=!trigger&&!grip&&!leftTrigger&&!leftGrip;
                if(controlsArmed){rightGrab.ready=true;leftGrab.ready=true;}
                hint.text="트리거와 그립을 놓으면 조작을 시작합니다.";return;
            }
            bool menu=OVRInput.IsControllerConnected(OVRInput.Controller.LTouch)&&OVRInput.Get(OVRInput.Button.Start,OVRInput.Controller.LTouch);
            if(!menu){menuArmed=true;menuSeconds=0;}
            else if(menuArmed) {
                menuSeconds+=Time.unscaledDeltaTime;
                if(menuSeconds>=1.5f) {
                    menuArmed=false;menuSeconds=0;
                    var catalog=Resources.Load<TrainingCatalog>("TrainingCatalog");
                    if(navigation!=null&&catalog!=null)navigation.TryLoad(catalog.lobbyScene);
                }
            }
            float stick=OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick,OVRInput.Controller.RTouch).x;
            if(Mathf.Abs(stick)<.25f)turnArmed=true;
            else if(turnArmed&&Mathf.Abs(stick)>.7f){rig.transform.RotateAround(head.position,Vector3.up,Mathf.Sign(stick)*30);turnArmed=false;}
            if(scenario.Procedure.Complete) {
                hint.text="교육 완료 · 왼쪽 메뉴 버튼 1.5초: 로비";pointer.enabled=false;
                ShowNavigationFeedback(menu);return;
            }
            Ray ray=new Ray(hand.position,hand.forward);
            HospitalInteractable target=FindRayTarget(ray,out float nearest);
            var palm=GetPalmPosition();
            var close=Held==null?FindNearTarget(palm):null;
            if(close!=null)target=close;
            pointer.enabled=true;pointer.SetPosition(0,ray.origin);pointer.SetPosition(1,close!=null?close.grabCollider.ClosestPoint(palm):ray.GetPoint(nearest));
            hint.text=scenario.Evaluation!=null
                ? (scenario.Evaluation.RetryMessage??(target!=null?target.label:"평가 진행 중 · 장비와 차트를 직접 확인하세요."))+"\n양손 트리거/그립: 집기 · 트리거: 선택/세척 · 왼쪽 메뉴 1.5초: 로비"
                : (target!=null?target.label:"안내된 대상의 표시를 조준하세요.")+"\n트리거 또는 그립 한 번: 집기 유지 · 지정 위치: 자동 놓기\n트리거: 선택/세척 · 왼쪽 스틱: 이동 · 오른쪽 스틱: 회전 · 왼쪽 메뉴 1.5초: 로비";
            ShowNavigationFeedback(menu);
            bool click=OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger,OVRInput.Controller.RTouch);
            bool heldBefore=Held!=null;
            if(ProcessHandGrab(ray,palm,trigger,grip,false))click=false;
            if(scenario.Evaluation?.IsRestarting==true)return;
            if(leftConnected)ProcessHandGrab(new Ray(rig.leftHandAnchor.position,rig.leftHandAnchor.forward),GetPalmPosition(true),leftTrigger,leftGrip,true);
            if(scenario.Evaluation?.IsRestarting==true)return;
            if(Held!=null) {
                var owner=heldByLeft?rig.leftHandAnchor:hand;
                Held.SetGripPose(GetPalmPosition(heldByLeft),owner.rotation);
                bool useTrigger=heldByLeft?leftTrigger:trigger;
                bool useClick=heldBefore&&(heldByLeft?OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger,OVRInput.Controller.LTouch):click);
                // A held syringe owns the trigger; the same press must not also click a label behind it.
                if(UpdateHeldUse(useTrigger,useClick))click=false;
                if(scenario.Evaluation?.IsRestarting==true)return;
                TryCompletePlacement(Time.deltaTime,useTrigger);
            }else scenario.Irrigate(null,false);
            if(click&&target!=null&&(target.kind==HospitalInteraction.Action||target.kind==HospitalInteraction.WrongChoice)){rightGrab.pending=false;scenario.Act(target);}
        }
        // Resolve physical attempts before checking the expected tool. Keep a press pending when
        // the hand has not reached an item yet, as with a normal controller grab.
        public bool ProcessHandGrab(Ray ray,Vector3 palm,bool trigger,bool grip,bool left) {
            var state=left?leftGrab:rightGrab;
            if(!scenario.Ready||scenario.Procedure.Complete||scenario.Evaluation?.IsRestarting==true)return false;
            if(!trigger&&!grip){state.ready=true;state.pending=false;return false;}
            if(Held!=null){state.ready=false;state.pending=false;return false;}
            if(state.ready){state.ready=false;state.pending=true;}
            if(!state.pending)return false;
            var target=FindNearTarget(palm)??FindRayTarget(ray,out _);
            if(target==null||!target.IsMovable)return false;
            state.pending=false;
            if(TryGrab(target))heldByLeft=left;
            return true;
        }
        void ResetGrabStates(){rightGrab.ready=false;rightGrab.pending=false;leftGrab.ready=false;leftGrab.pending=false;}
        public bool UpdateHeldUse(bool pressed,bool pressedThisFrame,float deltaSeconds=-1) {
            if(Held==null){scenario.Irrigate(null,false);return false;}
            bool consumesTrigger=Held.kind==HospitalInteraction.Irrigator;
            if(!pressed)useArmed=true;
            if(consumesTrigger&&pressedThisFrame&&useArmed&&scenario.Evaluation!=null&&!scenario.Procedure.IsWashing) {
                scenario.Evaluation.RecordMistake("현재 단계에서 세척 도구 사용",scenario.Procedure.Current.Action==HospitalAction.RespondPain,Held);
                if(scenario.Evaluation.IsRestarting)return true;
            }
            // During washing, imperfect contact only pauses progress. Keep the tool in hand to adjust it.
            scenario.Irrigate(Held,useArmed&&pressed,deltaSeconds);
            return consumesTrigger;
        }
        public HospitalInteractable FindRayTarget(Ray ray,out float nearest) {
            HospitalInteractable target=null;nearest=3.5f;
            foreach(var hit in Physics.RaycastAll(ray,3.5f,~0,QueryTriggerInteraction.Collide)) {
                var candidate=hit.collider.GetComponent<HospitalInteractable>();
                var surface=hit.collider.GetComponent<HospitalGrabSurface>();
                if(surface!=null)candidate=surface.target;
                if(candidate!=null&&!HospitalChartScreen.UsesMonitor(candidate)&&candidate!=Held&&scenario.CanSelect(candidate)&&hit.distance<nearest){target=candidate;nearest=hit.distance;}
            }
            // Action labels are world-space buttons. Test their actual displayed rectangle,
            // including any position/rotation applied by the arm-label layout.
            foreach(var candidate in scenario.targets) {
                if(HospitalChartScreen.UsesMonitor(candidate))continue;
                if((candidate.kind!=HospitalInteraction.Action&&candidate.kind!=HospitalInteraction.WrongChoice)||!scenario.CanSelect(candidate))continue;
                var canvas=candidate.GetComponentInChildren<Canvas>(true);
                if(canvas==null||!canvas.gameObject.activeInHierarchy)continue;
                var rect=canvas.transform as RectTransform;
                var corners=new Vector3[4];rect.GetWorldCorners(corners);
                var plane=new Plane(corners[0],corners[1],corners[2]);
                if(!plane.Raycast(ray,out float distance)||distance>nearest)continue;
                var local=rect.InverseTransformPoint(ray.GetPoint(distance));
                if(!rect.rect.Contains(new Vector2(local.x,local.y)))continue;
                target=candidate;nearest=distance;
            }
            var chart=scenario.GetComponent<HospitalChartScreen>();
            if(chart!=null){var onScreen=chart.PickScreen(ray,out float screenDistance);if(onScreen!=null&&screenDistance<nearest){target=onScreen;nearest=screenDistance;}}
            return target;
        }
        public Vector3 GetPalmPosition(bool left=false) {
            if(palms!=null)foreach(var palm in palms)if(palm!=null&&palm.isActiveAndEnabled&&palm.Hand.Handedness==(left?Handedness.Left:Handedness.Right)&&palm.Hand.IsTrackedDataValid&&palm.PalmPoint!=null)return palm.PalmPoint.position;
            return (left?rig.leftHandAnchor:rig.rightHandAnchor).TransformPoint(new Vector3(0,-.02f,.025f));
        }
        public HospitalInteractable FindNearTarget(Vector3 palm) {
            HospitalInteractable best=null;float distance=NearGrabDistance;
            foreach(var t in scenario.targets) {
                if(!t.IsMovable||t==Held||!scenario.CanSelect(t)||t.grabCollider==null||!t.grabCollider.enabled||!t.grabCollider.gameObject.activeInHierarchy)continue;
                float d=Vector3.Distance(palm,t.grabCollider.ClosestPoint(palm));
                if(d<distance){best=t;distance=d;}
            }
            return best;
        }
        public bool TryGrab(HospitalInteractable target) {
            if(Held!=null||target==null||!target.IsMovable||!scenario.CanSelect(target))return false;
            var evaluation=scenario.Evaluation;
            if(evaluation!=null){if(!evaluation.ReportToolGrab(target))return false;}
            else if(!scenario.CanUse(target))return false;
            Held=target;useArmed=false;placementSeconds=0;pendingSocket=null;scenario.Picked(target);return true;
        }
        public bool TryCompletePlacement(float seconds,bool triggerPressed=false) {
            if(Held==null||scenario.Procedure.Complete||scenario.Evaluation?.IsRestarting==true)return false;
            HospitalInteractable socket=null;float distance=.24f;
            foreach(var s in scenario.targets) {
                float d=Vector3.Distance(s.transform.position,Held.movable.position);
                bool inOpening=s.action!=HospitalAction.SecureDressing||d<.045f;
                if(s.action==HospitalAction.ClearWaste) {
                    var offset=Held.movable.position-s.transform.position;
                    inOpening=new Vector2(offset.x,offset.z).magnitude<.12f&&offset.y<=.06f&&offset.y>=-.18f;
                }
                if(s.kind==HospitalInteraction.Socket&&scenario.CanUse(s)&&s.itemId==Held.itemId&&d<distance&&inOpening){socket=s;distance=d;}
            }
            bool returnTool=Held.kind==HospitalInteraction.Irrigator&&(scenario.Procedure.Current.Action==HospitalAction.ReturnSyringe||scenario.Procedure.Current.Action==HospitalAction.RespondPain)
                &&!triggerPressed&&Vector3.Distance(Held.movable.position,Held.homePosition)<.10f;
            if(socket==null&&!returnTool){placementSeconds=0;pendingSocket=null;return false;}
            if(pendingSocket!=socket){placementSeconds=0;pendingSocket=socket;}
            placementSeconds+=Mathf.Max(0,seconds);
            if(placementSeconds<.25f)return false;
            var item=Held;
            if(returnTool)Held.ReturnHome();
            else if(!scenario.Place(Held,socket))return false;
            Held=null;placementSeconds=0;pendingSocket=null;scenario.Irrigate(null,false);
            if(returnTool)scenario.ReportToolReturned(item);
            scenario.Refresh();return true;
        }
        void ShowNavigationFeedback(bool menu) {
            if(navigation!=null&&!string.IsNullOrEmpty(navigation.Error))hint.text=navigation.Error;
            else if(menu&&menuArmed)hint.text=$"로비로 돌아가기… {Mathf.Clamp01(menuSeconds/1.5f)*100:0}%";
        }
        void ReleaseToHome() {if(Held!=null){Held.ReturnHome();Held=null;if(scenario.Ready)scenario.Refresh();}}
        public void ResetForStepRetry() {
            if(Held!=null){Held.ReturnHome();Held=null;}
            placementSeconds=0;pendingSocket=null;useArmed=false;controlsArmed=false;
            ResetGrabStates();
            if(pointer!=null)pointer.enabled=false;
        }
        void OnDisable() {ReleaseToHome();if(scenario!=null&&scenario.stream!=null)scenario.stream.enabled=false;if(pointer!=null)pointer.enabled=false;}
        void OnDestroy() {
            if(navigation!=null)navigation.enabled=navigationWasEnabled;
            if(emulator!=null)emulator.enabled=emulatorWasEnabled;
        }
    }
}
