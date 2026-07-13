using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ParticleContactDetector : MonoBehaviour
{
    public ParticleSystem liquidParticle;
    public GameObject targetObject;
    public float requiredTime;

    private Coroutine checkCoroutine;
    private bool isContacting = false;

    // 파티클 트리거 모듈이 감지할 때마다 호출됨
    void OnParticleTrigger()
    {
        List<ParticleSystem.Particle> insideParticles = new List<ParticleSystem.Particle>();
        int numInside = liquidParticle.GetTriggerParticles(ParticleSystemTriggerEventType.Inside, insideParticles);

        bool currentlyTouching = (numInside > 0);

        // 접촉 상태가 변화했을 때만 코루틴 제어
        if (currentlyTouching && !isContacting)
        {
            isContacting = true;
            checkCoroutine = StartCoroutine(ContactTimer());
        }
        else if (!currentlyTouching && isContacting)
        {
            isContacting = false;
            if (checkCoroutine != null) StopCoroutine(checkCoroutine);
        }
    }

    IEnumerator ContactTimer()
    {
        float elapsed = 0f;
        while (elapsed < requiredTime)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 3초 경과 시 이벤트 실행
        TriggerSpecialEvent();
    }

    void TriggerSpecialEvent()
    {
        targetObject.gameObject.SetActive(false);
    }
}