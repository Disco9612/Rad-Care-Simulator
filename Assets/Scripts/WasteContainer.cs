using Oculus.Interaction;
using System.Collections;
using UnityEngine;

public class WasteContainer : MonoBehaviour
{
    public GameObject openContainer;
    public GameObject closeContainer;
    private Material[] _materials;
    private Coroutine _glowCoroutine;
    private Color[] _originalEmissionColors;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        GameManager.Instance.OnStepChanged += HandleStepChanged;
        closeContainer.SetActive(false);
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
    }
    void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnStepChanged -= HandleStepChanged;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.name.Contains("Forceps"))
        {
            SoundManager.Instance.PlayNPC(SoundManager.Instance.trashSound);
            SoundManager.Instance.PlayNPC(SoundManager.Instance.NPC10);
            closeContainer.SetActive(true);
            openContainer.SetActive(false);
        }
    }

    private void HandleStepChanged(ScenarioStep newStep)
    {
        if (_glowCoroutine != null)
        {
            StopCoroutine(_glowCoroutine);
            _glowCoroutine = null;
        }

        if (newStep == ScenarioStep.DisposeStage)
        {
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
