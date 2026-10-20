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

    // Inside = Callback 으로 설정되어 있을 때 매 프레임 호출됨
    void OnParticleTrigger()
    {
        if (liquidParticle == null || GameManager.Instance == null || !GameManager.Instance.CanWash)
        {
            ResetContact();
            return;
        }

        // [핵심 방어 1] 화면에 살아있는 파티클이 아예 없다면 무시 (나오지도 않았는데 작동하는 버그 원천 차단)
        if (liquidParticle.particleCount <= 0)
        {
            ResetContact();
            return;
        }

        List<ParticleSystem.Particle> insideParticles = new List<ParticleSystem.Particle>();
        int numInside = liquidParticle.GetTriggerParticles(ParticleSystemTriggerEventType.Inside, insideParticles);

        bool currentlyTouching = (numInside > 0);

        // 실제로 파티클이 트리거 내부로 들어와 있고, 아직 타이머가 시작 안 된 경우
        if (currentlyTouching && !isContacting)
        {
            Debug.Log($"파티클 Inside 감지 시작 (내부 파티클 수: {numInside})");
            isContacting = true;
            if (checkCoroutine != null) StopCoroutine(checkCoroutine);
            checkCoroutine = StartCoroutine(ContactTimer());
        }
        // 파티클이 더 이상 내부에 없는데 타이머가 돌고 있던 경우 -> 즉시 중단
        else if (!currentlyTouching && isContacting)
        {
            Debug.Log("파티클 Inside 벗어남 - 타이머 중단");
            ResetContact();
        }
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

        while (elapsed < requiredTime && GameManager.Instance.CanWash)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 지정된 시간 동안 Inside 상태가 유지되었을 때 실행
        TriggerSpecialEvent();

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
