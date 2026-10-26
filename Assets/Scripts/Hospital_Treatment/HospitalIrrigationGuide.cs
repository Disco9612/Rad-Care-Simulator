using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace RadCare.HospitalTreatment {
    public sealed class HospitalIrrigationGuide : MonoBehaviour {
        public HospitalScenario scenario;
        public Material tutorialMaterial;
        LineRenderer ring,aimLine;
        TMP_Text label;
        Transform labelRoot;
        Material glow;
        public bool Visible => ring!=null&&ring.enabled;
        void Start() {
            glow=tutorialMaterial!=null?new Material(tutorialMaterial):new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            glow.name="Hospital tutorial glow";
            ring=Line("Irrigation_Target_Glow",.004f);ring.loop=true;ring.positionCount=64;
            aimLine=Line("Irrigation_Nozzle_Aim",.002f);aimLine.positionCount=2;
            var textObject=new GameObject("Irrigation_Status",typeof(RectTransform),typeof(Canvas));textObject.transform.SetParent(transform,false);
            labelRoot=textObject.transform;labelRoot.localScale=Vector3.one*.001f;
            textObject.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
            ((RectTransform)labelRoot).sizeDelta=new Vector2(650,100);
            var panel=new GameObject("Background",typeof(RectTransform),typeof(Image));panel.transform.SetParent(labelRoot,false);
            panel.GetComponent<Image>().color=new Color(.025f,.05f,.07f,.9f);panel.GetComponent<Image>().raycastTarget=false;
            ((RectTransform)panel.transform).sizeDelta=new Vector2(650,100);
            var text=new GameObject("Text",typeof(RectTransform));text.transform.SetParent(labelRoot,false);
            label=text.AddComponent<TextMeshProUGUI>();label.font=scenario.statusText.font;
            label.fontSize=26;label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;
            label.rectTransform.sizeDelta=new Vector2(630,90);
        }
        LineRenderer Line(string name,float width) {
            var go=new GameObject(name);go.transform.SetParent(transform,false);
            var line=go.AddComponent<LineRenderer>();line.sharedMaterial=glow;line.useWorldSpace=true;line.widthMultiplier=width;
            line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;line.receiveShadows=false;line.enabled=false;return line;
        }
        void LateUpdate() { RefreshVisual(); }
        public void RefreshVisual() {
            if(ring==null)return;
            bool show=scenario.Ready&&!scenario.Procedure.Complete&&(scenario.Procedure.IsWashing||scenario.Procedure.Current.Action==HospitalAction.RespondPain);
            ring.enabled=show;labelRoot.gameObject.SetActive(show);aimLine.enabled=false;if(!show)return;
            bool pain=scenario.Procedure.Current.Action==HospitalAction.RespondPain;
            bool running=scenario.Irrigating&&!pain;
            Color color=pain?new Color(1,.3f,.2f):running?new Color(.2f,1,.3f):scenario.IrrigationAligned?Color.cyan:new Color(.65f,.9f,1);
            glow.SetColor("_BaseColor",color);ring.startColor=ring.endColor=color;
            ring.widthMultiplier=.0035f*(1+.15f*Mathf.Sin(Time.unscaledTime*4));
            var center=scenario.IrrigationPoint;var rotation=Quaternion.FromToRotation(Vector3.up,scenario.IrrigationNormal);
            for(int i=0;i<64;i++){float angle=i*Mathf.PI*2/64;ring.SetPosition(i,center+rotation*new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*HospitalScenario.IrrigationRadius);}
            var held=scenario.input.Held;
            label.text=pain?"세척 중단 · 통증 확인":running?$"세척 중 {scenario.Procedure.WashSeconds:0.0} / 10초":scenario.IrrigationAligned?"접촉 확인 · 트리거를 누르세요":"분출구를 상처의 발광 부위에 대세요";
            label.color=color;
            var head=scenario.input.rig.centerEyeAnchor;
            var toward=Vector3.ProjectOnPlane(head.position-center,Vector3.up).normalized;
            labelRoot.position=center+Vector3.up*.36f+toward*.22f;
            labelRoot.rotation=Quaternion.LookRotation(labelRoot.position-head.position);
        }
        void OnDestroy(){if(glow!=null)Destroy(glow);}
    }
}
