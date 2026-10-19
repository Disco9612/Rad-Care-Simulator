using UnityEngine;

public enum EvaluationToolKind
{
    SurveyMeter,
    Saline,
    Forceps
}

public abstract class EvaluationTool : MonoBehaviour
{
    public GameObject placeHolder;
    protected Transform grabInteraction;
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private Rigidbody body;

    public abstract EvaluationToolKind Kind { get; }
    protected EvaluationGameManager Manager => EvaluationGameManager.Instance;
    protected bool IsInUse => Manager != null && Manager.IsToolInUse(this);

    protected virtual void Awake()
    {
        initialPosition = transform.position;
        initialRotation = transform.rotation;
        body = GetComponent<Rigidbody>();
        grabInteraction = transform.Find("[BuildingBlock] HandGrabInstallationRoutine");
        DisableGlow();
        SetGrabbable(false);
        if (placeHolder != null)
        {
            foreach (Renderer visual in placeHolder.GetComponentsInChildren<Renderer>(true))
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
            placeHolder.SetActive(false);
        }
    }

    protected virtual void Start()
    {
        Manager.OnStepChanged += HandleStepChanged;
        HandleStepChanged(Manager.currentStep);
    }

    protected virtual void OnDestroy()
    {
        if (Manager != null)
        {
            Manager.OnStepChanged -= HandleStepChanged;
        }
    }

    protected virtual void HandleStepChanged(EvaluationStep step)
    {
        SetGrabbable(Manager.IsRunning && !Manager.IsRestarting);
        if (placeHolder != null)
        {
            placeHolder.SetActive(Manager.CanReturn(Kind));
        }
    }

    public void SetGrabbable(bool enabled)
    {
        if (grabInteraction != null)
        {
            grabInteraction.gameObject.SetActive(enabled);
        }
    }

    protected bool TryReturn(Collider other)
    {
        if (!IsInUse || placeHolder == null || !placeHolder.activeInHierarchy
            || !other.transform.IsChildOf(placeHolder.transform) || !Manager.CanReturn(Kind))
        {
            return false;
        }
        Manager.Grabber.ReleaseTool(this);
        SetGrabbable(false);
        SetPose(placeHolder.transform.position, placeHolder.transform.rotation);
        placeHolder.SetActive(false);
        Manager.ReportToolReturned(Kind);
        SetGrabbable(Manager.IsRunning && !Manager.IsRestarting);
        return true;
    }

    public virtual void CaptureStageState() { }

    public virtual void ResetForStage()
    {
        StopAllCoroutines();
        CancelInvoke();
        SetGrabbable(false);
        SetPose(initialPosition, initialRotation);
    }

    private void SetPose(Vector3 position, Quaternion rotation)
    {
        transform.SetPositionAndRotation(position, rotation);
        if (body != null)
        {
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.position = position;
            body.rotation = rotation;
        }
    }

    protected void DisableGlow()
    {
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
}
