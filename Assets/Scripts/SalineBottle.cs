using UnityEngine;
using System.Collections;
using Oculus.Interaction;

public class SalineBottle : MonoBehaviour
{
    public ParticleSystem liquidParticle;
    public Transform salineBottle;
    public Transform salineBottleCap;

    public float maxEmissionRate = 50f;
    public float lerpSpeed = 5f;

    private ParticleSystem.EmissionModule emissionModule;
    private Coroutine flowCoroutine;
    private bool isTouchingTarget = false;

    private Coroutine _glowCoroutine;
    private Material _material;
    private Grabbable _grabbable;
    public GameObject placeHolder;

    private float pouringTimer = 0f;
    private const float RequiredPouringTime = 0.5f;
    void Start()
    {
        GameManager.Instance.OnStepChanged += HandleStepChanged;

        if (placeHolder != null)
        {
            placeHolder.SetActive(false);
        }

        _grabbable = GetComponent<Grabbable>();
        if (_grabbable != null)
        {
            _grabbable.enabled = false;
        }

        Renderer rend = GetComponent<Renderer>();
        if (GetComponent<Renderer>() != null)
        {
            _material = GetComponent<Renderer>().material;
            _material.EnableKeyword("_EMISSION");
        }

        if (liquidParticle != null)
        {
            emissionModule = liquidParticle.emission;
        }
    }
    void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnStepChanged -= HandleStepChanged;
    }
    void OnTriggerEnter(Collider other)
    {
        if (_glowCoroutine != null)
        {
            StopGlow();
        }
        if (GameManager.Instance.currentStep == ScenarioStep.SalineInteraction && other.name.Contains("PlaceHolder"))
        {
            transform.position = other.transform.position;
            transform.rotation = other.transform.rotation;
            _grabbable.enabled = false;
            GameManager.Instance.currentStep = ScenarioStep.ForcepsInteraction;
            other.gameObject.SetActive(false);
            Debug.Log("상태 : ForcepsInteraction");
        }
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

    // 접촉이 끝나면 로직 정지 잡고있는 상태
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
        if (liquidParticle != null && !liquidParticle.isPlaying)
            liquidParticle.Play();

        while (isTouchingTarget)
        {
            // 1. 뒤집혔는지 판단 (임계값 -0.8f 추천)
            float dot = Vector3.Dot(transform.up, Vector3.up);
            bool isCurrentlyUpsideDown = dot < -0.6f;

            // 2. 타이머 로직 (중요: 뒤집혔을 때만 증가, 아니면 즉시 0)
            if (isCurrentlyUpsideDown)
            {
                pouringTimer += Time.deltaTime;
            }
            else
            {
                pouringTimer = 0f; // 0.5초가 안 되었는데 병을 세우면 즉시 0으로 초기화
            }

            // 3. 0.5초 이상 유지되었을 때만 파티클 배출
            bool isPouring = pouringTimer >= 0.5f;

            var em = liquidParticle.emission;
            var rate = em.rateOverTime;

            if (isPouring)
            {
                // 물이 쏟아지는 상태
                rate.constant = Mathf.Lerp(rate.constant, maxEmissionRate, Time.deltaTime * lerpSpeed);
            }
            else
            {
                // 0.5초 미만이거나 똑바로 서 있을 때
                rate.constant = 0f;
            }

            em.rateOverTime = rate;
            yield return null;
        }

        // 종료 처리
        var finalEm = liquidParticle.emission;
        finalEm.rateOverTime = 0f;
        pouringTimer = 0f;
    }

    private void HandleStepChanged(ScenarioStep newStep)
    {
        if (_glowCoroutine != null)
        {
            StopCoroutine(_glowCoroutine);
            _glowCoroutine = null;
        }

        // 특정 단계일 때만 코루틴 시작
        if (newStep == ScenarioStep.SalineInteraction)
        {
            _grabbable.enabled = true;
            _glowCoroutine = StartCoroutine(GlowRoutine());
        }
    }

    private IEnumerator GlowRoutine()
    {
        while (true)
        {
            if (_material == null) yield break;
            float emission = Mathf.PingPong(Time.time * 2, 1.0f);
            _material.SetColor("_EmissionColor", Color.white * emission);
            yield return null;
        }
    }

    private void StopGlow()
    {
        if (_glowCoroutine != null)
        {
            StopCoroutine(_glowCoroutine);
            _glowCoroutine = null;
        }

        if (_material != null)
        {
            _material.SetColor("_EmissionColor", Color.black); // 발광 끄기
        }
    }
}