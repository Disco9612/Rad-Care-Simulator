using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RadCare.HospitalTreatment.Editor
{
    [InitializeOnLoad] public static class HospitalNarrationInstaller
    {
        [Serializable] class Manifest { public Line[] clips; }
        [Serializable] class Line { public string key,role,text; }
        static HospitalNarrationInstaller(){EditorApplication.update+=Poll;}
        static void Poll(){
            const string request="Temp/HospitalNarration.request";
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists(request))return;
            File.Delete(request);
            try{Install();}catch(Exception e){File.WriteAllText("Temp/HospitalNarration-error.txt",e.ToString());}
        }
        static void Install(){
            var scene=SceneManager.GetActiveScene();
            if(EditorApplication.isPlaying||scene.path!="Assets/Scenes/Hospital_Tutorial.unity")throw new Exception("Hospital edit mode required");
            AssetDatabase.Refresh();
            var scenario=UnityEngine.Object.FindAnyObjectByType<HospitalScenario>();
            var narration=scenario.GetComponent<HospitalNarration>()??scenario.gameObject.AddComponent<HospitalNarration>();
            narration.scenario=scenario;
            if(narration.source==null)narration.source=scenario.gameObject.AddComponent<AudioSource>();
            narration.source.playOnAwake=false;narration.source.loop=false;narration.source.spatialBlend=0;narration.source.priority=32;
            var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText("Documentation/HospitalTreatment/Voice/manifest.json"));
            narration.cues=manifest.clips.Select(line=>{
                var path="Assets/Scripts/Hospital_Treatment/Audio/"+line.key+".wav";
                var importer=(AudioImporter)AssetImporter.GetAtPath(path);
                var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.Vorbis;settings.quality=.7f;
                settings.preloadAudioData=true;importer.defaultSampleSettings=settings;importer.forceToMono=true;importer.SaveAndReimport();
                var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if(clip==null||clip.length<.2f)throw new Exception("Invalid speech clip: "+line.key);
                return new HospitalNarration.Cue{key=line.key,clip=clip,patient=line.role=="patient"};
            }).ToArray();
            for(int i=0;i<HospitalProcedure.Steps.Length;i++)if(!narration.cues.Any(c=>c.key==HospitalProcedure.NarrationKey(i)))throw new Exception("Missing step voice "+i);
            EditorUtility.SetDirty(narration);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            File.WriteAllText("Temp/HospitalNarration-installed.txt",$"{narration.cues.Length} Korean speech clips bound; {HospitalProcedure.Steps.Length} steps covered; offline AudioSource playback.");
        }
    }
}
