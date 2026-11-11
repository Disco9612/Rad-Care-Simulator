using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;

[Serializable]
public sealed class FreeEvaluationRecord
{
    [Serializable] public sealed class Procedure { public string key,label; public Procedure(string key,string label){this.key=key;this.label=label;} }
    [Serializable] public sealed class Entry { public int order; public string key,label,kind; public float at, duration; public bool completed,orderError; public string expectedKey,expectedLabel; }
    public string scenario,startedUtc;
    public bool started,finished;
    public float seconds;
    public Procedure[] procedures;
    public List<Entry> timeline=new List<Entry>();
    public int CompletedCount=>procedures.Count(p=>timeline.Any(e=>e.completed&&e.key==p.key));
    public int OrderErrorCount=>timeline.Count(e=>e.orderError);
    public bool HasOrderError(string key)=>timeline.Any(e=>e.key==key&&e.orderError);
    public float CompletionPercent=>procedures.Length==0?0:100f*CompletedCount/procedures.Length;
    public bool Has(string key)=>timeline.Any(e=>e.completed&&e.key==key);
    public void Begin(){if(started)return;started=true;startedUtc=DateTime.UtcNow.ToString("O");}
    public void Tick(float dt){if(started&&!finished)seconds+=Mathf.Max(0,dt);}
    public void Add(string key,string label,float duration=0,bool completed=true,string kind="수행")
    {
        if(!started||finished)return;
        // Capture prerequisites at the time of the action; a later retry cannot erase this evidence.
        int index=Array.FindIndex(procedures,p=>p.key==key);
        var missing=completed&&index>=0?procedures.Take(index).FirstOrDefault(p=>!Has(p.key)):null;
        timeline.Add(new Entry{order=timeline.Count+1,key=key,label=label,at=seconds,duration=Mathf.Max(0,duration),completed=completed,kind=kind,
            orderError=missing!=null,expectedKey=missing?.key,expectedLabel=missing?.label});
    }
}

public sealed class EvaluationFinishHold
{
    public float Seconds {get;private set;}
    bool armed;
    public bool Tick(bool focused,bool pressed,float dt){
        if(!focused){Seconds=0;armed=false;return false;}
        if(!pressed){Seconds=0;armed=true;return false;}
        if(!armed)return false;
        Seconds+=Mathf.Max(0,dt);
        return Seconds>=3f;
    }
}

public sealed class FreeEvaluationSession : MonoBehaviour
{
    public FreeEvaluationRecord Record {get;private set;}
    public string SavedPath {get;private set;}
    public event Action Finished;
    readonly EvaluationFinishHold finishHold=new EvaluationFinishHold();
    float held=>finishHold.Seconds;
    TMP_Text holdText;
    public void Configure(string name,FreeEvaluationRecord.Procedure[] procedures){Record=new FreeEvaluationRecord{scenario=name,procedures=procedures};}
    void Update()
    {
        if(Record==null||!Record.started||Record.finished)return;
        bool focused=TrainingInputFocus.Available;
        if(focused)Record.Tick(Time.unscaledDeltaTime);
        bool pressed=focused&&OVRInput.Get(OVRInput.Button.One,OVRInput.Controller.LTouch);
        if(finishHold.Tick(focused,pressed,Time.unscaledDeltaTime)){Finish();return;}
        if(held>0 && holdText==null){var rig=FindAnyObjectByType<OVRCameraRig>();if(rig!=null){var canvas=TrainingUI.Canvas("End evaluation hold",rig.centerEyeAnchor,new Vector2(640,80));canvas.localPosition=new Vector3(0,-.25f,1);holdText=TrainingUI.Label(canvas,Resources.Load<TrainingCatalog>("TrainingCatalog").font,"",Vector2.zero,new Vector2(640,80),28);}}
        if(holdText!=null){holdText.transform.parent.gameObject.SetActive(held>0);holdText.text=$"평가 종료까지 {Mathf.CeilToInt(3-held)}초 · 놓으면 취소";}
    }
    public void Finish()
    {
        if(Record==null||!Record.started||Record.finished)return;
        Record.finished=true;
        if(holdText!=null)holdText.transform.parent.gameObject.SetActive(false);
        Save();Finished?.Invoke();
    }
    void Save(){try{string dir=Path.Combine(Application.persistentDataPath,"FreeEvaluations");Directory.CreateDirectory(dir);if(SavedPath==null)SavedPath=Path.Combine(dir,Record.scenario+"-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+".json");File.WriteAllText(SavedPath,JsonUtility.ToJson(Record,true));}catch(Exception e){Debug.LogWarning("Evaluation record save failed: "+e.Message,this);}}
    void OnDestroy(){if(Record!=null&&Record.started)Save();}
}
