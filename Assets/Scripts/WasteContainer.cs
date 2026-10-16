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
        Forceps forceps = other.GetComponentInParent<Forceps>();
        if (forceps != null)
        {
            TryDispose(forceps);
        }
    }

    public void TryDispose(Forceps forceps)
    {
        if (forceps == null || !GameManager.Instance.ReportWasteDisposed())
        {
            return;
        }
        forceps.ApplyDisposalVisuals();
        StopGlow();
        closeContainer.SetActive(true);
        openContainer.SetActive(false);
    }

    private void HandleStepChanged(ScenarioStep newStep)
    {
        StopGlow();
        if (GameManager.Instance.CanDispose)
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
                    Color baseColor = _originalEmissionColors[i] == Color.black ? Color.white : _originalEmissionColors[i];
                    float weakIntensity = 0.5f;
                    _materials[i].SetColor("_EmissionColor", baseColor * emission * weakIntensity);
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
