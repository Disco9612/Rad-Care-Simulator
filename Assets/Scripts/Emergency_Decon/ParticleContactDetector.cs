using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ParticleContactDetector : MonoBehaviour
{
    public ParticleSystem liquidParticle;
    public float requiredTime;

    private Coroutine checkCoroutine;
    private bool isContacting = false;

    private void Start()
    {
        if (liquidParticle == null)
        {
            liquidParticle = GetComponent<ParticleSystem>();
        }
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
        if (liquidParticle == null || GameManager.Instance == null || !GameManager.Instance.CanWash)
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

    void ResetContact()
    {
        isContacting = false;
        if (checkCoroutine != null)
        {
            StopCoroutine(checkCoroutine);
            checkCoroutine = null;
        }
    }

    IEnumerator ContactTimer()
    {
        float elapsed = 0f;

        while (elapsed < requiredTime && isContacting && GameManager.Instance.CanWash)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 지정된 시간 동안 Inside 상태가 유지되었을 때 실행
        if (isContacting && GameManager.Instance.CanWash && elapsed >= requiredTime) TriggerSpecialEvent();

        // 이벤트 실행 후 상태 초기화
        ResetContact();
    }

    void TriggerSpecialEvent()
    {
        if (!GameManager.Instance.ReportWashingCompleted())
        {
            return;
        }

        SalineBottle _salineBottle = GetComponentInParent<SalineBottle>();
        if (_salineBottle != null && _salineBottle.placeHolder != null)
        {
            _salineBottle.placeHolder.gameObject.SetActive(true);
        }
    }
}
