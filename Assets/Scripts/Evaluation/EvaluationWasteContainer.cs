using UnityEngine;

public class EvaluationWasteContainer : MonoBehaviour
{
    public GameObject openContainer;
    public GameObject closeContainer;
    private bool checkpointOpen;
    private bool checkpointClosed;

    private void Awake()
    {
        if (closeContainer != null)
        {
            closeContainer.SetActive(false);
        }
        foreach (Renderer visual in GetComponentsInChildren<Renderer>(true))
        {
            foreach (Material material in visual.materials)
            {
                if (material.HasProperty("_EmissionColor"))
                {
                    material.SetColor("_EmissionColor", Color.black);
                }
                material.DisableKeyword("_EMISSION");
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        EvaluationForceps forceps = other.GetComponentInParent<EvaluationForceps>();
        if (forceps != null)
        {
            TryDispose(forceps);
        }
    }

    public void TryDispose(EvaluationForceps forceps)
    {
        EvaluationGameManager manager = EvaluationGameManager.Instance;
        if (forceps == null || !manager.IsToolInUse(forceps) || !manager.ReportWasteDisposed())
        {
            return;
        }
        forceps.ApplyDisposalVisuals();
        closeContainer.SetActive(true);
        openContainer.SetActive(false);
    }

    public void CaptureStageState()
    {
        checkpointOpen = openContainer.activeSelf;
        checkpointClosed = closeContainer.activeSelf;
    }

    public void ResetForStage()
    {
        openContainer.SetActive(checkpointOpen);
        closeContainer.SetActive(checkpointClosed);
    }
}
