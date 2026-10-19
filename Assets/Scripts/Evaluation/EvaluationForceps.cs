using System.Collections;
using UnityEngine;

public class EvaluationForceps : EvaluationTool
{
    public GameObject targetObject;
    public float requiredTime1;
    public float requiredTime2;
    public float requiredTime3;
    public Material bloodMaterial;
    public Material emptyMaterial;
    public GameObject surveyMeter;

    private Renderer visual;
    private Transform wasteCollider;
    private Coroutine contactRoutine;
    private bool contacting;
    private bool checkpointTargetActive;
    private bool checkpointWasteActive;
    private Material[] checkpointMaterials;

    public override EvaluationToolKind Kind => EvaluationToolKind.Forceps;

    protected override void Awake()
    {
        base.Awake();
        visual = GetComponent<Renderer>();
        wasteCollider = transform.Find("ForcepsBall_Colliders");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (TryReturn(other))
        {
            return;
        }
        EvaluationWasteContainer container = other.GetComponentInParent<EvaluationWasteContainer>();
        if (container != null)
        {
            container.TryDispose(this);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (IsInUse && Manager.CanTreat && other.gameObject == targetObject && !contacting)
        {
            contacting = true;
            contactRoutine = StartCoroutine(ContactTimer());
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject == targetObject)
        {
            contacting = false;
            if (contactRoutine != null)
            {
                StopCoroutine(contactRoutine);
                contactRoutine = null;
            }
        }
    }

    private IEnumerator ContactTimer()
    {
        float elapsed = 0f;
        while (contacting && IsInUse && Manager.CanTreat)
        {
            elapsed += Time.deltaTime;
            if (elapsed >= requiredTime1)
            {
                Manager.ReportTreatmentProgress(1);
            }
            if (elapsed >= requiredTime2)
            {
                Manager.ReportTreatmentProgress(2);
            }
            if (elapsed >= requiredTime3)
            {
                Manager.ReportTreatmentProgress(3);
                if (Manager.ReportTreatmentCompleted())
                {
                    targetObject.SetActive(false);
                    SetFirstMaterial(bloodMaterial);
                }
                break;
            }
            yield return null;
        }
        contactRoutine = null;
    }

    public void ApplyDisposalVisuals()
    {
        SetFirstMaterial(emptyMaterial);
        if (wasteCollider != null)
        {
            wasteCollider.gameObject.SetActive(false);
        }
        if (placeHolder != null)
        {
            placeHolder.SetActive(Manager.CanReturnForceps);
        }
    }

    private void SetFirstMaterial(Material material)
    {
        if (visual == null || material == null)
        {
            return;
        }
        Material[] materials = visual.materials;
        if (materials.Length > 0)
        {
            materials[0] = material;
            visual.materials = materials;
            DisableGlow();
        }
    }

    public override void CaptureStageState()
    {
        checkpointTargetActive = targetObject != null && targetObject.activeSelf;
        checkpointWasteActive = wasteCollider != null && wasteCollider.gameObject.activeSelf;
        checkpointMaterials = visual == null ? null : visual.materials;
    }

    public override void ResetForStage()
    {
        base.ResetForStage();
        contacting = false;
        contactRoutine = null;
        if (targetObject != null)
        {
            targetObject.SetActive(checkpointTargetActive);
        }
        if (wasteCollider != null)
        {
            wasteCollider.gameObject.SetActive(checkpointWasteActive);
        }
        if (visual != null && checkpointMaterials != null)
        {
            visual.materials = checkpointMaterials;
        }
    }
}
