using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static partial class EmergencyLightingWorkbench
{
    [MenuItem("Tools/RadCare/Emergency Lighting/Enhance Fixture Glow")]
    public static void EnhanceFixtureGlow()
    {
        if (EditorApplication.isPlaying || Lightmapping.isRunning) throw new InvalidOperationException("Edit mode required.");
        var initial = SceneManager.GetActiveScene();
        if (initial.isDirty) throw new InvalidOperationException("Save the scene before applying fixture glow.");
        string returnPath = Scenes.Contains(initial.path) ? initial.path : Scenes[0];
        Directory.CreateDirectory(Out);
        const string profilePath = AssetsRoot + "/FixtureGlow.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
        if (!profile)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
        }
        if (!profile.TryGet<Bloom>(out var bloom))
        {
            bloom = profile.Add<Bloom>(false);
            AssetDatabase.AddObjectToAsset(bloom, profile);
        }
        bloom.active = true;
        bloom.threshold.Override(2.2f);
        bloom.intensity.Override(.22f);
        bloom.scatter.Override(.65f);
        bloom.highQualityFiltering.Override(false);
        EditorUtility.SetDirty(bloom);
        EditorUtility.SetDirty(profile);
        var diffuser = AssetDatabase.LoadAssetAtPath<Material>(AssetsRoot + "/LED_Diffuser.mat");
        // URP Unlit uses BaseColor, not EmissionColor, for its HDR radiance.
        diffuser.SetColor("_BaseColor", new Color(4.5f, 4.392f, 4.176f, 1f));
        EditorUtility.SetDirty(diffuser);
        foreach (string path in new[] { "Assets/Settings/Mobile_RPAsset.asset", "Assets/Settings/PC_RPAsset.asset" })
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            pipeline.supportsHDR = true;
            EditorUtility.SetDirty(pipeline);
        }
        AssetDatabase.SaveAssets();
        foreach (string path in Scenes)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var rig = scene.GetRootGameObjects().Single(o => o.name == RigName);
            // Replace the previously unused sample post profile with a bloom-only profile.
            // This avoids enabling unrelated sample color grading or vignette on XR cameras.
            var volumes = All<Volume>(scene).Where(v => v.isGlobal).ToArray();
            if (volumes.Length == 0) volumes = new[] { rig.AddComponent<Volume>() };
            foreach (var volume in volumes)
            {
                volume.isGlobal = true;
                volume.sharedProfile = profile;
                volume.weight = 1f;
            }
            foreach (var camera in All<Camera>(scene))
            {
                camera.allowHDR = true;
                var data = camera.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = true;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Render(scene, "glow-after");
        }
        EditorSceneManager.OpenScene(returnPath, OpenSceneMode.Single);
        File.WriteAllText(Out + "-status.txt", "FIXTURE GLOW COMPLETE");
    }
}
