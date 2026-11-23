using System.Collections.Generic;
using Oculus.Interaction.HandGrab;
using UnityEngine;

/// <summary>
/// Prevents hand-attached decontamination tools from passing through solid scene
/// surfaces while preserving their trigger colliders for gameplay interactions.
/// </summary>
public static class ToolSurfaceMotionGuard
{
    private const float MaximumSampleSpacing = 0.025f;
    private const int MaximumSweepSamples = 16;
    private const int MaximumOverlaps = 32;
    private static readonly Collider[] overlapBuffer = new Collider[MaximumOverlaps];

    public sealed class State
    {
        public Transform Root;
        public Vector3 SafePosition;
        public Quaternion SafeRotation;
        public bool HasSafePose;
    }

    public static void Begin(State state, HandGrabInteractable held)
    {
        state.Root = FindToolRoot(held);
        state.HasSafePose = false;
    }

    public static void Reset(State state)
    {
        state.Root = null;
        state.HasSafePose = false;
    }

    public static void Constrain(State state, Transform rigRoot, HandGrabInteractable held)
    {
        if (state.Root == null || held == null || !state.Root.gameObject.activeInHierarchy)
        {
            return;
        }

        Transform toolRoot = state.Root;
        Vector3 desiredPosition = toolRoot.position;
        Quaternion desiredRotation = toolRoot.rotation;
        if (!state.HasSafePose)
        {
            state.SafePosition = desiredPosition;
            state.SafeRotation = desiredRotation;
            state.HasSafePose = true;
            return;
        }

        Vector3 startPosition = state.SafePosition;
        Quaternion startRotation = state.SafeRotation;
        float distance = Vector3.Distance(startPosition, desiredPosition);
        float angle = Quaternion.Angle(startRotation, desiredRotation);
        int samples = Mathf.Clamp(Mathf.Max(
            Mathf.CeilToInt(distance / MaximumSampleSpacing),
            Mathf.CeilToInt(angle / 8f)), 1, MaximumSweepSamples);

        Collider[] toolColliders = held.Colliders;
        if (toolColliders == null || toolColliders.Length == 0)
        {
            state.SafePosition = desiredPosition;
            state.SafeRotation = desiredRotation;
            return;
        }

        Dictionary<Collider, Dictionary<Collider, float>> startingDepths =
            GetPenetrationDepths(toolRoot, rigRoot, toolColliders);
        int safeSample = 0;
        int blockedSample = -1;
        for (int sample = 1; sample <= samples; sample++)
        {
            float t = sample / (float)samples;
            toolRoot.SetPositionAndRotation(
                Vector3.Lerp(startPosition, desiredPosition, t),
                Quaternion.Slerp(startRotation, desiredRotation, t));
            Physics.SyncTransforms();
            if (PenetrationIncreased(toolRoot, rigRoot, toolColliders, startingDepths))
            {
                blockedSample = sample;
                break;
            }
            safeSample = sample;
        }

        if (blockedSample < 0)
        {
            state.SafePosition = desiredPosition;
            state.SafeRotation = desiredRotation;
            return;
        }

        // Refine the first colliding segment to keep tools close to the surface
        // without allowing them to tunnel between hand-tracking frames.
        float low = safeSample / (float)samples;
        float high = blockedSample / (float)samples;
        for (int i = 0; i < 6; i++)
        {
            float middle = (low + high) * 0.5f;
            toolRoot.SetPositionAndRotation(
                Vector3.Lerp(startPosition, desiredPosition, middle),
                Quaternion.Slerp(startRotation, desiredRotation, middle));
            Physics.SyncTransforms();
            if (PenetrationIncreased(toolRoot, rigRoot, toolColliders, startingDepths))
            {
                high = middle;
            }
            else
            {
                low = middle;
            }
        }

        state.SafePosition = Vector3.Lerp(startPosition, desiredPosition, low);
        state.SafeRotation = Quaternion.Slerp(startRotation, desiredRotation, low);
        toolRoot.SetPositionAndRotation(state.SafePosition, state.SafeRotation);
        Physics.SyncTransforms();
    }

