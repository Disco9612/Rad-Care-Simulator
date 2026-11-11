using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
[InitializeOnLoad]
public static class TutorialCompletionCheck
{
 static TutorialCompletionCheck(){EditorApplication.update+=Poll;}
 static void Poll(){if(EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists("Temp/TutorialCompletionCheck.request"))return;var command=File.ReadAllText("Temp/TutorialCompletionCheck.request").Trim();File.Delete("Temp/TutorialCompletionCheck.request");try{
  if(command=="enable-hand"){var scenes=EditorBuildSettings.scenes;foreach(var scene in scenes)if(scene.path=="Assets/Scenes/Emergency_Decon_Tutorial_Hand.unity")scene.enabled=true;EditorBuildSettings.scenes=scenes;}
  var catalog=Resources.Load<TrainingCatalog>("TrainingCatalog");
  string[] tutorials={"Emergency_Decon_Tutorial","Emergency_Decon_Tutorial_Hand","Hospital_Tutorial"};
  foreach(var tutorial in tutorials){string path="Assets/Scenes/"+tutorial+".unity";var target=TutorialCompletionMenu.EvaluationFor(catalog,path);string expected="Assets/Scenes/"+(tutorial.StartsWith("Hospital")?"Hospital_Evaluation":"Emergency_Decon_Evaluation")+".unity";if(target!=expected)throw new Exception("Wrong evaluation: "+tutorial);foreach(var destination in new[]{path,target,catalog.lobbyScene})if(!EditorBuildSettings.scenes.Any(s=>s.enabled&&s.path==destination))throw new Exception("Not enabled in build: "+destination);}
  if(TutorialCompletionMenu.EvaluationFor(catalog,"Assets/Scenes/Hospital_Evaluation.unity")!=null)throw new Exception("Evaluation installs tutorial menu");
  if(TutorialCompletionMenu.FormatDuration(59.99f)!="0분 59초" || TutorialCompletionMenu.FormatDuration(60f)!="1분 00초" || TutorialCompletionMenu.FormatDuration(187.9f)!="3분 07초")throw new Exception("Duration formatting failed");
  File.WriteAllText("Temp/TutorialCompletionCheck-result.txt","Compiled. All three tutorials route to enabled lobby/retry/evaluation scenes; evaluation excluded.");
 }catch(Exception e){File.WriteAllText("Temp/TutorialCompletionCheck-error.txt",e.ToString());Debug.LogException(e);}}
}
