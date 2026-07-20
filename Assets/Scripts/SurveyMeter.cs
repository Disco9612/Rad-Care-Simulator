using Oculus.Interaction;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class SurveyMeter: MonoBehaviour
{
    private bool isScanningPlaying = false;
    private bool isWarningPlaying = false;
    private float bloodContactTimer = 0f;
    private float rescanTime = 0f;

    private Coroutine _glowCoroutine;
    private Material _material;
    private Transform grabInteraction;
    public GameObject placeHolder;
    private bool isReported =false;
    public bool isTreatmentComplete = false;
    private void Start()
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

        Renderer rend = GetComponent<Renderer>();
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
                GameManager.Instance.currentStep = ScenarioStep.SalineInteraction;
            }
            else if (GameManager.Instance.currentStep == ScenarioStep.ResultVerification)
            {
                GameManager.Instance.currentStep = ScenarioStep.End;
            }
            other.gameObject.SetActive(false);
            Debug.Log("상태 : SalineInteraction");
        }
    }
    void OnTriggerStay(Collider other)
    {
        bool isStepValid = (GameManager.Instance.currentStep == ScenarioStep.SurveyMeterInteraction
                     || GameManager.Instance.currentStep == ScenarioStep.SurveyMeterWarningInteraction);

        // 1. Blood 충돌 처리 (우선순위: 경고음이 더 중요함)
        if (other.name == "Blood")
        {
            if (!isWarningPlaying)
            {
                SoundManager.Instance.StopSFX(); // 스캔음 끄고
                SoundManager.Instance.PlaySFX(SoundManager.Instance.warningSound); // 경고음 재생
                isWarningPlaying = true;
                isScanningPlaying = false;
            }

            bloodContactTimer += Time.deltaTime;
            if (bloodContactTimer >= 1.0f && !isReported)
            {
                isReported = true;
                Invoke("ReportSituation", 3f);
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

            // isTreatmentComplete 처리 로직
            if (isTreatmentComplete)
            {
                rescanTime += Time.deltaTime;
                if (rescanTime >= 2.0f && !isReported)
                {
                    Invoke("ReportSituation", 3f);
                }
            }
        }

        if (GameManager.Instance.currentStep == ScenarioStep.SurveyMeterInteraction && other.name.Contains("Collider")
           && isTreatmentComplete)
        {
            if (!isScanningPlaying)
            {
                isWarningPlaying = false;

                SoundManager.Instance.PlaySFX(SoundManager.Instance.scanSound);
                isScanningPlaying = true;
            }

            rescanTime += Time.deltaTime;

            if(rescanTime >= 2.0f && !isReported)
            {
                Invoke("ReportSituation", 3f);
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
            Debug.Log("NPC1 대사");
            SoundManager.Instance.PlayNPC(SoundManager.Instance.NPC1);
            GameManager.Instance.currentStep = ScenarioStep.SurveyMeterWarningInteraction;
            isReported = false;
        }
        else
        {
            Debug.Log("NPC4 대사");
            SoundManager.Instance.PlayNPC(SoundManager.Instance.NPC4);
            GameManager.Instance.currentStep = ScenarioStep.ResultVerification;
            isReported = false;
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