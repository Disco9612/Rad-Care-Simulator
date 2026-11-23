using System.Collections.Generic;
using System.Collections;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Installs solid contact geometry for the three decontamination scenes.</summary>
public static class DeconPhysicalContactInstaller
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Emergency_Decon_Evaluation"
            && scene.name != "Emergency_Decon_Tutorial"
            && scene.name != "Emergency_Decon_Tutorial_Hand")
        {
            return;
        }

        foreach (GameObject sceneRoot in scene.GetRootGameObjects())
        foreach (Transform root in sceneRoot.GetComponentsInChildren<Transform>(true))
        {
            if (!root.name.StartsWith("SM_MedicalBed_01a"))
            {
                continue;
            }

            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter.name.Contains("LOD0") || filter.sharedMesh == null
                    || filter.GetComponent<MeshCollider>() != null)
                {
                    continue;
                }
                MeshCollider collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                collider.convex = false;
                collider.isTrigger = false;
            }
        }

        var toolRoots = new HashSet<Transform>();
        foreach (EvaluationTool tool in Object.FindObjectsByType<EvaluationTool>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            AddTool(scene, tool.transform, toolRoots);
        foreach (SalineBottle tool in Object.FindObjectsByType<SalineBottle>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            AddTool(scene, tool.transform, toolRoots);
        foreach (SurveyMeter tool in Object.FindObjectsByType<SurveyMeter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            AddTool(scene, tool.transform, toolRoots);
        foreach (Forceps tool in Object.FindObjectsByType<Forceps>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            AddTool(scene, tool.transform, toolRoots);
    }

    private static void AddTool(Scene scene, Transform root, HashSet<Transform> seen)
    {
        if (root.gameObject.scene != scene || !seen.Add(root))
            return;

        DeconPhysicalContact contact = root.GetComponent<DeconPhysicalContact>();
        if (contact == null)
            contact = root.gameObject.AddComponent<DeconPhysicalContact>();
        contact.Configure();
    }
}

[DefaultExecutionOrder(20000)]
public sealed class DeconPhysicalContact : MonoBehaviour, ITransformer
{
    private readonly ToolSurfaceMotionGuard.State motion = new ToolSurfaceMotionGuard.State();
    private HandGrabInteractable interactable;
    private Transform rigRoot;
    private GrabFreeTransformer free;
    private IGrabbable owner;

    public void Configure()
    {
        interactable = GetComponentInChildren<HandGrabInteractable>(true);
        if (interactable == null)
            return;

        if (transform.Find("Physical Contact Collider") == null)
        {
            GameObject contactRoot = new GameObject("Physical Contact Collider");
            contactRoot.transform.SetParent(transform, false);
            foreach (MeshFilter filter in GetComponentsInChildren<MeshFilter>(true))
            {
                Renderer visual = filter.GetComponent<Renderer>();
                if (filter.sharedMesh == null || visual == null || !visual.enabled
                    || !filter.gameObject.activeInHierarchy) continue;
                GameObject part = new GameObject("Small contact - " + filter.name);
                part.transform.SetParent(filter.transform, false);
                BoxCollider box = part.AddComponent<BoxCollider>();
                box.center = filter.sharedMesh.bounds.center;
                box.size = filter.sharedMesh.bounds.size * 0.80f;
                box.isTrigger = false;
            }
        }

        OVRCameraRig rig = Object.FindFirstObjectByType<OVRCameraRig>();
        rigRoot = rig != null ? rig.transform.root : null;
        ToolSurfaceMotionGuard.Begin(motion, interactable);
    }

    public void AcceptTeleport()
    {
        if (interactable != null)
            ToolSurfaceMotionGuard.Begin(motion, interactable);
    }

    private IEnumerator Start()
    {
        // Wait for Grabbable.Start to initialize its default transformers.
        yield return null;
        Grabbable grab = GetComponent<Grabbable>();
        if (grab == null || interactable == null) yield break;
        free = gameObject.AddComponent<GrabFreeTransformer>();
        Initialize(grab);
        grab.InjectOptionalOneGrabTransformer(this);
        grab.InjectOptionalTwoGrabTransformer(this);
        grab.InjectOptionalKinematicWhileSelected(true);
    }

    public void Initialize(IGrabbable grabbable)
    {
        owner = grabbable;
        free.Initialize(grabbable);
    }

    public void BeginTransform()
    {
        free.BeginTransform();
        ToolSurfaceMotionGuard.Begin(motion, interactable);
        // Capture the pose before the grab transformer moves the tool.
        ToolSurfaceMotionGuard.Constrain(motion, rigRoot, interactable);
    }

    public void UpdateTransform()
    {
        free.UpdateTransform();
        ToolSurfaceMotionGuard.Constrain(motion, rigRoot, interactable);
    }

    public void EndTransform()
    {
        free.EndTransform();
        ToolSurfaceMotionGuard.Reset(motion);
    }
}
