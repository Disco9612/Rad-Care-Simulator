using UnityEngine;

public class EvaluationSurveyMeter : EvaluationTool
{
    public override EvaluationToolKind Kind => EvaluationToolKind.SurveyMeter;
    private float bloodContactTimer;
    private float rescanTime;
    private bool warningPlaying;
    private bool scanningPlaying;

    private bool Held => GetComponent<Oculus.Interaction.Grabbable>() is Oculus.Interaction.Grabbable grab && grab.GrabPoints.Count > 0;
    private void Update()
    {
        if (!Held || !IsInUse || Manager == null || !Manager.CanScan) StopMeterAudio();
    }
    private void OnDisable() => StopMeterAudio();
    private void StopMeterAudio()
    {
        var sound = EvaluationSoundManager.Instance;
        if (sound != null && sound.sfxSource != null
            && (sound.sfxSource.clip == sound.scanSound || (warningPlaying && sound.sfxSource.clip == null))) sound.StopSFX();
        scanningPlaying = false; warningPlaying = false;
        bloodContactTimer = 0f; rescanTime = 0f;
    }

    private void OnTriggerEnter(Collider other)
    {
        TryReturn(other);
    }

    private void OnTriggerStay(Collider other)
    {
        if (!Held || !TrainingInputFocus.Available) { StopMeterAudio(); return; }
        if (!IsInUse || !Manager.CanScan)
        {
            return;
        }
        EvaluationSoundManager sound = EvaluationSoundManager.Instance;
        if (other.name == "Blood" && Manager.CanDetectContamination)
        {
            if (!warningPlaying)
            {
                sound.StopSFX();
                sound.PlaySFXOneShot(sound.warningSound);
                warningPlaying = true;
                scanningPlaying = false;
            }
            bloodContactTimer += Time.deltaTime;
            if (bloodContactTimer >= 1f)
            {
                Manager.ReportMeasurementCompleted(Manager.CanVerifyTreatment ? rescanTime : bloodContactTimer);
            }
            return;
        }
        if (other.name.Contains("Collider") && !warningPlaying && !scanningPlaying)
        {
            sound.PlaySFX(sound.scanSound);
            scanningPlaying = true;
        }
        if (other.name.Contains("Patient") && Manager.CanVerifyTreatment)
        {
            rescanTime += Time.deltaTime;
            if (rescanTime >= 2f)
            {
                Manager.ReportMeasurementCompleted(Manager.CanVerifyTreatment ? rescanTime : bloodContactTimer);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.name.Contains("Collider") && scanningPlaying)
        {
            EvaluationSoundManager.Instance.StopSFX();
            scanningPlaying = false;
        }
        if (other.name == "Blood")
        {
            warningPlaying = false;
        }
    }

    protected override void HandleStepChanged(EvaluationStep step)
    {
        base.HandleStepChanged(step);
        ResetTimers();
    }

    public override void ResetForStage()
    {
        base.ResetForStage();
        ResetTimers();
    }

    private void ResetTimers()
    {
        StopMeterAudio();
        bloodContactTimer = 0f;
        rescanTime = 0f;
        warningPlaying = false;
        scanningPlaying = false;
    }
}
