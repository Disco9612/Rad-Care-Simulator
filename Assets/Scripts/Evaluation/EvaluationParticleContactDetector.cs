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

    private float lastWaterContact = float.NegativeInfinity;
    private readonly List<ParticleCollisionEvent> waterCollisions = new List<ParticleCollisionEvent>();

    private void OnParticleCollision(GameObject other)
    {
        if (liquidParticle == null) return;
        int count = liquidParticle.GetCollisionEvents(other, waterCollisions);
        var trigger = liquidParticle.trigger;
        for (int i = 0; i < count; i++)
        for (int j = 0; j < trigger.colliderCount; j++)
        {
            Collider wound = trigger.GetCollider(j) as Collider;
            if (wound == null || !wound.enabled || !wound.gameObject.activeInHierarchy) continue;
            Vector3 point = waterCollisions[i].intersection;
            // Only surface impacts at the existing wound volume count as washing.
            if ((wound.ClosestPoint(point) - point).sqrMagnitude <= 0.015f * 0.015f)
                lastWaterContact = Time.time;
        }
    }

    private void OnParticleTrigger()
    {
        if (liquidParticle == null) return;
        var particles = new List<ParticleSystem.Particle>();
        if (liquidParticle.GetTriggerParticles(ParticleSystemTriggerEventType.Inside, particles) > 0)
            lastWaterContact = Time.time;
    }

    private void Update()
    {
        EvaluationGameManager manager = EvaluationGameManager.Instance;
        if (!TrainingInputFocus.Available || liquidParticle == null || manager == null || !manager.CanWash || bottle == null || !manager.IsToolInUse(bottle))
        {
            lastWaterContact = float.NegativeInfinity;
            ResetContact();
            return;
        }
        bool touching = Time.time - lastWaterContact <= 0.12f;
        if (touching && !isContacting)
        {
            isContacting = true;
            checkCoroutine = StartCoroutine(ContactTimer());
        }
        else if (!touching) ResetContact();
    }

    private IEnumerator ContactTimer()
    {
        float elapsed = 0f;
        while (elapsed < requiredTime)
        {
            EvaluationGameManager manager = EvaluationGameManager.Instance;
            if (!TrainingInputFocus.Available || !isContacting || !manager.CanWash || !manager.IsToolInUse(bottle))
            {
                checkCoroutine = null;
                yield break;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }
        if (EvaluationGameManager.Instance.ReportWashingCompleted(elapsed))
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
