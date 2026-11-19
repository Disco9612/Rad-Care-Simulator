using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

public static partial class EmergencyLightingWorkbench
{
    [MenuItem("Tools/RadCare/Emergency Lighting/Enhance Shadows in Three Scenes")]
    public static void EnhanceShadows()
    {
        if (EditorApplication.isPlaying || Lightmapping.isRunning)
            throw new InvalidOperationException("Stop play mode and baking before editing lighting.");
        const string docs = "Documentation/EmergencyLighting/ShadowRevision";
        Directory.CreateDirectory(docs);
        Directory.CreateDirectory(Out);
        var initial = SceneManager.GetActiveScene();
        if (!Scenes.Contains(initial.path))
        {
            if (initial.isDirty) throw new InvalidOperationException("Save the current scene before editing lighting.");
            initial = EditorSceneManager.OpenScene(Scenes[0], OpenSceneMode.Single);
        }
        if (initial.isDirty) EditorSceneManager.SaveScene(initial);
        string returnPath = initial.path;
        foreach (string pipeline in new[] { "Assets/Settings/Mobile_RPAsset.asset", "Assets/Settings/PC_RPAsset.asset" })
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(pipeline);
            var settings = new SerializedObject(asset);
            settings.FindProperty("m_AdditionalLightShadowsSupported").boolValue = true;
            settings.FindProperty("m_SoftShadowsSupported").boolValue = true;
            settings.FindProperty("m_AdditionalLightsShadowmapResolution").intValue = 2048;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }
        AssetDatabase.SaveAssets();
        var report = new List<string>();
        foreach (string path in Scenes)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var rig = scene.GetRootGameObjects().Single(o => o.name == RigName);
            var previous = rig.transform.Find("DirectionalTaskShadows");
            if (previous) previous.gameObject.SetActive(false);
            var fills = rig.GetComponentsInChildren<Light>().Where(l => l.name == "Mixed_Task_Fill").ToArray();
            foreach (var fill in fills) fill.intensity = 1f;
            Render(scene, "shadow-before");
            File.Copy(Out + "/" + scene.name + "-shadow-before.png", docs + "/" + scene.name + "-Before.png", true);
            if (previous) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            foreach (var fill in fills) fill.intensity = .35f;
            var group = new GameObject("DirectionalTaskShadows");
            group.transform.SetParent(rig.transform, false);
            Vector3[] positions = { new Vector3(-1.95f, 2.7f, -3.35f), new Vector3(2.45f, 2.7f, -.2f) };
            Vector3[] targets = { new Vector3(-.75f, .55f, -2.6f), new Vector3(1.6f, .5f, -.6f) };
            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject(i == 0 ? "Patient_Key_Shadow" : "Tent_Key_Shadow");
                go.transform.SetParent(group.transform, false);
                go.transform.position = positions[i];
                go.transform.LookAt(targets[i]);
                var light = go.AddComponent<Light>();
                light.type = LightType.Spot;
                light.lightmapBakeType = LightmapBakeType.Realtime;
                light.intensity = i == 0 ? 2.6f : 2.1f;
                light.range = 7f;
                light.spotAngle = 115f;
                light.innerSpotAngle = 70f;
                light.useColorTemperature = true;
                light.colorTemperature = i == 0 ? 5100 : 5700;
                light.shadows = LightShadows.Soft;
                light.shadowStrength = .85f;
                light.shadowBias = .035f;
                light.shadowNormalBias = .12f;
                light.shadowNearPlane = .12f;
                light.shadowCustomResolution = 1024;
            }
            foreach (var renderer in All<MeshRenderer>(scene))
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer.sharedMaterials.Any(m => m && m.renderQueue >= 3000)) continue;
                if (renderer.transform.root.name != "Patient" && renderer.transform.root.name != "Tent") continue;
                // Luminous ceiling diffusers must not occlude their own fixtures.
                if (renderer.name == "Diffuser") continue;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
            if (group.GetComponentsInChildren<Light>().Length != 2 || fills.Length != 2)
                throw new InvalidOperationException("Unexpected light rig structure: " + scene.name);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Render(scene, "shadow-after");
            File.Copy(Out + "/" + scene.name + "-shadow-after.png", docs + "/" + scene.name + "-After.png", true);
            report.Add(scene.name + ": two real-time soft-shadow spots; fill 0.35; baked data retained; PASS");
        }
        EditorSceneManager.OpenScene(returnPath, OpenSceneMode.Single);
        File.WriteAllLines(docs + "/validation.txt", report);
        File.WriteAllText(Out + "-status.txt", "SHADOWS COMPLETE");
    }
}
