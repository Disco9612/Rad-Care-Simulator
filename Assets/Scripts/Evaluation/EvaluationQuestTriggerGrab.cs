using System.Collections.Generic;
using Oculus.Interaction.Grab;
using Oculus.Interaction.GrabAPI;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Input;
using UnityEngine;

[DefaultExecutionOrder(-90)]
[RequireComponent(typeof(OVRCameraRig))]
public class EvaluationQuestTriggerGrab : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float grabRadius = 0.12f;
    [SerializeField] private Vector3 forcepsPalmOffset = new Vector3(0f, 0f, 0.015f);
    [SerializeField] private Vector3 forcepsRotationOffset = Vector3.zero;

    private sealed class HandBinding
    {
        public HandGrabInteractor Interactor;
        public OVRInput.Controller Controller;
        public HandGrabInteractable Held;
        public bool Approved;
    }

    private readonly List<HandBinding> hands = new List<HandBinding>();
    private readonly Dictionary<HandGrabInteractable, HandGrabPose> gripPoses = new Dictionary<HandGrabInteractable, HandGrabPose>();
    private HandGrabInteractable[] tools;
    private bool waitForTriggerRelease;

    private void Start()
    {
        tools = FindObjectsByType<HandGrabInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (HandGrabInteractor interactor in GetComponentsInChildren<HandGrabInteractor>(true))
        {
            if (interactor.GetComponentInChildren<ControllerPinchInjector>(true) == null || interactor.Hand == null)
            {
                continue;
            }

            interactor.InjectSupportedGrabTypes(GrabTypeFlags.Palm);
            interactor.HoverOnZeroStrength = true;
            interactor.InjectOptionalGripCollider(null);
            interactor.SetComputeShouldSelectOverride(() => false, false);
            hands.Add(new HandBinding
            {
                Interactor = interactor,
                Controller = interactor.Hand.Handedness == Handedness.Left
                    ? OVRInput.Controller.LTouch
                    : OVRInput.Controller.RTouch
            });
        }
    }

    private void Update()
    {
        EvaluationGameManager manager = EvaluationGameManager.Instance;
        if (manager == null || !manager.CanAcceptGrab)
        {
            return;
        }
        if (waitForTriggerRelease)
        {
            if (OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.LTouch)
                || OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
            {
                return;
            }
            waitForTriggerRelease = false;
        }
        foreach (HandBinding hand in hands)
        {
            if (hand.Held != null && !hand.Held.isActiveAndEnabled)
            {
                Release(hand);
            }

            if (!OVRManager.hasInputFocus || !OVRInput.IsControllerConnected(hand.Controller)
                || !hand.Interactor.isActiveAndEnabled || !hand.Interactor.Hand.IsTrackedDataValid)
            {
                continue;
            }

            if (hand.Held != null)
            {
                if (!hand.Approved && hand.Interactor.SelectedInteractable == hand.Held)
                {
                    EvaluationTool selectedTool = hand.Held.GetComponentInParent<EvaluationTool>();
                    hand.Approved = manager.ReportToolGrab(selectedTool);
                    if (!hand.Approved)
                    {
                        return;
                    }
                }
                if (hand.Interactor.SelectedInteractable != hand.Held)
                {
                    hand.Interactor.ForceSelect(hand.Held, false);
                }
                continue;
            }

            if (!OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, hand.Controller))
            {
                continue;
            }

            HandGrabInteractable target = FindClosestTool(hand);
            if (target != null)
            {
                ConfigureGripPose(hand, target);
                hand.Held = target;
                hand.Approved = false;
                hand.Interactor.ForceSelect(target, false);
            }
        }
    }

    private void ConfigureGripPose(HandBinding hand, HandGrabInteractable target)
    {
        bool isBottle = target.GetComponentInParent<EvaluationSalineBottle>() != null;
        bool isForceps = target.GetComponentInParent<EvaluationForceps>() != null;
        if ((!isBottle && !isForceps) || hand.Interactor.PalmPoint == null)
        {
            return;
        }

        bool hasGripPose = gripPoses.TryGetValue(target, out HandGrabPose gripPose);
        if (!hasGripPose && target.HandGrabPoses.Count != 0)
        {
            return;
        }

        Collider gripCollider = null;
        foreach (Collider collider in target.Colliders)
        {
            if (collider != null && collider.enabled && collider.gameObject.activeInHierarchy
                && (isBottle || collider.name == "Forceps_Colliders"))
            {
                gripCollider = collider;
                break;
            }
        }

        if (gripCollider == null)
        {
            return;
        }

        if (!hasGripPose)
        {
            GameObject gripObject = new GameObject("Controller Grip Pose");
            gripObject.transform.SetParent(target.RelativeTo, false);
            gripPose = gripObject.AddComponent<HandGrabPose>();
            gripPose.InjectRelativeTo(target.RelativeTo);
            gripPose.InjectOptionalHandPose(null);
            target.HandGrabPoses.Add(gripPose);
            gripPoses.Add(target, gripPose);
        }

        Vector3 gripPosition = gripCollider.bounds.center;
        Quaternion gripRotation = hand.Interactor.PalmPoint.rotation;
        if (isForceps && TryGetHandFrame(hand.Interactor.Hand, out Quaternion handFrame))
        {
            Quaternion toolFrame = Quaternion.LookRotation(gripCollider.transform.up, gripCollider.transform.forward);
            Quaternion desiredFrame = handFrame * Quaternion.Euler(forcepsRotationOffset);
            Quaternion toolRotation = desiredFrame * Quaternion.Inverse(toolFrame);
            gripRotation = Quaternion.Inverse(toolRotation) * gripRotation;
            gripPosition -= Quaternion.Inverse(toolRotation) * (handFrame * forcepsPalmOffset);
        }
        gripPose.transform.SetPositionAndRotation(gripPosition, gripRotation);
    }

    private static bool TryGetHandFrame(IHand hand, out Quaternion frame)
    {
        frame = Quaternion.identity;
        if (!hand.GetRootPose(out Pose wrist)
            || !hand.GetJointPose(HandJointId.HandMiddle1, out Pose middle)
            || !hand.GetJointPose(HandJointId.HandIndex1, out Pose index)
            || !hand.GetJointPose(HandJointId.HandPinky1, out Pose pinky))
        {
            return false;
        }

        Vector3 forward = middle.position - wrist.position;
        Vector3 across = index.position - pinky.position;
        Vector3 normal = Vector3.Cross(forward, across);
        if (forward.sqrMagnitude < 0.000001f || normal.sqrMagnitude < 0.00000001f)
        {
            return false;
        }
        if (hand.Handedness == Handedness.Left)
        {
            normal = -normal;
        }
        frame = Quaternion.LookRotation(forward, normal);
        return true;
    }

    private HandGrabInteractable FindClosestTool(HandBinding hand)
    {
        Vector3 position = hand.Interactor.WristPoint.position;
        float closestDistance = grabRadius * grabRadius;
        HandGrabInteractable closest = null;

        foreach (HandGrabInteractable target in tools)
        {
            if (target == null || !target.isActiveAndEnabled || target.gameObject.scene != gameObject.scene
                || target.Colliders == null || IsHeld(target) || !hand.Interactor.CanSelect(target))
            {
                continue;
            }

            if (target.GetComponentInParent<EvaluationSurveyMeter>() == null
                && target.GetComponentInParent<EvaluationSalineBottle>() == null
                && target.GetComponentInParent<EvaluationForceps>() == null)
            {
                continue;
            }

            foreach (Collider collider in target.Colliders)
            {
                if (!collider.enabled || !collider.gameObject.activeInHierarchy)
                {
                    continue;
                }

                float distance = (collider.ClosestPoint(position) - position).sqrMagnitude;
                if (distance <= closestDistance)
                {
                    closestDistance = distance;
                    closest = target;
                }
            }
        }

        return closest;
    }

    private bool IsHeld(HandGrabInteractable target)
    {
        foreach (HandBinding hand in hands)
        {
            if (hand.Held == target)
            {
                return true;
            }
        }
        return false;
    }

    private static void Release(HandBinding hand)
    {
        hand.Interactor.ForceRelease();
        hand.Interactor.SetComputeShouldSelectOverride(() => false, false);
        hand.Held = null;
        hand.Approved = false;
    }

    public bool IsHolding(EvaluationTool tool)
    {
        foreach (HandBinding hand in hands)
        {
            if (hand.Approved && hand.Held != null && hand.Interactor.SelectedInteractable == hand.Held
                && hand.Held.GetComponentInParent<EvaluationTool>() == tool)
            {
                return true;
            }
        }
        return false;
    }

    public void ReleaseTool(EvaluationTool tool)
    {
        foreach (HandBinding hand in hands)
        {
            if (hand.Held != null && hand.Held.GetComponentInParent<EvaluationTool>() == tool)
            {
                Release(hand);
            }
        }
    }

    public void ReleaseAll()
    {
        waitForTriggerRelease = true;
        foreach (HandBinding hand in hands)
        {
            if (hand.Interactor != null)
            {
                Release(hand);
            }
        }
    }
    private void OnDisable()
    {
        ReleaseAll();
    }
}
