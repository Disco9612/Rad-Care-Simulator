using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace RadCare.HospitalTreatment
{
    // Pre-rendered Korean speech: no network or platform TTS dependency at runtime.
    public sealed class HospitalNarration : MonoBehaviour
    {
        [Serializable] public sealed class Cue { public string key; public AudioClip clip; public bool patient; }
        public HospitalScenario scenario;
        public AudioSource source;
        public Cue[] cues;
        [Range(0,1)] public float volume=.85f;
        public string CurrentKey { get; private set; }
        public int SequenceCount { get; private set; }
        Coroutine sequence;
        AudioSource evaluationFeedback;
        AudioClip equipmentFeedback;
        public int EquipmentFeedbackCount { get; private set; }
        public bool EquipmentFeedbackPlaying => evaluationFeedback!=null&&evaluationFeedback.isPlaying;

        public void PlayEquipmentFeedback() {
            if(EquipmentFeedbackPlaying)return;
            if(equipmentFeedback==null)equipmentFeedback=Resources.Load<AudioClip>("HospitalTreatment/equipment_not_for_step");
            if(equipmentFeedback==null){Debug.LogWarning("Hospital equipment feedback audio missing",this);return;}
            if(evaluationFeedback==null){
                evaluationFeedback=gameObject.AddComponent<AudioSource>();
                evaluationFeedback.playOnAwake=false;evaluationFeedback.loop=false;evaluationFeedback.spatialBlend=0;evaluationFeedback.priority=16;
            }
            evaluationFeedback.clip=equipmentFeedback;evaluationFeedback.volume=volume;evaluationFeedback.Play();
            EquipmentFeedbackCount++;
        }

        void Update() { RefreshNarration(); }
        public void RefreshNarration()
        {
            if(scenario==null||!scenario.Ready||source==null||scenario.Evaluation?.IsRestarting==true||EquipmentFeedbackPlaying)return;
            var procedure=scenario.Procedure;
            string key=procedure.Complete?"complete":HospitalProcedure.NarrationKey(procedure.Index);
            if(!procedure.Complete&&HospitalScenario.IsMeasurement(procedure.Current.Action)) {
                if(scenario.AssistantSeconds>0)key="assistant_wait_"+procedure.Current.Stage;
                else if(scenario.AssistantReportReady)key=procedure.Current.Stage==1?"assistant_before":"assistant_after";
            }
            if(!procedure.Complete&&procedure.Current.Action==HospitalAction.RespondPain&&scenario.input.Held==null)key="pain_monitor";
            if(key==CurrentKey)return;
            CurrentKey=key;SequenceCount++;
            if(sequence!=null)StopCoroutine(sequence);
            source.Stop();
            string prefix=null;
            if(!procedure.Complete) {
                if(procedure.Current.Action==HospitalAction.RemoveDressing)prefix="pain_initial";
                if(procedure.Current.Action==HospitalAction.RespondPain&&key!="pain_monitor")prefix="pain_alert";
                if(procedure.Current.Action==HospitalAction.WashSecond)prefix="pain_relief";
            }
            string main=key.StartsWith("assistant_wait_")?"assistant_wait":key;
            if(scenario.Evaluation!=null&&(main.StartsWith("step_")||main=="complete"||main=="pain_monitor"))main=null;
            sequence=StartCoroutine(PlaySequence(prefix,main));
        }
        IEnumerator PlaySequence(params string[] keys)
        {
            foreach(string key in keys) {
                if(string.IsNullOrEmpty(key))continue;
                var cue=cues?.FirstOrDefault(c=>c.key==key);
                if(cue?.clip==null){Debug.LogWarning("Hospital narration cue missing: "+key,this);continue;}
                source.clip=cue.clip;source.volume=volume;source.pitch=1;source.Play();
                while(source.isPlaying)yield return null;
                yield return new WaitForSeconds(.15f);
            }
            sequence=null;
        }
        public void ResetForStepRetry(){if(sequence!=null)StopCoroutine(sequence);sequence=null;if(source!=null)source.Stop();CurrentKey=null;}
        void OnDisable(){ResetForStepRetry();if(evaluationFeedback!=null)evaluationFeedback.Stop();}
    }
}
