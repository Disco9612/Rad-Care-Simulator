using System;
using TMPro;
using UnityEngine;

public enum ScenarioStep
{
    Briefing,
    SurveyMeterInteraction,
    SurveyMeterWarningInteraction,
    SalineInteraction,
    ForcepsInteraction,
    DisposeStage,
    ResultVerification,
    End
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public event Action<ScenarioStep> OnStepChanged;

    private ScenarioStep _currentStep = ScenarioStep.Briefing;
    public CanvasGroup mainUI;
    public TextMeshProUGUI uIText;
    private bool _isCurrentlyPinching = false;
    [SerializeField] private OVRHand leftHand;

    // 이벤트 기반으로 변경: 상태가 변경될 때만 처리
    private event Action<bool> OnPinchStateChanged;


    public ScenarioStep currentStep
    {
        get => _currentStep;
        set
        {
            if (_currentStep != value) // 값이 실제로 바뀔 때만 처리
            {
                _currentStep = value;
                OnStepChanged?.Invoke(_currentStep); // 변경 알림 발생
                Debug.Log($"단계 변경됨: {_currentStep}");
            }
        }
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 씬이 바뀌어도 파괴되지 않게 함
        }
        else
        {
            Destroy(gameObject);
        }

        OnPinchStateChanged += GameStart;
}

void OnDestroy()
    {
        OnPinchStateChanged -= GameStart;
    }

    void LateUpdate()
    {
        bool isPinching = leftHand.GetFingerIsPinching(OVRHand.HandFinger.Index);

        if (isPinching != _isCurrentlyPinching)
        {
            _isCurrentlyPinching = isPinching;

            OnPinchStateChanged?.Invoke(isPinching);
        }
    }



    private void GameStart(bool isPinching)
    {
        if (isPinching && currentStep == ScenarioStep.Briefing)
        {
            mainUI.alpha = 0f;
            mainUI.blocksRaycasts = false;
            currentStep = ScenarioStep.SurveyMeterInteraction;
            SoundManager.Instance.PlayNPC(SoundManager.Instance.NPC1);
            uIText.text = "제염 미션 완료. 결과 리포트를 출력합니다";
        }
    }

    public void GameResult()
    {
        mainUI.alpha = 255f;
        mainUI.blocksRaycasts = true;
    }
}