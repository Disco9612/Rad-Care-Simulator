using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Shared presentation only: evaluation scoring and recorded events remain unchanged.
public sealed class EvaluationReportView : MonoBehaviour
{
    public RectTransform Canvas {get;private set;}
    public Image[] Buttons {get;private set;}
    public Image Pointer {get;private set;}
    public TMP_Text Status {get;private set;}
    const int PageSize=4;
    readonly Color surface=new Color(.105f,.16f,.245f), rowColor=new Color(.13f,.19f,.28f);
    readonly Color muted=new Color(.65f,.74f,.85f), green=new Color(.25f,.88f,.64f), amber=new Color(1,.76f,.34f), coral=new Color(1,.55f,.47f), teal=new Color(.1f,.8f,.75f);
    TMP_FontAsset font;
    FreeEvaluationRecord record;
    Sprite rounded;
    Texture2D texture;
    TMP_Text leftPage,rightPage,empty;
    Row[] procedures=new Row[PageSize],events=new Row[PageSize];
    readonly bool[] enabledButtons=new bool[6];
    sealed class Row {public Image panel,badge,accent,node;public TMP_Text number,title,state,time,detail;}
    public void Initialize(FreeEvaluationRecord data,TMP_FontAsset typeface)
    {
        record=data;font=typeface;CreateSprite();
        Canvas=TrainingUI.Canvas("Free evaluation results",transform,new Vector2(1440,980));Canvas.localScale=Vector3.one*.00112f;
        Card(Canvas,"Background",Vector2.zero,Canvas.sizeDelta,new Color(.025f,.045f,.085f));
        Label(Canvas,"RAD CARE  ·  자유 수행 평가",new Vector2(-370,452),new Vector2(620,30),21,muted);
        Label(Canvas,"훈련 평가 결과",new Vector2(-370,405),new Vector2(620,62),44,Color.white);
        Label(Canvas,record.scenario=="Hospital"?"병원 상처 처치":"응급 제염",new Vector2(480,413),new Vector2(400,42),26,muted,TextAlignmentOptions.Right);
        Metric(-466,"총 수행 시간",TutorialCompletionMenu.FormatDuration(record.seconds),"시간 표기: 분 · 초",Color.white);
        Metric(0,"절차 수행률",$"{record.CompletedCount} / {record.procedures.Length}",$"{record.CompletionPercent:0}% · 중복 수행 제외",green);
        Metric(466,"순서 오류",$"{record.OrderErrorCount}회",record.OrderErrorCount>0?"재수행해도 최초 오류는 유지됩니다":"기록된 순서 오류가 없습니다",record.OrderErrorCount>0?coral:green);
        Card(Canvas,"Progress track",new Vector2(0,217),new Vector2(390,6),new Color(.25f,.33f,.44f));
        float progress=390*Mathf.Clamp01(record.CompletionPercent/100);if(progress>0)Card(Canvas,"Completion progress",new Vector2(-195+progress/2,217),new Vector2(progress,6),teal);
        for(int side=0;side<2;side++){
            float x=side==0?-350:350;
            Card(Canvas,"Report section",new Vector2(x,-90),new Vector2(680,570),surface);
            Label(Canvas,side==0?"절차별 수행 결과":"수행 타임라인",new Vector2(x,151),new Vector2(620,40),30,Color.white);
            Label(Canvas,side==0?"표준 절차 순서":"실제 행동 순서 · 반복 수행 포함",new Vector2(x,116),new Vector2(620,26),20,muted);
            for(int i=0;i<PageSize;i++)(side==0?procedures:events)[i]=CreateRow(x,55-i*100,side==1);
        }
        empty=Label(Canvas,"아직 수행 기록이 없습니다.",new Vector2(350,20),new Vector2(600,60),27,muted,TextAlignmentOptions.Center);
        Buttons=new Image[6];
        string[] names={"메인 로비","다시 평가하기","이전","다음","이전","다음"};
        Vector2[] pos={new Vector2(330,-432),new Vector2(575,-432),new Vector2(-582,-335),new Vector2(-118,-335),new Vector2(118,-335),new Vector2(582,-335)};
        for(int i=0;i<6;i++){
            var size=i<2?new Vector2(220,64):new Vector2(150,52);
            Buttons[i]=Card(Canvas,names[i],pos[i],size,i==1?teal:rowColor);
            Label(Buttons[i].transform,names[i],Vector2.zero,size-new Vector2(12,8),25,Color.white,TextAlignmentOptions.Center);
        }
        leftPage=Label(Canvas,"",new Vector2(-350,-335),new Vector2(200,40),24,Color.white,TextAlignmentOptions.Center);
        rightPage=Label(Canvas,"",new Vector2(350,-335),new Vector2(200,40),24,Color.white,TextAlignmentOptions.Center);
        Label(Canvas,"<color=#40E0A3>● 완료</color>   <color=#FFC257>● 미수행</color>   <color=#FF8C78>● 순서 오류</color>",new Vector2(-345,-419),new Vector2(670,32),22,muted);
        Status=Label(Canvas,"수행 시점 / 소요 시간은 분:초로 표시됩니다.",new Vector2(-345,-453),new Vector2(670,28),19,muted);
        Pointer=Card(Canvas,"Pointer",Vector2.zero,new Vector2(9,9),Color.white);Pointer.gameObject.SetActive(false);
        Refresh(0,0);
    }
    void Metric(float x,string name,string value,string note,Color color){
        var card=Card(Canvas,name,new Vector2(x,280),new Vector2(448,150),surface);
        Label(card.transform,name,new Vector2(0,48),new Vector2(398,32),24,muted);
        Label(card.transform,value,new Vector2(0,2),new Vector2(398,58),42,color);
        Label(card.transform,note,new Vector2(0,-43),new Vector2(398,26),19,muted);
    }
    Row CreateRow(float x,float y,bool timeline){
        var r=new Row();r.panel=Card(Canvas,"Result row",new Vector2(x,y),new Vector2(648,92),rowColor);
        r.accent=Card(r.panel.transform,"Status marker",new Vector2(timeline?-310:-317,0),new Vector2(timeline?2:4,timeline?92:64),muted);
        if(timeline)r.node=Card(r.panel.transform,"Timeline node",new Vector2(-310,21),new Vector2(10,10),muted);
        r.number=Label(r.panel.transform,"",new Vector2(-282,21),new Vector2(47,30),22,muted,TextAlignmentOptions.Center);
        r.title=Label(r.panel.transform,"",new Vector2(-48,21),new Vector2(410,32),25,Color.white);
        r.badge=Card(r.panel.transform,"Status badge",new Vector2(244,21),new Vector2(124,30),green);
        r.state=Label(r.badge.transform,"",Vector2.zero,new Vector2(118,28),19,new Color(.035f,.065f,.1f),TextAlignmentOptions.Center);
        r.time=Label(r.panel.transform,"",new Vector2(10,-10),new Vector2(576,26),21,muted);
        r.detail=Label(r.panel.transform,"",new Vector2(10,-34),new Vector2(576,22),19,coral);
        return r;
    }
    public void Refresh(int procedurePage,int timelinePage){
        for(int slot=0;slot<PageSize;slot++){
            int index=procedurePage*PageSize+slot;var row=procedures[slot];row.panel.gameObject.SetActive(index<record.procedures.Length);
            if(index<record.procedures.Length){var p=record.procedures[index];var done=record.timeline.FirstOrDefault(e=>e.completed&&e.key==p.key);var error=record.timeline.FirstOrDefault(e=>e.orderError&&e.key==p.key);
                Fill(row,index+1,p.label,error!=null?"순서 오류":done!=null?"완료":"미수행",error!=null?coral:done!=null?green:amber,done==null?"수행 —   ·   소요 —":$"수행 {Clock(done.at)}   ·   소요 {Clock(done.duration)}",error!=null?"먼저 수행할 절차: "+error.expectedLabel:"");}
            index=timelinePage*PageSize+slot;row=events[slot];row.panel.gameObject.SetActive(index<record.timeline.Count);
            if(index<record.timeline.Count){var e=record.timeline[index];bool repeat=e.completed&&record.timeline.Take(index).Any(p=>p.completed&&p.key==e.key);
                bool pickedUp=e.kind=="집기";
                Fill(row,e.order,e.label,e.orderError?"순서 오류":repeat?"재수행":pickedUp?"집기 완료":e.kind,e.orderError?coral:repeat?teal:(e.completed||pickedUp)?green:muted,$"{Clock(e.at)}   ·   소요 {Clock(e.duration)}",e.orderError?"먼저 수행할 절차: "+e.expectedLabel:"");}
        }
        int pages=Mathf.Max(1,(record.procedures.Length+PageSize-1)/PageSize),logs=Mathf.Max(1,(record.timeline.Count+PageSize-1)/PageSize);
        leftPage.text=$"{procedurePage+1} / {pages}";rightPage.text=$"{timelinePage+1} / {logs}";empty.gameObject.SetActive(record.timeline.Count==0);
        enabledButtons[0]=enabledButtons[1]=true;enabledButtons[2]=procedurePage>0;enabledButtons[3]=procedurePage+1<pages;enabledButtons[4]=timelinePage>0;enabledButtons[5]=timelinePage+1<logs;
        SetHover(-1);
    }
    static string Clock(float t){int s=Mathf.FloorToInt(Mathf.Max(0,t));return $"{s/60:00}:{s%60:00}";}
    void Fill(Row r,int number,string title,string state,Color color,string time,string detail){r.number.text=number.ToString("00");r.title.text=title;r.state.text=state;r.badge.color=r.accent.color=color;if(r.node!=null)r.node.color=color;r.time.text=time;r.detail.text=detail;}
    public bool CanSelect(int index)=>index>=0&&index<enabledButtons.Length&&enabledButtons[index];
    public void SetHover(int selected){for(int i=0;i<Buttons.Length;i++){Buttons[i].color=!enabledButtons[i]?new Color(.09f,.13f,.19f):selected==i?new Color(.08f,.49f,.55f):i==1?teal:rowColor;Buttons[i].GetComponentInChildren<TMP_Text>().color=enabledButtons[i]?Color.white:new Color(.36f,.43f,.53f);}}
    TMP_Text Label(Transform parent,string value,Vector2 position,Vector2 size,float pointSize,Color color,TextAlignmentOptions align=TextAlignmentOptions.Left){var t=TrainingUI.Label(parent,font,value,position,size,pointSize);t.alignment=align;t.color=color;t.enableAutoSizing=true;t.fontSizeMin=pointSize-2;t.fontSizeMax=pointSize;return t;}
    Image Card(Transform parent,string name,Vector2 position,Vector2 size,Color color){var card=TrainingUI.Panel(parent,name,position,size,color);card.sprite=rounded;card.type=Image.Type.Sliced;return card;}
    void CreateSprite(){
        texture=new Texture2D(64,64,TextureFormat.RGBA32,false){name="Report rounded card",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
        for(int y=0;y<64;y++)for(int x=0;x<64;x++){float dx=Mathf.Max(16-x,0,x-47),dy=Mathf.Max(16-y,0,y-47);texture.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(16-Mathf.Sqrt(dx*dx+dy*dy))));}
        texture.Apply();rounded=Sprite.Create(texture,new Rect(0,0,64,64),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(16,16,16,16));
    }
    void OnDestroy(){if(rounded!=null){if(Application.isPlaying)Destroy(rounded);else DestroyImmediate(rounded);}if(texture!=null){if(Application.isPlaying)Destroy(texture);else DestroyImmediate(texture);}}
}
