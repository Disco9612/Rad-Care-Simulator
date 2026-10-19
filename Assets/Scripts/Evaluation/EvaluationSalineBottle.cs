using UnityEngine;

public class EvaluationSalineBottle : EvaluationTool
{
    public ParticleSystem liquidParticle;
    public Transform salineBottle;
    public Transform salineBottleCap;
    public float maxEmissionRate = 50f;
    public float lerpSpeed = 5f;

    private float pouringTimer;
    public override EvaluationToolKind Kind => EvaluationToolKind.Saline;

    private void OnTriggerEnter(Collider other)
    {
        TryReturn(other);
    }

    private void Update()
    {
        if (liquidParticle == null)
        {
            return;
        }
        bool canPour = IsInUse && Manager.CanWash;
        bool upsideDown = Vector3.Dot(transform.up, Vector3.up) < -0.6f;
        pouringTimer = canPour && upsideDown ? pouringTimer + Time.deltaTime : 0f;
        var emission = liquidParticle.emission;
        EvaluationSoundManager sound = EvaluationSoundManager.Instance;
        if (pouringTimer >= 0.5f)
        {
            if (!liquidParticle.isPlaying)
            {
                liquidParticle.Play();
            }
            emission.rateOverTime = Mathf.Lerp(emission.rateOverTime.constant, maxEmissionRate, Time.deltaTime * lerpSpeed);
            sound.PlaySFX(sound.waterSound);
        }
        else
        {
            emission.rateOverTime = 0f;
            if (sound != null && sound.sfxSource.clip == sound.waterSound)
            {
                sound.StopSFX();
            }
        }
    }

    public void ShowReturnTarget()
    {
        if (placeHolder != null)
        {
            placeHolder.SetActive(Manager.CanReturnSaline);
        }
    }

    public override void ResetForStage()
    {
        base.ResetForStage();
        pouringTimer = 0f;
        if (liquidParticle != null)
        {
            liquidParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var emission = liquidParticle.emission;
            emission.rateOverTime = 0f;
        }
        foreach (EvaluationParticleContactDetector detector in GetComponentsInChildren<EvaluationParticleContactDetector>(true))
        {
            detector.ResetContact();
        }
    }
}
