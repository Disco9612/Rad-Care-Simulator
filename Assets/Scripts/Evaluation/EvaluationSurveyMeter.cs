using UnityEngine;

public class EvaluationSurveyMeter : EvaluationTool
{
    public override EvaluationToolKind Kind => EvaluationToolKind.SurveyMeter;
    private float bloodContactTimer;
    private float rescanTime;
    private bool warningPlaying;
    private bool scanningPlaying;

    private void OnTriggerEnter(Collider other)
    {
        TryReturn(other);
    }

    private void OnTriggerStay(Collider other)
    {
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
                Manager.ReportMeasurementCompleted();
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
                Manager.ReportMeasurementCompleted();
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
        bloodContactTimer = 0f;
        rescanTime = 0f;
        warningPlaying = false;
        scanningPlaying = false;
    }
}
