using Oculus.Interaction;
using System.Collections;
using UnityEngine;

public class Forceps : MonoBehaviour
{
    private Coroutine _glowCoroutine;
    private Material _material;
    private Transform grabInteraction;
    public GameObject placeHolder;
    public GameObject targetObject;
    public float requiredTime;
    public Material bloodMaterial;
    private Coroutine checkCoroutine;
    private bool isContacting = false;
    private Renderer rend;
    public Material emptyMaterial;

    public GameObject surveyMeter;
    private SurveyMeter _srurveyMeter;
    void Start()
    {
        GameManager.Instance.OnStepChanged += HandleStepChanged;

        if (placeHolder != null)
        {
            placeHolder.SetActive(false);
        }

        grabInteraction = transform.Find("[BuildingBlock] HandGrabInstallationRoutine");
        if (grabInteraction != null)
        {
            grabInteraction.gameObject.SetActive(false);
        }

        rend = GetComponent<Renderer>();
        if (GetComponent<Renderer>() != null)
        {
            _material = GetComponent<Renderer>().material;
            _material.EnableKeyword("_EMISSION");
        }
      

    }
    void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnStepChanged -= HandleStepChanged;
    }
    void OnTriggerStay(Collider other)
    {
        if(GameManager.Instance.currentStep == ScenarioStep.ForcepsInteraction && other.gameObject == targetObject)
        {
            if (!isContacting)
            {
                Debug.Log("들어옴");
                isContacting = true;
                checkCoroutine = StartCoroutine(ContactTimer());
            }
        }
    }
    void OnTriggerExit(Collider other)
    {
        // 2. 접촉이 끝났을 때만 코루틴 정지 및 변수 초기화
        if (other.gameObject == targetObject)
        {
            if (isContacting)
            {
                isContacting = false;
                if (checkCoroutine != null)
                {
                    StopCoroutine(checkCoroutine);
                    checkCoroutine = null;
                }
            }
        }
    }
    void OnTriggerEnter(Collider other)
    {
        if (_glowCoroutine != null)
        {
            StopGlow();
        }
        if(GameManager.Instance.currentStep == ScenarioStep.DisposeStage && other.name.Contains("PlaceHolder"))
        {
            Debug.Log("집게 제자리로 돌아옴");
            grabInteraction.gameObject.SetActive(false);
            transform.position = other.transform.position;
            transform.rotation = other.transform.rotation;

            GameManager.Instance.currentStep = ScenarioStep.SurveyMeterInteraction;
            _srurveyMeter = surveyMeter.GetComponent<SurveyMeter>();
            _srurveyMeter.isTreatmentComplete = true;

            other.gameObject.SetActive(false);
            Debug.Log("상태 : DisposeStage");
        }

        if (GameManager.Instance.currentStep == ScenarioStep.DisposeStage && other.name.Contains("container"))
        {
            Material[] materials = rend.materials;
            if(materials.Length > 0)
            {
                materials[0] = emptyMaterial;
                rend.materials = materials;
                gameObject.transform.Find("ForcepsBall_Colliders").gameObject.SetActive(false);
                placeHolder.gameObject.SetActive(true);

            }

        }
    }

    IEnumerator ContactTimer()
    {
        float elapsed = 0f;
        while (elapsed < requiredTime)
        {
            elapsed += Time.deltaTime;
            Debug.Log("elapsed");
            yield return null;
        }
        
        targetObject.SetActive(false);

        Material[] materials = rend.materials;
        if (materials.Length > 0)
        {
            materials[0] = bloodMaterial;

            rend.materials = materials;
        }
        GameManager.Instance.currentStep = ScenarioStep.DisposeStage;
    }



    private void HandleStepChanged(ScenarioStep newStep)
    {
        if (_glowCoroutine != null)
        {
            StopCoroutine(_glowCoroutine);
            _glowCoroutine = null;
        }

        // 특정 단계일 때만 코루틴 시작
        if (newStep == ScenarioStep.ForcepsInteraction)
        {
            grabInteraction.gameObject.SetActive(true);
            _glowCoroutine = StartCoroutine(GlowRoutine());
        }
    }

    private IEnumerator GlowRoutine()
    {
        if (rend == null) yield break;

        while (true)
        {
            // 1. 매 프레임 머티리얼 배열 전체를 가져옵니다.
            Material[] materials = rend.materials;

            float emission = Mathf.PingPong(Time.time * 2, 1.0f);
            Color emissionColor = Color.white * emission;

            // 2. 모든 엘리먼트를 순회하며 적용합니다.
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] != null)
                {
                    materials[i].SetColor("_EmissionColor", emissionColor);
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

        if (rend != null)
        {
            Material[] materials = rend.materials;
            foreach (Material mat in materials)
            {
                if (mat != null)
                {
                    mat.SetColor("_EmissionColor", Color.black);
                }
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
