using System.Collections;
using UnityEngine;

public class SurveyMeter: MonoBehaviour
{
    private bool isScanningPlaying = false;
    private bool isWarningPlaying = false;
    private float bloodContactTimer = 0f;
    private float rescanTime = 0f;

    private Coroutine _glowCoroutine;
    private Material[] _materials;
    private Transform grabInteraction;
    public GameObject placeHolder;
    private bool isReported = false;
    public bool isTreatmentComplete = false;
    private Color[] _originalEmissionColors;

    private void Start()
    {
        GameManager.Instance.OnStepChanged += HandleStepChanged;

        if (placeHolder != null)
        {
            placeHolder.SetActive(false);
        }

        Renderer[] rends = GetComponentsInChildren<Renderer>();
        if (rends != null && rends.Length > 0)
        {
            _materials = new Material[rends.Length];
            _originalEmissionColors = new Color[rends.Length]; // 배열 크기 할당
            Debug.Log($"{_materials.Length} 메터리얼수");
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


        grabInteraction = transform.Find("[BuildingBlock] HandGrabInstallationRoutine");
        if (grabInteraction != null)
        {
            grabInteraction.gameObject.SetActive(false);
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
        if ((GameManager.Instance.currentStep == ScenarioStep.SurveyMeterWarningInteraction 
            || GameManager.Instance.currentStep == ScenarioStep.ResultVerification)
            && other.name.Contains("PlaceHolder"))
        {
            grabInteraction.gameObject.SetActive(false);
            transform.position = other.transform.position;
            transform.rotation = other.transform.rotation;
            if(GameManager.Instance.currentStep == ScenarioStep.SurveyMeterWarningInteraction)
            {
                Debug.Log("들어옴");
                GameManager.Instance.currentStep = ScenarioStep.SalineInteraction;
            }
            else if (GameManager.Instance.currentStep == ScenarioStep.ResultVerification)
            {
                GameManager.Instance.currentStep = ScenarioStep.End;
            }
            other.gameObject.SetActive(false);
            
        }
    }
    void OnTriggerStay(Collider other)
    {
        bool isStepValid = (GameManager.Instance.currentStep == ScenarioStep.SurveyMeterInteraction
                     || GameManager.Instance.currentStep == ScenarioStep.SurveyMeterWarningInteraction);

        // 1. Blood 충돌 처리 (우선순위: 경고음이 더 중요함)
        if (other.name == "Blood" && isStepValid)
        {
            if (!isWarningPlaying)
            {
                SoundManager.Instance.StopSFX(); // 스캔음 끄고
                SoundManager.Instance.PlaySFXOneShot(SoundManager.Instance.warningSound); // 경고음 재생
                isWarningPlaying = true;
                isScanningPlaying = false;
            }

            bloodContactTimer += Time.deltaTime;
            if (bloodContactTimer >= 1.0f && !isReported) 
            {
                isReported = true;
                Invoke("ReportSituation",0f); //첫번재 보고
            }
            return; // Blood 처리 후에는 아래 스캔 로직을 타지 않도록 종료
        }

        // 2. Collider 충돌 처리
        if (isStepValid && other.name.Contains("Collider"))
        {
            // Blood 경고음이 재생 중이 아닐 때만 스캔음 재생
            if (!isWarningPlaying && !isScanningPlaying)
            {
                SoundManager.Instance.PlaySFX(SoundManager.Instance.scanSound);
                isScanningPlaying = true;
            }
        }


        if (GameManager.Instance.currentStep == ScenarioStep.SurveyMeterInteraction && other.name.Contains("Collider")
           && isTreatmentComplete)
        {
            if (!isScanningPlaying)
            {
                isWarningPlaying = false;

                SoundManager.Instance.PlaySFXOneShot(SoundManager.Instance.scanSound);
                isScanningPlaying = true;
            }
        }
        if (other.name.Contains("Patient") && isTreatmentComplete)
        {
            rescanTime += Time.deltaTime;

            if (rescanTime >= 2.0f && isReported)
            {
                isReported = false;
                Invoke("ReportSituation", 0f);
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.name.Contains("Collider"))
        {
            SoundManager.Instance.StopSFX();
            isScanningPlaying = false;
        }

        if (other.name == "Blood")
        {
            isWarningPlaying = false;
        }
    }

    public void ReportSituation()
    {
        if (!isTreatmentComplete)
        {
            SoundManager.Instance.PlayNPC(SoundManager.Instance.NPC2);
            GameManager.Instance.currentStep = ScenarioStep.SurveyMeterWarningInteraction;

        }
        else
        {
            GameManager.Instance.currentStep = ScenarioStep.ResultVerification;
            SoundManager.Instance.PlayNPC(SoundManager.Instance.NPC11);


        }

    }

    private void HandleStepChanged(ScenarioStep newStep)
    {
        if (_glowCoroutine != null)
        {
            StopCoroutine(_glowCoroutine);
            _glowCoroutine = null;
        }

        if (newStep == ScenarioStep.SurveyMeterInteraction)
        {
            if (grabInteraction != null)
            {
                grabInteraction.gameObject.SetActive(true);
            }
            _glowCoroutine = StartCoroutine(GlowRoutine());
        }
        else if(newStep == ScenarioStep.SurveyMeterWarningInteraction || newStep == ScenarioStep.ResultVerification)
        {
            if (placeHolder != null)
            {

                placeHolder.gameObject.SetActive(true);
            }
        }
        else if(newStep == ScenarioStep.End)
        {
            SoundManager.Instance.PlayNPC(SoundManager.Instance.NPC12);
            GameManager.Instance.GameResult();
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