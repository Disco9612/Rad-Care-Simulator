using System.Collections;
using UnityEngine;

public class Forceps : MonoBehaviour
{
    private Coroutine _glowCoroutine;
    private Material _material;
    private Transform grabInteraction;
    public GameObject placeHolder;
    public GameObject targetObject;
    public float requiredTime1;
    public float requiredTime2;
    public float requiredTime3;
    public Material bloodMaterial;
    private Coroutine checkCoroutine;
    private bool isContacting = false;
    private Renderer rend;
    public Material emptyMaterial;

    public GameObject surveyMeter;
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
        if(GameManager.Instance.CanTreat && other.gameObject == targetObject)
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
        if (placeHolder != null && placeHolder.activeInHierarchy
            && other.transform.IsChildOf(placeHolder.transform)
            && GameManager.Instance.ReportForcepsReturned())
        {
            grabInteraction.gameObject.SetActive(false);
            transform.SetPositionAndRotation(placeHolder.transform.position, placeHolder.transform.rotation);
            placeHolder.SetActive(false);
        }

        WasteContainer container = other.GetComponentInParent<WasteContainer>();
        if (container != null)
        {
            container.TryDispose(this);
        }
    }

    public void ApplyDisposalVisuals()
    {
        Material[] materials = rend.materials;
        if (materials.Length > 0)
        {
            materials[0] = emptyMaterial;
            rend.materials = materials;
        }
        Transform wasteCollider = transform.Find("ForcepsBall_Colliders");
        if (wasteCollider != null)
        {
            wasteCollider.gameObject.SetActive(false);
        }
        if (placeHolder != null)
        {
            placeHolder.SetActive(true);
        }
    }

    IEnumerator ContactTimer()
    {
        float elapsed = 0f;

        while (elapsed < requiredTime1)
        {
            elapsed += Time.deltaTime;
            Debug.Log("elapsed");
            yield return null;
        }
        GameManager.Instance.ReportTreatmentProgress(1);

        while (elapsed < requiredTime2)
        {
            elapsed += Time.deltaTime;
            Debug.Log("elapsed");
            yield return null;
        }
        GameManager.Instance.ReportTreatmentProgress(2);

        while (elapsed < requiredTime3)
        {
            elapsed += Time.deltaTime;
            Debug.Log("elapsed");
            yield return null;
        }
        GameManager.Instance.ReportTreatmentProgress(3);

        if (!GameManager.Instance.ReportTreatmentCompleted())
        {
            yield break;
        }
        targetObject.SetActive(false);

        Material[] materials = rend.materials;
        if (materials.Length > 0)
        {
            materials[0] = bloodMaterial;

            rend.materials = materials;
        }
        checkCoroutine = null;
    }



    private void HandleStepChanged(ScenarioStep newStep)
    {
        StopGlow();
        bool canUse = GameManager.Instance.CanTreat || GameManager.Instance.CanDispose
            || GameManager.Instance.CanReturnForceps;
        if (grabInteraction != null)
        {
            grabInteraction.gameObject.SetActive(canUse);
        }
        if (placeHolder != null)
        {
            placeHolder.SetActive(GameManager.Instance.CanReturnForceps);
        }
        if (GameManager.Instance.CanTreat)
        {
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

}
