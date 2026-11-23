using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static partial class EmergencyLightingWorkbench
{
    private const string ZoneRootName = "Emergency Decon Floor Markings";
    private const string EnvironmentFolder = "Assets/EmergencyDeconEnvironment";
    private const float TapeLength = 0.18f;
    private const float TapeWidth = 0.065f;

    [MenuItem("Tools/RadCare/Emergency Decon/Apply Background Zone Markings")]
    public static void ApplyDeconZones()
    {
        if (EditorApplication.isPlaying)
        {
            throw new InvalidOperationException("Stop Play mode before applying scene polish.");
        }

        Scene initial = SceneManager.GetActiveScene();
        if (initial.isDirty)
        {
            throw new InvalidOperationException("Save the current scene edits before applying scene polish.");
        }

        string returnPath = string.IsNullOrEmpty(initial.path) ? Scenes[0] : initial.path;
        EnsureEnvironmentAssets();
        Material cautionYellow = AssetDatabase.LoadAssetAtPath<Material>(EnvironmentFolder + "/CautionYellow.mat");
        Material cautionCharcoal = AssetDatabase.LoadAssetAtPath<Material>(EnvironmentFolder + "/CautionCharcoal.mat");
        Material cleanTeal = AssetDatabase.LoadAssetAtPath<Material>(EnvironmentFolder + "/CleanZoneTeal.mat");
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        var report = new System.Collections.Generic.List<string>();

        foreach (string scenePath in Scenes)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath);
            Transform tent = All<Transform>(scene).FirstOrDefault(t => t.parent == null && t.name == "Tent");
            if (tent == null)
            {
                throw new InvalidOperationException("Tent environment root not found in " + scene.name);
            }

            GameObject existing = tent.Find(ZoneRootName)?.gameObject;
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing);
            }

            Transform firstBed = tent.Find("SM_MedicalBed_01a (2)");
            Transform secondBed = tent.Find("SM_MedicalBed_01a (3)");
            Transform supplyTable = tent.Find("SM_Folding_Table_01a");
            Transform floor = tent.Find("SM_FloorTarp_01a");
            if (firstBed == null || secondBed == null || supplyTable == null || floor == null)
            {
                throw new InvalidOperationException("Expected medical beds, supply table, and floor tarp in " + scene.name);
            }

            GameObject rootObject = new GameObject(ZoneRootName);
            Undo.RegisterCreatedObjectUndo(rootObject, "Add emergency decon zone markings");
            Transform root = rootObject.transform;
            root.SetParent(tent, true);

            Bounds bedArea = Encapsulate(RendererBounds(firstBed), RendererBounds(secondBed));
            float floorY = RendererBounds(floor).max.y;
            float edgeY = floorY + 0.004f;
            float minX = bedArea.min.x - 0.24f;
            float maxX = bedArea.max.x + 0.24f;
            float minZ = bedArea.min.z - 0.24f;
            float maxZ = bedArea.max.z + 0.24f;

            AddStrip(root, "Treatment bay front", minX, maxX, maxZ, edgeY, true, cautionYellow, cautionCharcoal);
            AddStrip(root, "Treatment bay rear", minX, maxX, minZ, edgeY, true, cautionYellow, cautionCharcoal);
            AddStrip(root, "Treatment bay left", minZ, maxZ, minX, edgeY, false, cautionYellow, cautionCharcoal);
            AddStrip(root, "Treatment bay right", minZ, maxZ, maxX, edgeY, false, cautionYellow, cautionCharcoal);

            float labelZ = maxZ - 0.09f;
            if (font != null)
            {
                AddFloorLabel(root, "Decon zone stencil", "DECON / CONTAMINATED ZONE",
                    new Vector3(bedArea.center.x, floorY + 0.009f, labelZ), 1.2f,
                    new Color(0.13f, 0.16f, 0.17f, 0.84f), font);
            }

            Bounds tableArea = RendererBounds(supplyTable);
            float supplyLineZ = tableArea.max.z + 0.16f;
            AddFlatLine(root, "Clean supply boundary", tableArea.min.x - 0.12f,
                tableArea.max.x + 0.12f, supplyLineZ, floorY + 0.004f, cleanTeal);
            if (font != null)
            {
                AddFloorLabel(root, "Clean supply stencil", "CLEAN SUPPLIES",
                    new Vector3(tableArea.center.x, floorY + 0.009f, supplyLineZ + 0.20f), 1.3f,
                    new Color(0.15f, 0.28f, 0.30f, 0.82f), font);
            }

            CombineZonePaintMeshes(root, scene.name, cautionYellow, cautionCharcoal, cleanTeal);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            report.Add(scene.name + ": treatment bay hazard boundary and clean supply floor markings applied.");
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(returnPath);
        System.IO.Directory.CreateDirectory("Temp/EmergencyLighting");
        System.IO.File.WriteAllLines("Temp/EmergencyLighting/zone-polish.txt", report);
    }

    private static void EnsureEnvironmentAssets()
    {
        if (!AssetDatabase.IsValidFolder(EnvironmentFolder))
        {
            AssetDatabase.CreateFolder("Assets", "EmergencyDeconEnvironment");
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            throw new InvalidOperationException("URP/Lit shader could not be found.");
        }

        CreateMaterial("CautionYellow", new Color32(193, 151, 54, 255), shader);
        CreateMaterial("CautionCharcoal", new Color32(47, 54, 56, 255), shader);
        CreateMaterial("CleanZoneTeal", new Color32(62, 112, 119, 255), shader);
    }

    private static void CreateMaterial(string name, Color color, Shader shader)
    {
        string path = EnvironmentFolder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", 0.18f);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
    }

    private static Bounds RendererBounds(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true)
            .Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
        if (renderers.Length == 0)
        {
            throw new InvalidOperationException("No active renderers found under " + root.name);
        }

        Bounds result = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            result.Encapsulate(renderers[i].bounds);
        }
        return result;
    }

    private static Bounds Encapsulate(Bounds first, Bounds second)
    {
        first.Encapsulate(second);
        return first;
    }

    private static void AddStrip(Transform parent, string name, float start, float end,
        float fixedCoordinate, float y, bool alongX, Material yellow, Material charcoal)
    {
        float span = end - start;
        int segmentCount = Mathf.CeilToInt(span / TapeLength);
        float segmentLength = span / segmentCount;
        for (int i = 0; i < segmentCount; i++)
        {
            float coordinate = start + (i + 0.5f) * segmentLength;
            Vector3 position = alongX
                ? new Vector3(coordinate, y, fixedCoordinate)
                : new Vector3(fixedCoordinate, y, coordinate);
            Vector3 scale = alongX
                ? new Vector3(segmentLength * 0.96f, 0.003f, TapeWidth)
                : new Vector3(TapeWidth, 0.003f, segmentLength * 0.96f);
            CreateMarking(parent, name + " " + i.ToString("D2"), position, scale,
                i % 2 == 0 ? yellow : charcoal);
        }
    }

    private static void AddFlatLine(Transform parent, string name, float startX, float endX,
        float z, float y, Material material)
    {
        CreateMarking(parent, name,
            new Vector3((startX + endX) * 0.5f, y, z), new Vector3(endX - startX, 0.003f, 0.018f), material);
    }

    private static GameObject CreateMarking(Transform parent, string name, Vector3 position,
        Vector3 scale, Material material)
    {
        GameObject marking = GameObject.CreatePrimitive(PrimitiveType.Cube);
        marking.name = name;
        marking.transform.SetParent(parent, true);
        marking.transform.SetPositionAndRotation(position, Quaternion.identity);
        marking.transform.localScale = scale;
        Collider collider = marking.GetComponent<Collider>();
        if (collider != null)
        {
            UnityEngine.Object.DestroyImmediate(collider);
        }
        Renderer renderer = marking.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = true;
        return marking;
    }

    private static void AddFloorLabel(Transform parent, string name, string value, Vector3 position,
        float size, Color color, TMP_FontAsset font)
    {
        GameObject label = new GameObject(name);
        label.transform.SetParent(parent, true);
        label.transform.SetPositionAndRotation(position, Quaternion.Euler(90f, 0f, 0f));
        TextMeshPro text = label.AddComponent<TextMeshPro>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.verticalAlignment = VerticalAlignmentOptions.Middle;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.margin = Vector4.zero;
        text.richText = false;
        text.renderer.shadowCastingMode = ShadowCastingMode.Off;
        text.renderer.receiveShadows = false;
        text.ForceMeshUpdate(true, true);
    }

    private static void CombineZonePaintMeshes(Transform root, string sceneName,
        Material cautionYellow, Material cautionCharcoal, Material cleanTeal)
    {
        string meshFolder = EnvironmentFolder + "/SceneMeshes";
        if (!AssetDatabase.IsValidFolder(meshFolder))
        {
            AssetDatabase.CreateFolder(EnvironmentFolder, "SceneMeshes");
        }

        CombineMaterialMeshes(root, sceneName, "Caution Yellow", cautionYellow, meshFolder);
        CombineMaterialMeshes(root, sceneName, "Caution Charcoal", cautionCharcoal, meshFolder);
        CombineMaterialMeshes(root, sceneName, "Clean Zone Teal", cleanTeal, meshFolder);
    }

    private static void CombineMaterialMeshes(Transform root, string sceneName, string label,
        Material material, string meshFolder)
    {
        MeshFilter[] sources = root.GetComponentsInChildren<MeshFilter>(true)
            .Where(filter => filter.sharedMesh != null
                && filter.GetComponent<MeshRenderer>() != null
                && filter.GetComponent<MeshRenderer>().sharedMaterial == material)
            .ToArray();
        if (sources.Length == 0)
        {
            return;
        }

        GameObject combinedObject = new GameObject(label);
        combinedObject.transform.SetParent(root, false);
        MeshFilter combinedFilter = combinedObject.AddComponent<MeshFilter>();
        MeshRenderer combinedRenderer = combinedObject.AddComponent<MeshRenderer>();
        var instances = new CombineInstance[sources.Length];
        Matrix4x4 worldToLocal = combinedObject.transform.worldToLocalMatrix;
        for (int i = 0; i < sources.Length; i++)
        {
            instances[i] = new CombineInstance
            {
                mesh = sources[i].sharedMesh,
                transform = worldToLocal * sources[i].transform.localToWorldMatrix
            };
        }

        var combinedMesh = new Mesh
        {
            name = sceneName + " " + label,
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
        };
        combinedMesh.CombineMeshes(instances, true, true, false);
        combinedMesh.RecalculateBounds();

        string assetPath = meshFolder + "/" + sceneName + "_" + label.Replace(' ', '_') + ".asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(combinedMesh, assetPath);
        }
        else
        {
            EditorUtility.CopySerialized(combinedMesh, existing);
            UnityEngine.Object.DestroyImmediate(combinedMesh);
            combinedMesh = existing;
            EditorUtility.SetDirty(existing);
        }

        combinedFilter.sharedMesh = combinedMesh;
        combinedRenderer.sharedMaterial = material;
        combinedRenderer.shadowCastingMode = ShadowCastingMode.Off;
        combinedRenderer.receiveShadows = true;

        foreach (MeshFilter source in sources)
        {
            if (source != null)
            {
                UnityEngine.Object.DestroyImmediate(source.gameObject);
            }
        }
    }
}
