using UnityEngine;
using System.Collections;

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
    private Material[] _materials;
    private Transform grabInteraction;
    public GameObject placeHolder;

    private float pouringTimer = 0f;
    private const float RequiredPouringTime = 0.5f;
    private Color[] _originalEmissionColors;
    private void Awake()
    {
        if (placeHolder != null)
        {
            placeHolder.SetActive(false);
        }
    }

    void Start()
    {
        GameManager.Instance.OnStepChanged += HandleStepChanged;


        grabInteraction = transform.Find("[BuildingBlock] HandGrabInstallationRoutine");
        if (grabInteraction != null)
        {
            grabInteraction.gameObject.SetActive(false);
        }

        Renderer[] rends = GetComponentsInChildren<Renderer>();
        if (rends != null && rends.Length > 0)
        {
            _materials = new Material[rends.Length];
            _originalEmissionColors = new Color[rends.Length]; // 배열 크기 할당

            for (int i = 0; i < rends.Length; i++)
            {
                _materials[i] = rends[i].material;
                _materials[i].EnableKeyword("_EMISSION");

                // 1. 각 머티리얼의 원래 Emission 색상을 미리 저장해 둠 (디폴트가 흰색이든 검은색이든 상관없어짐)
                if (_materials[i].HasProperty("_EmissionColor"))
                {
                    _originalEmissionColors[i] = _materials[i].GetColor("_EmissionColor");
                }
            }
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
            SoundManager.Instance.PlayNPC(SoundManager.Instance.NPC4);
        }
        if (GameManager.Instance.currentStep == ScenarioStep.SalineInteraction && other.name.Contains("PlaceHolder"))
        {
            grabInteraction.gameObject.SetActive(false);
            transform.position = other.transform.position;
            transform.rotation = other.transform.rotation;
            GameManager.Instance.currentStep = ScenarioStep.ForcepsInteraction;
            other.gameObject.SetActive(false);
            Debug.Log("상태 : ForcepsInteraction");
        }
    }

    // 대상 오브젝트와 접촉 중일 때만 체크 시작
    void OnTriggerStay(Collider other)  
    {
        isTouchingTarget = true;
        Debug.Log($"isTouchingTarget = {isTouchingTarget}");
        Debug.Log($"currentStage = {GameManager.Instance.currentStep}");
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

        var em = liquidParticle.emission;

        while (isTouchingTarget)
        {
            // 1. 뒤집혔는지 판단 (임계값 -0.6f)
            float dot = Vector3.Dot(transform.up, Vector3.up);
            bool isCurrentlyUpsideDown = dot < -0.6f;

            // 2. 타이머 로직
            if (isCurrentlyUpsideDown)
            {
                pouringTimer += Time.deltaTime;
            }
            else
            {
                pouringTimer = 0f;
            }

            // 3. 0.5초 이상 유지되었을 때만 파티클 배출
            bool isPouring = pouringTimer >= 0.5f;

            // 현재 배출량 가져오기 (안전하게 가져오기)
            float currentRate = em.rateOverTime.constant;

            if (isPouring && GameManager.Instance.currentStep == ScenarioStep.SalineInteraction)
            {
                // 물이 쏟아지는 상태: 부드럽게 증가
                float targetRate = Mathf.Lerp(currentRate, maxEmissionRate, Time.deltaTime * lerpSpeed);

                // [핵심 수정] MinMaxCurve 구조체를 새로 만들어 통째로 할당
                em.rateOverTime = new ParticleSystem.MinMaxCurve(targetRate);

                SoundManager.Instance.PlaySFX(SoundManager.Instance.waterSound);
            }
            else
            {
                // 0.5초 미만이거나 똑바로 서 있을 때: 0으로 설정
                em.rateOverTime = new ParticleSystem.MinMaxCurve(0f);

                if (SoundManager.Instance.sfxSource.clip == SoundManager.Instance.waterSound)
                {
                    SoundManager.Instance.StopSFX();
                }
            }

            yield return null;
        }

        // 종료 처리 (손을 뗐을 때)
        em.rateOverTime = new ParticleSystem.MinMaxCurve(0f);
        pouringTimer = 0f;

        // 물 소리 정지
        if (SoundManager.Instance.sfxSource.clip == SoundManager.Instance.waterSound)
        {
            SoundManager.Instance.StopSFX();
        }
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
            SoundManager.Instance.PlayNPC(SoundManager.Instance.NPC3);
            grabInteraction.gameObject.SetActive(true);  
            _glowCoroutine = StartCoroutine(GlowRoutine());
        }
    }

    private IEnumerator GlowRoutine()
    {
        while (true)
        {
            if (_materials == null || _materials.Length == 0) yield break;

            float emission = Mathf.PingPong(Time.time * 2, 1.0f);

            for (int i = 0; i < _materials.Length; i++)
            {
                if (_materials[i] != null)
                {
                    // 원래 색상에 반짝이는 강도를 곱해줌 (원래 색상이 흰색이면 흰색으로, 컬러풀하면 그 색상 기반으로 빛남)
                    Color baseColor = _originalEmissionColors[i] == Color.black ? Color.white : _originalEmissionColors[i];
                    _materials[i].SetColor("_EmissionColor", baseColor * emission);
                }
            }
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

        if (_materials != null)
        {
            for (int i = 0; i < _materials.Length; i++)
            {
                if (_materials[i] != null)
                {
                    // 2. 끌 때는 무조건 검은색으로 만드는 대신, 저장해 뒀던 원래 디폴트 색상으로 복구
                    _materials[i].SetColor("_EmissionColor", _originalEmissionColors[i]);
                }
            }
        }
    }
}