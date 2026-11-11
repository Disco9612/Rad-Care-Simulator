using System.Collections.Generic;
using UnityEngine;

namespace RadCare.HospitalTreatment
{
    // Scene-owned audio: no persistent source or dependency on Emergency_Decon managers.
    [DisallowMultipleComponent]
    public sealed class HospitalInteractionAudio : MonoBehaviour
    {
        [Range(0, 1)] public float volume = .32f;
        HospitalScenario scenario;
        HospitalNarration narration;
        AudioSource effects, water;
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();

        void Awake()
        {
            scenario = GetComponent<HospitalScenario>();
            narration = GetComponent<HospitalNarration>();
            effects = CreateSource("Interaction effects");
            water = CreateSource("Irrigation sound");
            water.loop = true;
            water.clip = Clip("irrigation");
        }
        AudioSource CreateSource(string label)
        {
            var child = new GameObject(label);
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = .8f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = .6f;
            source.maxDistance = 5f;
            source.dopplerLevel = 0;
            source.priority = 160;
            source.volume = volume;
            return source;
        }
        AudioClip Clip(string key)
        {
            if (!clips.TryGetValue(key, out var clip))
            {
                clip = Resources.Load<AudioClip>("HospitalTreatment/SFX/" + key);
                clips[key] = clip;
                if (clip == null) Debug.LogWarning("Missing hospital effect: " + key, this);
            }
            return clip;
        }
        float Gain => volume * (narration != null &&
            ((narration.source != null && narration.source.isPlaying) || narration.EquipmentFeedbackPlaying) ? .35f : 1f);
        void Play(string key, Vector3 point)
        {
            if (!isActiveAndEnabled || !TrainingInputFocus.Available) return;
            effects.transform.position = point;
            effects.volume = Gain;
            effects.PlayOneShot(Clip(key));
        }
        public void Picked(HospitalInteractable item)
        {
            Play(item.itemId == "receiver" ? "metal" : item.kind == HospitalInteraction.Irrigator ? "plastic" :
                item.itemId == "fixation_tape" ? "tape" : "cloth", item.movable.position);
        }
        public void Placed(HospitalInteractable socket)
        {
            string key = socket.action == HospitalAction.ClearWaste || socket.action == HospitalAction.DisposeDressing ? "waste" :
                socket.action == HospitalAction.SecureDressing ? "tape" : socket.action == HospitalAction.PlaceReceiver ? "metal" : "cloth";
            Play(key, socket.transform.position);
        }
        public void Returned(HospitalInteractable item) { Play("plastic", item.movable.position); }
        public void Confirmed(HospitalInteractable target) { Play(target.action == HospitalAction.AdjustLight ? "switch" : "click", target.transform.position); }
        void LateUpdate()
        {
            effects.volume = Gain;
            water.volume = Gain * .45f;
            bool active = scenario != null && scenario.Ready && scenario.enabled && TrainingInputFocus.Available
                && scenario.input != null && scenario.input.isActiveAndEnabled
                && scenario.Evaluation?.IsRestarting != true && scenario.Procedure.IsWashing && scenario.Irrigating;
            if (active)
            {
                water.transform.position = scenario.IrrigationPoint;
                if (!water.isPlaying) water.Play();
            }
            else water.Stop();
        }
        public void StopAll() { if (effects != null) effects.Stop(); if (water != null) water.Stop(); }
        void OnDisable() { StopAll(); }
        void OnApplicationFocus(bool focused) { if (!focused) StopAll(); }
        void OnApplicationPause(bool paused) { if (paused) StopAll(); }
    }
}
