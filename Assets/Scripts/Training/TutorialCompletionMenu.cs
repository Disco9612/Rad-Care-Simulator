using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using RadCare.HospitalTreatment;

// Scene-owned completion menu, shared by controller and hand tutorials.
public sealed class TutorialCompletionMenu : MonoBehaviour
{
    TrainingCatalog catalog;
    TrainingNavigation navigation;
    OVRCameraRig rig;
    HospitalScenario hospital;
    GameManager decon;
    EvaluationGameManager deconEvaluation;
    bool isEvaluation, showingResults;
    TMP_Text heading, subtitle;
    FreeEvaluationRecord report;
    TMP_Text procedureRows,timelineRows,metrics;
    int procedurePage,timelinePage;
    const int RowsPerPage=4;
    int finalMistakes;
    float finalSeconds;
    TrainingMenuPointer menuPointer;
    string tutorial, evaluation;
    RectTransform canvas;
    Image[] buttons;
    Image dot;
    TMP_Text status;
    bool shown, armed, loading;
    float shownAt;
    readonly Color normal = new Color(.10f,.20f,.28f);
    readonly Color hover = new Color(.03f,.46f,.53f);

    public static string EvaluationFor(TrainingCatalog catalog, string tutorial)
    {
        if (tutorial == "Assets/Scenes/Emergency_Decon_Tutorial_Hand.unity")
            tutorial = "Assets/Scenes/Emergency_Decon_Tutorial.unity";
        return catalog.scenarios.FirstOrDefault(s => s != null && s.tutorialScene == tutorial)?.evaluationScene;
    }
    public static void Install(Scene scene, TrainingCatalog catalog, TrainingNavigation navigation)
    {
        string evaluation = EvaluationFor(catalog, scene.path);
        bool isEvaluation = catalog.scenarios.Any(s => s != null && s.evaluationScene == scene.path);
        if (string.IsNullOrEmpty(evaluation) && !isEvaluation) return;
        var root = new GameObject("Tutorial completion menu");
        SceneManager.MoveGameObjectToScene(root, scene);
        var menu = root.AddComponent<TutorialCompletionMenu>();
        menu.isEvaluation = isEvaluation;
        menu.catalog = catalog; menu.navigation = navigation;
        menu.tutorial = scene.path; menu.evaluation = evaluation;
    }
    void Start()
    {
        rig = FindAnyObjectByType<OVRCameraRig>();
        hospital = FindAnyObjectByType<HospitalScenario>();
        decon = FindAnyObjectByType<GameManager>();
        deconEvaluation = FindAnyObjectByType<EvaluationGameManager>();
        menuPointer=gameObject.AddComponent<TrainingMenuPointer>();
    }
    void Show()
    {
        if(isEvaluation){finalMistakes=hospital!=null?hospital.Evaluation.MistakeCount:deconEvaluation.MistakeCount;finalSeconds=hospital!=null?hospital.Procedure.Elapsed:deconEvaluation.ElapsedSeconds;}
        shown = true; shownAt = Time.unscaledTime; armed = false;
        if (hospital != null)
        {
            hospital.InteractionAudio?.StopAll();
            hospital.input.enabled = false;
            if (hospital.input.hud != null) hospital.input.hud.gameObject.SetActive(false);
        }
        if (decon != null && decon.mainUI != null) decon.mainUI.gameObject.SetActive(false);
        if(deconEvaluation!=null && deconEvaluation.mainUI!=null)deconEvaluation.mainUI.gameObject.SetActive(false);
        var evaluationMovement=rig.GetComponent<EvaluationControllerScene>();
        if(evaluationMovement!=null)evaluationMovement.enabled=false;
        foreach (var c in rig.GetComponentsInChildren<Canvas>(true))
            if (c.name == "Return to Lobby" || c.name == "Evaluation Status") c.gameObject.SetActive(false);
        var movement = rig.GetComponent<QuestControllerScene>();
        if (movement != null) movement.enabled = false;
        if(isEvaluation){BuildReport();Recenter();return;}
        canvas = TrainingUI.Canvas("Training complete", transform, new Vector2(780,580));
        TrainingUI.Panel(canvas,"Background",Vector2.zero,canvas.sizeDelta,new Color(.035f,.075f,.11f));
        heading=TrainingUI.Label(canvas,catalog.font,isEvaluation?"평가가 종료되었습니다.":"훈련이 종료되었습니다.",new Vector2(0,205),new Vector2(730,70),40);
        subtitle=TrainingUI.Label(canvas,catalog.font,"다음 활동을 선택하세요.",new Vector2(0,140),new Vector2(710,45),24);
        string[] names = {"메인 로비로 돌아가기",isEvaluation?"평가 결과 보기":"평가씬 시작하기","다시하기"};
        buttons = new Image[3];
        for (int i=0;i<3;i++)
        {
            buttons[i]=TrainingUI.Panel(canvas,names[i],new Vector2(0,65-i*90),new Vector2(650,72),normal);
            TrainingUI.Label(buttons[i].transform,catalog.font,names[i],Vector2.zero,new Vector2(620,65),30);
        }
        status=TrainingUI.Label(canvas,catalog.font,"버튼을 가리킨 뒤 트리거 또는 검지 핀치로 선택",new Vector2(0,-225),new Vector2(730,65),22);
        dot=TrainingUI.Panel(canvas,"Pointer",Vector2.zero,new Vector2(10,10),new Color(.4f,1,.9f));
        Recenter();
    }
    EvaluationReportView reportView;
    void BuildReport(){
        if(report==null)report=hospital!=null?hospital.Evaluation.Session.Record:deconEvaluation.Session.Record;
        reportView=gameObject.AddComponent<EvaluationReportView>();
        reportView.Initialize(report,catalog.font);
        canvas=reportView.Canvas;buttons=reportView.Buttons;dot=reportView.Pointer;status=reportView.Status;
    }
    void RefreshReport(){reportView.Refresh(procedurePage,timelinePage);}
    public static string FormatDuration(float seconds)
    {
        int total=Mathf.FloorToInt(Mathf.Max(0,seconds));
        return $"{total/60}분 {total%60:00}초";
    }
    void SetResults(bool visible)
    {
        showingResults=visible;
        heading.text=visible?"평가 결과":"평가가 종료되었습니다.";
        subtitle.text=visible?$"틀린 횟수: {finalMistakes}회\n진행 시간: {FormatDuration(finalSeconds)}":"다음 활동을 선택하세요.";
        subtitle.rectTransform.anchoredPosition=new Vector2(0,visible?65:140);
        subtitle.rectTransform.sizeDelta=new Vector2(710,visible?160:45);
        subtitle.fontSize=visible?34:24;
        buttons[0].rectTransform.anchoredPosition=new Vector2(0,visible?-115:65);
        buttons[0].GetComponentInChildren<TMP_Text>().text=visible?"돌아가기":"메인 로비로 돌아가기";
        buttons[1].gameObject.SetActive(!visible);buttons[2].gameObject.SetActive(!visible);
        armed=false;
    }
    void Recenter()
    {
        var head=rig.centerEyeAnchor;
        var facing=Quaternion.Euler(0,head.eulerAngles.y,0);
        canvas.SetPositionAndRotation(head.position+facing*new Vector3(0,0,1.4f),facing);
    }
    void Update()
    {
        if (rig == null) return;
        if (!shown)
        {
            if ((hospital != null && hospital.Procedure.Complete)
                || (decon != null && decon.currentStep == ScenarioStep.End)
                || (deconEvaluation != null && deconEvaluation.currentStep == EvaluationStep.End)) Show();
            return;
        }
        if (loading || navigation == null || navigation.IsLoading) return;
        bool valid=menuPointer.Read(rig);
        if(!valid){armed=false;dot.gameObject.SetActive(false);return;}
        if(menuPointer.Recovered){armed=false;Recenter();}
        if (OVRInput.GetDown(OVRInput.Button.Four)) Recenter();
        bool pressed=menuPointer.Pressed;
        int selected=-1;
        dot.gameObject.SetActive(false);
        if (valid)
        {
            var ray=menuPointer.Ray;
            menuPointer.Draw(2f);
            if (new Plane(canvas.forward,canvas.position).Raycast(ray,out float distance) && distance>0 && distance<5)
            {
                menuPointer.Draw(distance);
                Vector3 point=ray.GetPoint(distance);
                var local=canvas.InverseTransformPoint(point);
                if(canvas.rect.Contains(new Vector2(local.x,local.y)))
                { dot.gameObject.SetActive(true);dot.rectTransform.anchoredPosition=new Vector2(local.x,local.y); }
                for(int i=0;i<buttons.Length;i++)
                {
                    var p=buttons[i].rectTransform.InverseTransformPoint(point);
                    if((reportView==null||reportView.CanSelect(i)) && buttons[i].gameObject.activeSelf && buttons[i].rectTransform.rect.Contains(new Vector2(p.x,p.y)))selected=i;
                }
            }
        }
        if(reportView!=null)reportView.SetHover(selected);
        else for(int i=0;i<buttons.Length;i++)buttons[i].color=selected==i?hover:normal;
        if (!pressed) { if(Time.unscaledTime-shownAt>.5f)armed=true;return; }
        if (!armed)return;
        armed=false;
        if (selected<0)return;
        if(isEvaluation){
            if(selected>=2){if(selected==2)procedurePage=Mathf.Max(0,procedurePage-1);if(selected==3)procedurePage=Mathf.Min((report.procedures.Length-1)/RowsPerPage,procedurePage+1);if(selected==4)timelinePage=Mathf.Max(0,timelinePage-1);if(selected==5)timelinePage=Mathf.Min(Mathf.Max(0,(report.timeline.Count-1)/RowsPerPage),timelinePage+1);RefreshReport();return;}
            loading=navigation.TryLoad(selected==0?catalog.lobbyScene:tutorial);status.text=loading?"이동 중입니다…":navigation.Error;return;
        }
        if(showingResults){SetResults(false);return;}
        if(isEvaluation && selected==1){SetResults(true);return;}
        string destination=selected==0?catalog.lobbyScene:selected==1?evaluation:tutorial;
        loading=navigation.TryLoad(destination);
        status.text=loading?"이동 중입니다…":navigation.Error;
    }
}
