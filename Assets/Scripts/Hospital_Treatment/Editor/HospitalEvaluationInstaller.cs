using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RadCare.HospitalTreatment.Editor
{
    [InitializeOnLoad] public static class HospitalEvaluationInstaller
    {
        const string Tutorial="Assets/Scenes/Hospital_Tutorial.unity";
        const string Evaluation="Assets/Scenes/Hospital_Evaluation.unity";
        const string Request="Temp/HospitalEvaluation.request";
        static HospitalEvaluationInstaller(){EditorApplication.update+=Poll;}
        static void Poll(){
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists(Request))return;
            string command=File.ReadAllText(Request).Trim();File.Delete(Request);
            try{if(command=="install")Install();else if(command=="audit")Audit();
                else if(command=="open-tutorial")EditorSceneManager.OpenScene(Tutorial,OpenSceneMode.Single);
                else if(command=="open-evaluation")EditorSceneManager.OpenScene(Evaluation,OpenSceneMode.Single);
                else throw new Exception("Unknown evaluation command");}
            catch(Exception e){File.WriteAllText("Temp/HospitalEvaluation-error.txt",e.ToString());Debug.LogException(e);}
        }
        [MenuItem("Tools/RadCare/Create Hospital Evaluation")]
        public static void Install(){
            if(EditorApplication.isPlaying)throw new Exception("Edit mode required");
            var active=SceneManager.GetActiveScene();if(active.isDirty)EditorSceneManager.SaveScene(active);
            if(!File.Exists(Evaluation)){
                var source=EditorSceneManager.OpenScene(Tutorial,OpenSceneMode.Single);
                if(!EditorSceneManager.SaveScene(source,Evaluation,true))throw new IOException("Evaluation scene copy failed");
            }
            var scene=EditorSceneManager.OpenScene(Evaluation,OpenSceneMode.Single);
            var scenario=UnityEngine.Object.FindAnyObjectByType<HospitalScenario>();if(scenario==null)throw new Exception("Hospital scenario missing");
            var evaluation=scenario.GetComponent<HospitalEvaluation>()??scenario.gameObject.AddComponent<HospitalEvaluation>();evaluation.scenario=scenario;
            var glow=scenario.GetComponent<HospitalObjectGlow>();if(glow!=null)glow.enabled=false;
            var irrigationGuide=scenario.GetComponent<HospitalIrrigationGuide>();if(irrigationGuide!=null)irrigationGuide.enabled=false;
            EditorUtility.SetDirty(evaluation);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            AssetDatabase.Refresh();
            var catalog=AssetDatabase.LoadAssetAtPath<TrainingCatalog>("Assets/Resources/TrainingCatalog.asset");if(catalog==null)throw new Exception("Training catalog missing");
            if(!catalog.scenarios.Any(x=>x!=null&&x.tutorialScene==Tutorial)){
                catalog.scenarios=catalog.scenarios.Concat(new[]{new TrainingCatalog.Scenario{
                    title="병원 상처 처치",description="환자 인계 · 상처 평가 · 세척 · 재평가 · 드레싱 및 기록",
                    tutorialScene=Tutorial,evaluationScene=Evaluation}}).ToArray();
                EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
            }
            var scenes=EditorBuildSettings.scenes.ToList();
            foreach(string path in new[]{Tutorial,Evaluation}){
                var entry=scenes.FirstOrDefault(x=>x.path==path);
                if(entry==null)scenes.Add(new EditorBuildSettingsScene(path,true));
                else entry.enabled=true;
            }
            EditorBuildSettings.scenes=scenes.ToArray();
            AssetDatabase.SaveAssets();
            Audit();
        }
        static void Audit(){
            var catalog=AssetDatabase.LoadAssetAtPath<TrainingCatalog>("Assets/Resources/TrainingCatalog.asset");
            var selection=catalog.scenarios.FirstOrDefault(x=>x!=null&&x.tutorialScene==Tutorial&&x.evaluationScene==Evaluation);
            if(selection==null)throw new Exception("Lobby catalog entry missing");
            if(!EditorBuildSettings.scenes.Any(x=>x.path==Tutorial&&x.enabled)||!EditorBuildSettings.scenes.Any(x=>x.path==Evaluation&&x.enabled))throw new Exception("Build scenes missing");
            if(SceneManager.GetActiveScene().path!=Evaluation)throw new Exception("Open the hospital evaluation scene");
            var scenario=UnityEngine.Object.FindAnyObjectByType<HospitalScenario>();
            if(scenario==null||scenario.GetComponent<HospitalEvaluation>()==null||scenario.targets.Length!=28)throw new Exception("Evaluation bindings incomplete");
            if(scenario.GetComponent<HospitalObjectGlow>().enabled||scenario.GetComponent<HospitalIrrigationGuide>().enabled)throw new Exception("Tutorial guidance remains enabled");
            if(scenario.GetComponent<HospitalNarration>()?.cues?.Length!=33)throw new Exception("Clinical voices missing");
            File.WriteAllText("Temp/HospitalEvaluation-installed.txt",$"Lobby: {selection.title}; scenes: {Tutorial}, {Evaluation}; targets: {scenario.targets.Length}; evaluation scoring ready; guidance off; role audio present.");
        }
    }
}
