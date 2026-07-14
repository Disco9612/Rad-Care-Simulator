using Oculus.Interaction;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class SurveyMeter: MonoBehaviour
{
    private bool isScanningPlaying = false;
    private bool isWarningPlaying = false;
    private float bloodContactTimer = 0f;

    private Coroutine _glowCoroutine;
    private Material _material;
    private Grabbable _grabbable;
    public GameObject placeHolder;
    private bool isReported =false;
    private void Start()
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
        if (GameManager.Instance.currentStep == ScenarioStep.SurveyMeterWarningInteraction && other.name.Contains("PlaceHolder"))
        {
            transform.position = other.transform.position;
            transform.rotation = other.transform.rotation;
            _grabbable.enabled = false;
            GameManager.Instance.currentStep = ScenarioStep.SalineInteraction;
            other.gameObject.SetActive(false);
            Debug.Log("상태 : SalineInteraction");
        }
    }
    void OnTriggerStay(Collider other)
    {
        if (GameManager.Instance.currentStep == ScenarioStep.SurveyMeterInteraction && other.name.Contains("Collider"))
        {
            if (!isScanningPlaying)
            {
                SoundManager.Instance.StopSFX();
                isWarningPlaying = false;

                SoundManager.Instance.PlaySFX(SoundManager.Instance.scanSound);
                isScanningPlaying = true;
            }
        }
        else if (other.name == "Blood")
        {

            if (!isWarningPlaying)
            {                
                SoundManager.Instance.StopSFX();
                isScanningPlaying = false;

                SoundManager.Instance.PlaySFX(SoundManager.Instance.warningSound);
                isWarningPlaying = true;

                bloodContactTimer += Time.deltaTime;
            }
            if (bloodContactTimer >= 1.0f && !isReported)
            {
                isReported = true; // 실행했음을 표시
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
        Debug.Log("NPC1 대사");
        SoundManager.Instance.PlayNPC(SoundManager.Instance.NPC1);
        GameManager.Instance.currentStep = ScenarioStep.SurveyMeterWarningInteraction;
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
            if (_grabbable != null)
            {
                _grabbable.enabled = true;
            }
            _glowCoroutine = StartCoroutine(GlowRoutine());
        }
        else if(newStep == ScenarioStep.SurveyMeterWarningInteraction)
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