    private static Dictionary<Collider, Dictionary<Collider, float>> GetPenetrationDepths(
        Transform toolRoot, Transform rigRoot, Collider[] toolColliders)
    {
        var depths = new Dictionary<Collider, Dictionary<Collider, float>>();
        foreach (Collider toolCollider in toolColliders)
        {
            if (toolCollider == null || !toolCollider.enabled || !toolCollider.gameObject.activeInHierarchy)
            {
                continue;
            }

            Bounds bounds = toolCollider.bounds;
            int count = Physics.OverlapBoxNonAlloc(bounds.center, bounds.extents + Vector3.one * 0.002f,
                overlapBuffer, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider surface = overlapBuffer[i];
                overlapBuffer[i] = null;
                if (!IsSolidSurface(surface, toolCollider, toolRoot, rigRoot))
                {
                    continue;
                }

                if (Physics.ComputePenetration(toolCollider, toolCollider.transform.position,
                    toolCollider.transform.rotation, surface, surface.transform.position,
                    surface.transform.rotation, out _, out float depth) && depth > 0.0005f)
                {
                    if (!depths.TryGetValue(toolCollider, out Dictionary<Collider, float> surfaceDepths))
                    {
                        surfaceDepths = new Dictionary<Collider, float>();
                        depths.Add(toolCollider, surfaceDepths);
                    }
                    surfaceDepths[surface] = depth;
                }
            }
        }
        return depths;
    }

    private static bool PenetrationIncreased(Transform toolRoot, Transform rigRoot,
        Collider[] toolColliders, Dictionary<Collider, Dictionary<Collider, float>> startingDepths)
    {
        foreach (Collider toolCollider in toolColliders)
        {
            if (toolCollider == null || !toolCollider.enabled || !toolCollider.gameObject.activeInHierarchy)
            {
                continue;
            }

            startingDepths.TryGetValue(toolCollider, out Dictionary<Collider, float> surfaceDepths);
            Bounds bounds = toolCollider.bounds;
            int count = Physics.OverlapBoxNonAlloc(bounds.center, bounds.extents + Vector3.one * 0.002f,
                overlapBuffer, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider surface = overlapBuffer[i];
                overlapBuffer[i] = null;
                if (!IsSolidSurface(surface, toolCollider, toolRoot, rigRoot))
                {
                    continue;
                }

                if (Physics.ComputePenetration(toolCollider, toolCollider.transform.position,
                    toolCollider.transform.rotation, surface, surface.transform.position,
                    surface.transform.rotation, out _, out float depth))
                {
                    float allowedDepth = surfaceDepths != null && surfaceDepths.TryGetValue(surface, out float initialDepth)
                        ? initialDepth + 0.0015f
                        : 0.0005f;
                    if (depth > allowedDepth)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool IsSolidSurface(Collider surface, Collider toolCollider, Transform toolRoot, Transform rigRoot)
    {
        return surface != null && surface.enabled && surface.gameObject.activeInHierarchy && !surface.isTrigger
            && surface.gameObject.scene == toolRoot.gameObject.scene
            && surface != toolCollider && surface.transform != toolRoot && !surface.transform.IsChildOf(toolRoot)
            && (rigRoot == null || (surface.transform != rigRoot && !surface.transform.IsChildOf(rigRoot)));
    }

    private static Transform FindToolRoot(HandGrabInteractable held)
    {
        if (held == null)
        {
            return null;
        }

        EvaluationTool evaluationTool = held.GetComponentInParent<EvaluationTool>();
        if (evaluationTool != null)
        {
            return evaluationTool.transform;
        }
        SalineBottle salineBottle = held.GetComponentInParent<SalineBottle>();
        if (salineBottle != null)
        {
            return salineBottle.transform;
        }
        SurveyMeter surveyMeter = held.GetComponentInParent<SurveyMeter>();
        if (surveyMeter != null)
        {
            return surveyMeter.transform;
        }
        Forceps forceps = held.GetComponentInParent<Forceps>();
        return forceps != null ? forceps.transform : held.transform;
    }
}
