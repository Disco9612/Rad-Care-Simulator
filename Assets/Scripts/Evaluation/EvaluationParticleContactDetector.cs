using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EvaluationParticleContactDetector : MonoBehaviour
{
    public ParticleSystem liquidParticle;
    public float requiredTime;
    private Coroutine checkCoroutine;
    private bool isContacting;
    private EvaluationSalineBottle bottle;
    private readonly List<ParticleSystem.Particle> insideParticles = new List<ParticleSystem.Particle>();

    private void Start()
    {
        if (liquidParticle == null)
        {
            liquidParticle = GetComponent<ParticleSystem>();
        }
        bottle = GetComponentInParent<EvaluationSalineBottle>();
    }

    private void OnParticleTrigger()
    {
        EvaluationGameManager manager = EvaluationGameManager.Instance;
        if (liquidParticle == null || manager == null || !manager.CanWash
            || bottle == null || !manager.IsToolInUse(bottle) || liquidParticle.particleCount <= 0)
        {
            ResetContact();
            return;
        }
        int count = liquidParticle.GetTriggerParticles(ParticleSystemTriggerEventType.Inside, insideParticles);
        if (count > 0 && !isContacting)
        {
            isContacting = true;
            checkCoroutine = StartCoroutine(ContactTimer());
        }
        else if (count == 0)
        {
            ResetContact();
        }
    }

    private IEnumerator ContactTimer()
    {
        float elapsed = 0f;
        while (elapsed < requiredTime)
        {
            EvaluationGameManager manager = EvaluationGameManager.Instance;
            if (!isContacting || !manager.CanWash || !manager.IsToolInUse(bottle))
            {
                checkCoroutine = null;
                yield break;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }
        if (EvaluationGameManager.Instance.ReportWashingCompleted())
        {
            bottle.ShowReturnTarget();
        }
        checkCoroutine = null;
        isContacting = false;
    }

    public void ResetContact()
    {
        isContacting = false;
        if (checkCoroutine != null)
        {
            StopCoroutine(checkCoroutine);
            checkCoroutine = null;
        }
    }

    private void OnDisable()
    {
        ResetContact();
    }
}
