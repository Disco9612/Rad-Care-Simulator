using Oculus.Interaction;
using Oculus.Interaction.Grab;
using Oculus.Interaction.GrabAPI;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Input;
using Oculus.Interaction.Input.Visuals;
using UnityEngine;

[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(OVRCameraRig))]
public class EvaluationControllerScene : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 1.5f;
    [SerializeField] private float deadZone = 0.15f;
    [SerializeField] private LayerMask obstacleLayer = 1 << 9;
    [SerializeField] private float collisionRadius = 0.2f;

    private OVRCameraRig cameraRig;

    private void Awake()
    {
        cameraRig = GetComponent<OVRCameraRig>();

        foreach (FromOVRControllerDataSource controllerSource in GetComponentsInChildren<FromOVRControllerDataSource>(true))
        {
            controllerSource.enabled = true;
            ActivateAncestors(controllerSource.transform);
        }

        foreach (OVRHand hand in GetComponentsInChildren<OVRHand>(true))
        {
            hand.m_showState = OVRInput.InputDeviceShowState.Always;
            hand.enabled = true;
            ActivateAncestors(hand.transform);
        }

        foreach (HandJoystickLocomotion locomotion in GetComponentsInChildren<HandJoystickLocomotion>(true))
        {
            locomotion.enabled = false;
        }

        foreach (GrabInteractor interactor in GetComponentsInChildren<GrabInteractor>(true))
        {
            interactor.gameObject.SetActive(false);
        }

        int controllerCount = 0;
        foreach (HandGrabInteractor interactor in GetComponentsInChildren<HandGrabInteractor>(true))
        {
            ControllerPinchInjector controllerInput = interactor.GetComponentInChildren<ControllerPinchInjector>(true);
            if (controllerInput == null)
            {
                interactor.gameObject.SetActive(false);
                continue;
            }

            interactor.InjectSupportedGrabTypes(GrabTypeFlags.Palm);
            interactor.enabled = true;
            controllerInput.enabled = true;
            ActivateAncestors(controllerInput.transform);
            controllerCount++;
        }

        foreach (HandVisual handVisual in GetComponentsInChildren<HandVisual>(true))
        {
            handVisual.enabled = true;
            ActivateAncestors(handVisual.transform);
        }

        foreach (ControllerVisual controllerVisual in GetComponentsInChildren<ControllerVisual>(true))
        {
            controllerVisual.ForceOffVisibility = true;
            controllerVisual.gameObject.SetActive(false);
        }

        foreach (OVRControllerHelper controllerModel in GetComponentsInChildren<OVRControllerHelper>(true))
        {
            controllerModel.gameObject.SetActive(false);
        }

        if (controllerCount != 2)
        {
            Debug.LogError($"Expected two controller hand grab interactors, found {controllerCount}.", this);
        }
    }

    private void ActivateAncestors(Transform child)
    {
        for (Transform ancestor = child; ancestor != transform && ancestor != null; ancestor = ancestor.parent)
        {
            ancestor.gameObject.SetActive(true);
        }
    }

    private void Update()
    {
        if ((EvaluationGameManager.Instance != null && EvaluationGameManager.Instance.IsRestarting)
            || !OVRManager.hasInputFocus || !OVRInput.IsControllerConnected(OVRInput.Controller.LTouch))
        {
            return;
        }

        Vector2 stick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch);
        if (stick.magnitude <= deadZone)
        {
            return;
        }

        Transform head = cameraRig.centerEyeAnchor;
        Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Vector3 motion = (forward * stick.y + right * stick.x) * (moveSpeed * Time.deltaTime);
        if (motion.sqrMagnitude == 0f)
        {
            return;
        }

        if (!Physics.CheckSphere(head.position, collisionRadius, obstacleLayer, QueryTriggerInteraction.Ignore)
            && !Physics.SphereCast(head.position, collisionRadius, motion.normalized, out _,
                motion.magnitude, obstacleLayer, QueryTriggerInteraction.Ignore))
        {
            transform.position += motion;
        }
    }
}
