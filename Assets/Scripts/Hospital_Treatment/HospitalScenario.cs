using System;
using System.IO;
using TMPro;
using UnityEngine;

namespace RadCare.HospitalTreatment
{
    public sealed class HospitalScenario : MonoBehaviour
    {
        public TMP_Text statusText, chartText, hintText;
        public HospitalInteractable[] targets;
        public Transform wound, receiver, lamp;
        public Transform irrigationTarget;
        public const float IrrigationRange=.015f, IrrigationRadius=.035f;
        public bool IrrigationAligned { get; private set; }
        public bool Irrigating { get; private set; }
        public Vector3 IrrigationPoint => irrigationTarget!=null?irrigationTarget.position:wound.position;
        public Vector3 IrrigationNormal => irrigationTarget!=null?irrigationTarget.up:Vector3.up;
        public GameObject oldDressing, newDressing, wasteVisual, tapeVisual;
        public LineRenderer stream;
        public HospitalInput input;
        public HospitalEvaluation Evaluation => GetComponent<HospitalEvaluation>();
        public HospitalProcedure Procedure { get; private set; } = new HospitalProcedure();
        public string Feedback { get; private set; } = "가상의 교육 사례입니다. 측정치는 임상 판단 기준이 아닙니다.";
        public string SavedPath { get; private set; }
        public bool ReceiverPlaced { get; private set; }
        bool drapePlaced, paused=true;
        public float AssistantSeconds {get;private set;}
        public bool AssistantReportReady {get;private set;}
        public static bool IsMeasurement(HospitalAction action)=>action==HospitalAction.MeasureWound||action==HospitalAction.MeasureSkin||action==HospitalAction.MeasureReference;
        public string AssistantMessage=>AssistantSeconds>0?"보조자: 세 부위를 측정하고 있습니다…":AssistantReportReady?(Procedure.Current.Stage==1?"보조자: 상처 120 · 주변 90 · 대조 30입니다. 결과를 확인해 주세요.":"보조자: 상처 35 · 주변 28 · 대조 12입니다. 결과를 확인해 주세요."):"보조자에게 측정을 요청하세요.";
        public void AdvanceAssistantMeasurement(float seconds){
            if(AssistantSeconds<=0)return;
            AssistantSeconds=Mathf.Max(0,AssistantSeconds-seconds);
            if(AssistantSeconds==0){AssistantReportReady=true;Feedback=AssistantMessage;Refresh();}
        }
        void AskAssistant(){
            if(AssistantSeconds>0){Feedback=AssistantMessage;return;}
            if(!AssistantReportReady){AssistantSeconds=6;Feedback=AssistantMessage;Refresh();return;}
            AssistantReportReady=false;
            for(int i=0;i<3&&!Procedure.Complete&&IsMeasurement(Procedure.Current.Action);i++)Accept(Procedure.Current.Action);
            Feedback="보조자의 모의 측정 결과를 인계받았습니다. 모니터에서 기록을 확인하세요.";Refresh();
        }
        float[] before=new float[3],after=new float[3];
        string pain="미확인",order="미확인";
        public bool Ready { get; private set; }
        [Serializable] public sealed class Record {
            public string scenario="Hospital Treatment", caseId="SIM-P001", site="왼쪽 전완 모의 상처";
            public string dataNotice="Fictional simulation units; not clinical thresholds or a validated medical protocol.";
            public float duration; public float[] before,after;
            public string patientResponse,plan; public HospitalProcedure.Entry[] actions;
        }
        void Start() {
            if(statusText==null||chartText==null||input==null||wound==null||receiver==null||targets==null||targets.Length==0) {
                Debug.LogError("Hospital scenario bindings are incomplete.",this);enabled=false;return;
            }
            Ready=true;chartText.transform.parent.gameObject.SetActive(false);Refresh();
        }
        void Update() {
            Procedure.Tick(Time.deltaTime);
            AdvanceAssistantMeasurement(Time.deltaTime);
            RefreshStatus();
        }
        void LateUpdate() { RefreshArmLabels(); }
        public void RefreshArmLabels() {
            if(!Ready||input.rig==null)return;
            var head=input.rig.centerEyeAnchor;
            int row=0;
            foreach(var target in targets) {
                // Move only the presentation, leaving physical targets and grab points in place.
                if(Vector3.Distance(target.transform.position,wound.position)>.65f)continue;
                var canvas=target.GetComponentInChildren<Canvas>(true);
                if(canvas==null||!canvas.gameObject.activeInHierarchy)continue;
                var toward=Vector3.ProjectOnPlane(head.position-wound.position,Vector3.up).normalized;
                canvas.transform.position=wound.position+Vector3.up*(.36f+.1f*row++)+toward*.22f;
                canvas.transform.rotation=Quaternion.LookRotation(canvas.transform.position-head.position);
            }
        }
        void RefreshStatus() {
            if(statusText!=null) {
                if(Evaluation!=null){statusText.text=Evaluation.Status;return;}
                string time=TimeSpan.FromSeconds(Procedure.Elapsed).ToString(@"mm\:ss");
                statusText.text=Procedure.Complete?$"처치 완료 · {time}\n{Feedback}":
                    $"{Procedure.Current.Stage+1}/7  {HospitalProcedure.Stages[Procedure.Current.Stage]}  ·  {time}\n권장 구간 {HospitalProcedure.Windows[Procedure.Current.Stage]} / 전체 4:30\n{Procedure.Current.Prompt}\n{(Procedure.IsWashing?$"세척 {Procedure.WashSeconds:0.0} / 10초":"")}\n{Feedback}";
            }
        }
        public bool CanUse(HospitalInteractable target) {
            if(!Ready||target==null||target.retired||Procedure.Complete||Evaluation?.IsRestarting==true)return false;
            if(target.kind==HospitalInteraction.WrongChoice)return false;
            if(target.action==HospitalAction.RespondPain&&input.Held!=null)return false;
            if(target.kind==HospitalInteraction.Irrigator)return Procedure.Current.Action==HospitalAction.SelectSterile||Procedure.IsWashing||Procedure.Current.Action==HospitalAction.ReturnSyringe;
            if(target.itemId=="old"&&target.kind==HospitalInteraction.Carry&&Procedure.Current.Action==HospitalAction.DisposeDressing)return true;
            return target.action==Procedure.Current.Action;
        }
        public bool CanSelect(HospitalInteractable target) {
            if(!Ready||target==null||target.retired||Procedure.Complete||Evaluation?.IsRestarting==true)return false;
            if(Evaluation!=null&&target.kind==HospitalInteraction.Socket)return false;
            // Evaluation accepts attempts to grab any available tool so incorrect choices can be scored.
            // Confirmation labels still belong only to the current action.
            return (Evaluation!=null&&target.IsMovable&&Procedure.Started)||CanUse(target);
        }
        public void RestartCurrentStep() {
            if(!Ready||Procedure.Complete)return;
            input.ResetForStepRetry();
            Procedure.RestartCurrentStep();
            AssistantSeconds=0;AssistantReportReady=false;
            paused=true;IrrigationAligned=false;Irrigating=false;
            if(stream!=null)stream.enabled=false;
            Feedback="잘못된 행동으로 현재 단계를 다시 시작합니다.";
            GetComponent<HospitalNarration>()?.ResetForStepRetry();
            Refresh();
        }
        public void Act(HospitalInteractable target) {
            if(!Ready||Procedure.Complete||Evaluation?.IsRestarting==true)return;
            if(Evaluation!=null&&target!=null&&target.kind==HospitalInteraction.WrongChoice){Evaluation.RecordMistake("잘못된 치료 선택",true);Feedback="선택을 다시 검토하세요.";return;}
            if(!CanUse(target)){Evaluation?.RecordMistake("순서에 맞지 않는 대상 선택",false);Feedback="현재 안내된 행동을 먼저 완료하세요.";return;}
            if(target.action==HospitalAction.SecureDressing){Feedback="테이프를 직접 집어 드레싱 위에 붙이세요.";return;}
            if(IsMeasurement(target.action)){AskAssistant();return;}
            if(target.kind==HospitalInteraction.WrongChoice){Evaluation?.RecordMistake("잘못된 치료 선택",true);Feedback="모의 지시는 보호 드레싱과 관찰입니다. 지시를 다시 확인하세요.";return;}
            if(target.action==HospitalAction.ClearWaste){Evaluation?.RecordMistake("폐기물 직접 수거 누락",false);Feedback="세척 도구를 카트에 놓고 오염 소모품을 직접 집어 폐기함 안에 넣으세요.";return;}
            if(target.action==HospitalAction.RespondPain&&!paused){Evaluation?.RecordMistake("통증 호소 중 세척 지속",true);Feedback="트리거를 놓아 세척을 중단한 뒤 상태를 확인하세요.";return;}
            if(target.action==HospitalAction.SaveChart&&!SaveRecord())return;
            Accept(target.action);
        }
        public void Picked(HospitalInteractable target) {
            target.SetHeld(true);
            if(target.action==HospitalAction.RemoveDressing||target.action==HospitalAction.SelectSterile)Accept(target.action);
            else Feedback="발광하는 목표 오브젝트에 잠시 가져가면 자동으로 놓습니다.";
            Refresh();
        }
        public void ReportToolReturned(HospitalInteractable tool) {
            if(!Ready||Procedure.Complete||Evaluation?.IsRestarting==true||input.Held!=null)return;
            if(tool!=null&&tool.kind==HospitalInteraction.Irrigator&&Procedure.Current.Action==HospitalAction.ReturnSyringe
                &&Vector3.Distance(tool.movable.position,tool.homePosition)<.001f)Accept(HospitalAction.ReturnSyringe);
        }
        public bool Place(HospitalInteractable item,HospitalInteractable socket) {
            if(!CanUse(socket)||socket.kind!=HospitalInteraction.Socket||item.itemId!=socket.itemId)return false;
            item.movable.SetPositionAndRotation(socket.transform.position,socket.transform.rotation);
            var shape=item.movable.GetComponent<HospitalPlacementShape>();
            if(shape!=null)shape.Apply();
            item.retired=true;
            item.SetHeld(false);
            if(socket.action==HospitalAction.DisposeDressing||socket.action==HospitalAction.ClearWaste)item.movable.gameObject.SetActive(false);
            if(socket.action==HospitalAction.PlaceReceiver)ReceiverPlaced=true;
            if(socket.action==HospitalAction.PlaceDrape)drapePlaced=true;
            Accept(socket.action);return true;
        }
        public void Irrigate(HospitalInteractable item,bool pressed,float deltaSeconds=-1) {
            if(Evaluation?.IsRestarting==true){Irrigating=false;IrrigationAligned=false;stream.enabled=false;return;}
            // Selecting the pain response also uses the trigger. Remember the intervening release.
            if(!Procedure.Complete&&Procedure.Current.Action==HospitalAction.RespondPain) {if(!pressed)paused=true;}
            else paused=!pressed;
            IrrigationAligned=IsIrrigationAligned(item);
            bool valid=pressed&&IrrigationAligned;
            Irrigating=valid;
            stream.enabled=false;
            if(valid) {
                if(Procedure.Irrigate(deltaSeconds<0?Time.deltaTime:deltaSeconds,true)) {
                    Feedback=Procedure.Current.Action==HospitalAction.RespondPain?"환자: ‘통증이 심해졌어요.’ 주사기를 카트에 내려놓고 모니터에서 상태를 확인하세요.":"세척 완료. 트리거를 놓고 주사기를 카트의 원래 자리에 내려놓으세요.";
                    if(Procedure.Current.Action==HospitalAction.RespondPain)pain="초기 5/10 → 세척 중 7/10 (모의 응답)";
                    Refresh();
                }
            } else if(pressed&&Procedure.IsWashing)Feedback="주사기 분출구를 상처의 발광 부위에 대세요. 청록색 접촉 표시가 켜진 상태에서 트리거를 누르세요.";
        }
        public bool IsIrrigationAligned(HospitalInteractable item) {
            if(item==null||item.kind!=HospitalInteraction.Irrigator||item.tip==null||!Procedure.IsWashing||!drapePlaced||!ReceiverPlaced)return false;
            if(Vector3.Distance(receiver.position,wound.position)>=.55f||receiver.position.y>=wound.position.y)return false;
            var offset=item.tip.position-IrrigationPoint;
            float depth=Vector3.Dot(offset,IrrigationNormal);
            return Mathf.Abs(depth)<=IrrigationRange&&Vector3.ProjectOnPlane(offset,IrrigationNormal).magnitude<=IrrigationRadius
                &&Vector3.Dot(item.tip.forward,IrrigationNormal)<-.25f;
        }

