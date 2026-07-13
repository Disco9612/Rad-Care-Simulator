using UnityEngine;
using System.Collections;

public class LiquidFlowController : MonoBehaviour
{
    public ParticleSystem liquidParticle;
    public Transform salineBottle;
    public Transform salineBottleCap;

    public float maxEmissionRate = 50f;
    public float lerpSpeed = 5f;

    private ParticleSystem.EmissionModule emissionModule;
    private Coroutine flowCoroutine;
    private bool isTouchingTarget = false;

    void Start()
    {
        emissionModule = liquidParticle.emission;
    }

    // 대상 오브젝트와 접촉 중일 때만 체크 시작
    void OnTriggerStay(Collider other)
    {
        isTouchingTarget = true;

        if (flowCoroutine == null)
        {
            flowCoroutine = StartCoroutine(FlowRoutine());
        }
    }

    // 접촉이 끝나면 로직 정지
    void OnTriggerExit(Collider other)
    {
        isTouchingTarget = false;

        if (flowCoroutine != null)
        {
            StopCoroutine(flowCoroutine);
            flowCoroutine = null;
            emissionModule.rateOverTime = 0f;
        }
    }

    IEnumerator FlowRoutine()
    {
        // 1. 파티클 시스템을 루프 모드로 재생 시작 (루프는 인스펙터에서 설정)
        if (!liquidParticle.isPlaying)
            liquidParticle.Play();

        while (isTouchingTarget)
        {
            bool isPouring = salineBottleCap.position.y < (salineBottle.position.y - 0.005f);

            // 2. 목표치 설정
            float targetEmission = isPouring ? maxEmissionRate : 0f;

            // 3. Rate 조절 (constant를 직접 건드리기보다 값을 대입)
            // 만약 에러가 나면 아래처럼 설정하세요.
            var rate = emissionModule.rateOverTime;
            rate.constant = Mathf.Lerp(rate.constant, targetEmission, Time.deltaTime * lerpSpeed);
            emissionModule.rateOverTime = rate;

            yield return null;
        }

        // 4. 접촉 종료 시 0으로 확실히 초기화
        emissionModule.rateOverTime = 0f;
    }
}