using UnityEngine;
using UnityEngine.EventSystems;

public class ScanObject : MonoBehaviour
{
    private bool isScanningPlaying = false;
    private bool isWarningPlaying = false;
    private float bloodContactTimer = 0f;
    void OnTriggerStay(Collider other)
    {
        if (GameManager.Instance.currentStep == ScenarioStep.SurveyMeterInteraction && other.name.Contains("Collider"))
        {
            // 스캔 소리가 이미 재생 중이 아니라면 딱 한 번만 호출
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
            // Blood 접촉 시 단계 변경 및 경고음 재생
            if (!isWarningPlaying)
            {                
                SoundManager.Instance.StopSFX();
                isScanningPlaying = false;

                GameManager.Instance.currentStep = ScenarioStep.SurveyMeterWarningInteraction;
                SoundManager.Instance.PlaySFX(SoundManager.Instance.warningSound);
                isWarningPlaying = true;

                bloodContactTimer += Time.deltaTime;
                if (bloodContactTimer >= 1.0f)
                {
                    GameManager.Instance.currentStep  = ScenarioStep.SalineInteraction;
                }
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
            GameManager.Instance.currentStep = ScenarioStep.SurveyMeterInteraction;
            isWarningPlaying = false;
        }
    }
}