        void Accept(HospitalAction action) {
            int stage=Procedure.Current.Stage;
            if(!Procedure.Try(action))return;
            Feedback="확인했습니다.";
            if(action==HospitalAction.AskPain)pain="초기 통증 5/10 (모의 응답)";
            if(action==HospitalAction.RespondPain){pain="5/10 → 7/10 → 중단·확인 후 4/10 (모의 응답)";Feedback="모의 의료진 지시: 상태 확인 후 부드럽게 세척 재개.";}
            if(action==HospitalAction.AdjustLight) {
                lamp.rotation=Quaternion.Euler(0,Vector3.SignedAngle(Vector3.forward,Vector3.ProjectOnPlane(wound.position-lamp.position,Vector3.up),Vector3.up),0);
                var light=lamp.GetComponentInChildren<Light>();if(light!=null){light.enabled=true;light.transform.LookAt(wound);}
            }
            if(action==HospitalAction.MeasureWound||action==HospitalAction.MeasureSkin||action==HospitalAction.MeasureReference) {
                int i=action==HospitalAction.MeasureWound?0:action==HospitalAction.MeasureSkin?1:2;
                (stage==1?before:after)[i]=(stage==1?new float[]{120,90,30}:new float[]{35,28,12})[i];
                Feedback=$"{new[]{"상처","주변 피부","대조 부위"}[i]}: {(stage==1?before:after)[i]} 모의 단위. 차트에서 기록을 확인하세요.";
            }
            if(action==HospitalAction.ClearWaste)wasteVisual.SetActive(false);
            if(action==HospitalAction.RecordAfter)order="모의 지시: 추가 세척 보류. 보호 드레싱 적용 후 통증·피부 상태를 관찰하고 변화 시 보고.";
            if(action==HospitalAction.RecordAfter)Feedback="재평가 결과와 모의 지시를 기록했습니다. 새 드레싱을 집어 상처를 보호하세요.";
            if(action==HospitalAction.SecureDressing)tapeVisual.SetActive(true);
            if(Procedure.Complete){Feedback="차트 기록이 저장되었습니다. 로비로 돌아가 교육을 마칠 수 있습니다.";Debug.Log("Hospital treatment record: "+SavedPath,this);}
            Refresh();
        }
        bool SaveRecord() {
            try {
                var entries=new System.Collections.Generic.List<HospitalProcedure.Entry>(Procedure.Entries);
                entries.Add(new HospitalProcedure.Entry{action="SaveChart",stage=6,seconds=Procedure.Elapsed});
                var record=new Record {duration=Procedure.Elapsed,before=before,after=after,patientResponse=pain,plan=order,actions=entries.ToArray()};
                string dir=Path.Combine(Application.persistentDataPath,"HospitalTreatmentRecords");Directory.CreateDirectory(dir);
                SavedPath=Path.Combine(dir,"SIM-P001-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+".json");
                File.WriteAllText(SavedPath,JsonUtility.ToJson(record,true));return true;
            } catch(Exception e) {Feedback="기록 저장 실패. 저장 버튼으로 다시 시도하세요: "+e.Message;Debug.LogWarning(Feedback,this);return false;}
        }
        public void Refresh() {
            if(!Ready)return;
            foreach(var t in targets) {
                bool active=CanUse(t)&&!t.IsHeld;
                bool selectable=CanSelect(t)&&!t.IsHeld;
                foreach(var c in t.GetComponents<Collider>())c.enabled=selectable&&!t.IsMovable&&!HospitalChartScreen.UsesMonitor(t);
                if(t.grabCollider!=null)t.grabCollider.enabled=selectable;
                var label=t.GetComponentInChildren<TMP_Text>(true);
                if(IsMeasurement(t.action)){t.label=AssistantSeconds>0?"보조자 · 측정 중…":AssistantReportReady?"보조자 측정 결과 확인":"보조자에게 측정 요청";if(label!=null)label.text=AssistantReportReady?(Procedure.Current.Stage==1?"상처 120 · 주변 90 · 대조 30\n모의 측정 결과 확인":"상처 35 · 주변 28 · 대조 12\n모의 측정 결과 확인"):t.label;}
                if(label!=null)label.transform.parent.gameObject.SetActive(active&&!HospitalChartScreen.UsesMonitor(t));
                var renderer=t.GetComponent<Renderer>();if(renderer!=null)renderer.enabled=false;
            }
            if(!Procedure.Complete&&Procedure.Current.Action==HospitalAction.SecureDressing)tapeVisual.SetActive(true);
            chartText.text="SIM-P001 · 가상 성인 · 왼쪽 전완\n현장 이력: 초기 세척 및 임시 드레싱\n알레르기·처방: 모의 인계 확인 완료\n"+
                $"통증: {pain}\n모의 단위 (임상 기준 아님)\n             상처 / 주변 / 대조\n처치 전: {before[0]} / {before[1]} / {before[2]}\n처치 후: {after[0]} / {after[1]} / {after[2]}\n{order}";
            RefreshStatus();
        }
    }
}
