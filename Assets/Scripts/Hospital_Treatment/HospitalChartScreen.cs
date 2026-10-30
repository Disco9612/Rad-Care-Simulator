using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace RadCare.HospitalTreatment {
 [DefaultExecutionOrder(250)]
 public sealed class HospitalChartScreen:MonoBehaviour {
  public HospitalScenario scenario;
  public Renderer screen;
  public Vector3 localCenter,localRight,localUp,localNormal;
  public Vector2 screenSize;
  TMP_Text actionText,confirmText,alternateText;
  Image confirmBackground,alternateBackground;
  bool IsChoice=>!scenario.Procedure.Complete&&scenario.Procedure.Current.Action==HospitalAction.ProtectChoice;
  public Rect ConfirmRect(bool wrong=false)=>new Rect((IsChoice?(wrong?-235:235):0)-190,-352,380,90);
  public Vector3 ConfirmPoint=>ScreenCenter+screen.transform.TransformDirection(localRight).normalized*(ConfirmRect().center.x/1024*screenSize.x)+screen.transform.TransformDirection(localUp).normalized*(ConfirmRect().center.y/768*screenSize.y);

  public Vector3 ScreenCenter=>screen.transform.TransformPoint(localCenter);
  public Vector3 ScreenNormal=>screen.transform.TransformDirection(localNormal).normalized;
  public static bool UsesMonitor(HospitalInteractable t) {
   return t.kind==HospitalInteraction.Action||t.kind==HospitalInteraction.WrongChoice ?
    t.action==HospitalAction.Start||t.action==HospitalAction.Chart||t.action==HospitalAction.RecordBefore||t.action==HospitalAction.RespondPain||t.action==HospitalAction.RecordAfter||t.action==HospitalAction.ReviewOrder||t.action==HospitalAction.ProtectChoice||t.action==HospitalAction.SaveChart:false;
  }
  public HospitalInteractable PickScreen(Ray ray,out float distance){
   distance=0;if(screen==null||!scenario.Ready||scenario.Procedure.Complete)return null;
   if(Vector3.Distance(scenario.input.rig.centerEyeAnchor.position,ScreenCenter)>1.2f)return null;
   if(!new Plane(ScreenNormal,ScreenCenter).Raycast(ray,out distance)||distance>.75f)return null;
   var delta=ray.GetPoint(distance)-ScreenCenter;
   var right=screen.transform.TransformDirection(localRight).normalized;var up=screen.transform.TransformDirection(localUp).normalized;
   float x=Vector3.Dot(delta,right),y=Vector3.Dot(delta,up);
   if(Mathf.Abs(x)>screenSize.x*.5f||Mathf.Abs(y)>screenSize.y*.5f)return null;
   var pixel=new Vector2(x/screenSize.x*1024,y/screenSize.y*768);
   bool wrong=IsChoice&&ConfirmRect(true).Contains(pixel);
   if(!wrong&&!ConfirmRect().Contains(pixel))return null;
   foreach(var t in scenario.targets)if(UsesMonitor(t)&&scenario.CanUse(t)) {
    if((t.kind==HospitalInteraction.WrongChoice)==wrong)return t;
   }
   return null;
  }
  public RenderTexture Display {get;private set;}
  public string StateLabel {get;private set;}
  public int Revision {get;private set;}
  Camera displayCamera;GameObject displayRoot;TMP_Text title,body;Image banner;
  Material[] original,local;
  MaterialPropertyBlock block;
  string previous;float nextUpdate;
  void Start(){Initialize();}
  void Initialize(){
   if(Display!=null)return;
   block=new MaterialPropertyBlock();
   Display=new RenderTexture(1024,768,0){name="Live hospital chart",antiAliasing=1};Display.Create();
   displayRoot=new GameObject("Chart screen renderer");displayRoot.transform.position=new Vector3(10000,10000,10000);
   displayCamera=displayRoot.AddComponent<Camera>();displayCamera.enabled=false;displayCamera.orthographic=true;
   displayCamera.orthographicSize=384;displayCamera.aspect=4f/3;displayCamera.nearClipPlane=.1f;displayCamera.farClipPlane=10;
   displayCamera.clearFlags=CameraClearFlags.SolidColor;displayCamera.backgroundColor=new Color(.025f,.045f,.07f);displayCamera.cullingMask=1<<30;
   displayCamera.AdditionalCameraData().allowXRRendering=false;
   var panel=new GameObject("Chart Canvas",typeof(RectTransform),typeof(Canvas));panel.layer=30;panel.transform.SetParent(displayRoot.transform,false);
   var canvas=panel.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=displayCamera;canvas.planeDistance=1;
   displayCamera.targetTexture=Display;
   title=Text(panel.transform,"State",new Vector2(0,305),new Vector2(950,100),44);
   body=Text(panel.transform,"Record",new Vector2(0,-65),new Vector2(940,540),30);
   body.alignment=TextAlignmentOptions.TopLeft;
   var bar=new GameObject("Status stripe",typeof(RectTransform),typeof(Image));bar.layer=30;bar.transform.SetParent(panel.transform,false);
   banner=bar.GetComponent<Image>();banner.raycastTarget=false;var rect=(RectTransform)bar.transform;rect.sizeDelta=new Vector2(960,8);rect.anchoredPosition=new Vector2(0,242);
   actionText=Text(panel.transform,"Action",new Vector2(0,-224),new Vector2(960,65),26);
   actionText.color=new Color(.45f,1,.85f);
   confirmBackground=ButtonBackground(panel.transform,"Confirm background");
   alternateBackground=ButtonBackground(panel.transform,"Alternate background");
   confirmText=Text(panel.transform,"Confirm",Vector2.zero,new Vector2(380,90),40);
   alternateText=Text(panel.transform,"Alternate",Vector2.zero,new Vector2(380,90),30);
   original=screen.sharedMaterials;local=screen.materials;
   foreach(var mat in local){mat.EnableKeyword("_EMISSION");mat.SetColor("_BaseColor",Color.white);mat.SetFloat("_Smoothness",.15f);}
  }
  Image ButtonBackground(Transform parent,string name){
   var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.layer=30;go.transform.SetParent(parent,false);
   var img=go.GetComponent<Image>();img.color=new Color(.025f,.32f,.36f);img.raycastTarget=false;((RectTransform)go.transform).sizeDelta=new Vector2(380,90);return img;
  }
  TMP_Text Text(Transform parent,string name,Vector2 position,Vector2 size,float fontSize){
   var go=new GameObject(name,typeof(RectTransform));go.layer=30;go.transform.SetParent(parent,false);
   var text=go.AddComponent<TextMeshProUGUI>();text.font=scenario.chartText.font;text.fontSize=fontSize;text.color=Color.white;text.raycastTarget=false;
   text.alignment=TextAlignmentOptions.Center;text.rectTransform.sizeDelta=size;text.rectTransform.anchoredPosition=position;return text;
  }
  void LateUpdate(){if(Time.unscaledTime>=nextUpdate){nextUpdate=Time.unscaledTime+.1f;RefreshScreen();}ApplyTexture();}
  public void RefreshScreen(){
   if(scenario==null||!scenario.Ready||screen==null)return;Initialize();
   bool done=scenario.Procedure.Complete;
   bool pain=!done&&scenario.Procedure.Current.Action==HospitalAction.RespondPain;
   StateLabel=done?"처치 완료 · 기록 저장":pain?"통증 호소 · 세척 중단":scenario.Procedure.IsWashing?(scenario.Irrigating?"상처 세척 진행 중":"상처 세척 · 접촉 대기"):HospitalProcedure.Stages[scenario.Procedure.Current.Stage];
   string content=scenario.chartText.text;
   if(scenario.Procedure.IsWashing)content+=$"\n\n유효 세척  {scenario.Procedure.WashSeconds:0.0} / 10초";
   if(!done&&scenario.Procedure.Current.Action==HospitalAction.ReturnSyringe){StateLabel="주사기 반환 대기";content+="\n\n세척 완료 · 트리거를 놓고 주사기를 카트의 원래 자리에 놓으세요.";}
   string action="";
   foreach(var t in scenario.targets)if(UsesMonitor(t)&&t.kind!=HospitalInteraction.WrongChoice&&scenario.CanUse(t)){action=t.label+" · 아래 확인 버튼을 선택하세요";break;}
   if(!done&&scenario.Procedure.Current.Action==HospitalAction.ProtectChoice)action="왼쪽: 추가 세척  |  오른쪽: 보호 드레싱·관찰";
   string key=StateLabel+content+action;if(key!=previous){
    previous=key;title.text=StateLabel;body.text=content;actionText.text=action;
    bool visible=!string.IsNullOrEmpty(action);
    confirmBackground.gameObject.SetActive(visible);confirmText.gameObject.SetActive(visible);
    alternateBackground.gameObject.SetActive(visible&&IsChoice);alternateText.gameObject.SetActive(visible&&IsChoice);
    confirmBackground.rectTransform.anchoredPosition=confirmText.rectTransform.anchoredPosition=ConfirmRect().center;
    alternateBackground.rectTransform.anchoredPosition=alternateText.rectTransform.anchoredPosition=ConfirmRect(true).center;
    confirmText.text=IsChoice?"보호 드레싱 확인":"확인";confirmText.fontSize=IsChoice?30:40;alternateText.text="추가 세척 확인";
    title.color=banner.color=pain?new Color(1,.45f,.25f):done?new Color(.3f,1,.65f):new Color(.35f,.85f,1);
    Canvas.ForceUpdateCanvases();
    RenderPipeline.SubmitRenderRequest(displayCamera,new UniversalRenderPipeline.SingleCameraRequest{destination=Display});Revision++;
   }
   ApplyTexture();
  }
  void ApplyTexture(){if(Display==null)return;screen.GetPropertyBlock(block);block.SetTexture("_BaseMap",Display);block.SetTexture("_EmissionMap",Display);block.SetColor("_EmissionColor",Color.white);block.SetColor("_BaseColor",Color.white);screen.SetPropertyBlock(block);
   // Per-slot properties take precedence over renderer-wide glow properties.
   for(int i=0;i<screen.sharedMaterials.Length;i++){screen.GetPropertyBlock(block,i);block.SetTexture("_BaseMap",Display);block.SetTexture("_EmissionMap",Display);block.SetColor("_EmissionColor",Color.white);block.SetColor("_BaseColor",Color.white);screen.SetPropertyBlock(block,i);}
  }
  void OnDestroy(){if(screen!=null&&original!=null)screen.sharedMaterials=original;if(local!=null)foreach(var m in local)if(m!=null)Destroy(m);if(Display!=null){Display.Release();Destroy(Display);}if(displayRoot!=null)Destroy(displayRoot);}
 }
 static class ChartCameraExtensions{public static UniversalAdditionalCameraData AdditionalCameraData(this Camera c)=>c.gameObject.AddComponent<UniversalAdditionalCameraData>();}
}
