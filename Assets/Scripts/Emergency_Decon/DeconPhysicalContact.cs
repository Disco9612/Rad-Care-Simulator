using System.Collections.Generic;
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
public sealed class DeconPhysicalContact : MonoBehaviour
{
    private readonly ToolSurfaceMotionGuard.State motion = new ToolSurfaceMotionGuard.State();
    private HandGrabInteractable interactable;
    private Transform rigRoot;

    public void Configure()
    {
        interactable = GetComponentInChildren<HandGrabInteractable>(true);
        if (interactable == null)
            return;

        if (transform.Find("Physical Contact Collider") == null)
        {
            Bounds localBounds = default;
            bool found = false;
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))
                    continue;
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy
                    || renderer.name.Contains("PlaceHolder"))
                    continue;

                Bounds worldBounds = renderer.bounds;
                for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 corner = transform.InverseTransformPoint(worldBounds.center
                        + Vector3.Scale(worldBounds.extents, new Vector3(x, y, z)));
                    if (!found)
                    {
                        localBounds = new Bounds(corner, Vector3.zero);
                        found = true;
                    }
                    else localBounds.Encapsulate(corner);
                }
            }

            if (found)
            {
                GameObject proxy = new GameObject("Physical Contact Collider");
                proxy.transform.SetParent(transform, false);
                BoxCollider collider = proxy.AddComponent<BoxCollider>();
                collider.center = localBounds.center;
                collider.size = Vector3.Max(localBounds.size, Vector3.one * 0.015f);
                collider.isTrigger = false;
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

    private void OnEnable() => Application.onBeforeRender += Constrain;
    private void OnDisable() => Application.onBeforeRender -= Constrain;
    private void LateUpdate() => Constrain();

    private void Constrain()
    {
        if (interactable != null)
            ToolSurfaceMotionGuard.Constrain(motion, rigRoot, interactable);
    }
}
