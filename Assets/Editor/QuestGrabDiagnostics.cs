using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Oculus.Interaction.GrabAPI;
using Oculus.Interaction.HandGrab;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class QuestGrabDiagnostics
{
    private static double nextCapture;

    static QuestGrabDiagnostics()
    {
        EditorApplication.update += Capture;
    }

    private static void Capture()
    {
        if (EditorApplication.isCompiling || EditorApplication.timeSinceStartup < nextCapture
            || SceneManager.GetActiveScene().name != "MainScean_Controller")
        {
            return;
        }
        nextCapture = EditorApplication.timeSinceStartup + 2;
        try
        {
            var rig = UnityEngine.Object.FindAnyObjectByType<QuestTriggerGrab>();
            var bindings = rig == null ? null : typeof(QuestTriggerGrab).GetField("hands", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(rig) as System.Collections.ICollection;
            var data = new Snapshot
            {
                playing = EditorApplication.isPlaying,
                time = DateTime.Now.ToString("O"),
                scene = SceneManager.GetActiveScene().path,
                bindings = bindings?.Count ?? -1,
                focus = OVRManager.hasInputFocus,
                controllers = OVRInput.GetConnectedControllers().ToString(),
                step = GameManager.Instance == null ? "none" : GameManager.Instance.currentStep.ToString(),
                sources = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Where(source => source is Oculus.Interaction.Input.IController || source is Oculus.Interaction.OVR.OVRControllerInHandActiveState)
                    .Select(DescribeSource).ToArray(),
                interactors = UnityEngine.Object.FindObjectsByType<HandGrabInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Where(interactor => interactor.GetComponentInChildren<ControllerPinchInjector>(true) != null)
                    .Select(interactor => $"{DescribePath(interactor.transform)}: active={interactor.isActiveAndEnabled}, state={interactor.State}, hand={interactor.Hand?.Handedness}, valid={interactor.Hand?.IsTrackedDataValid}, selected={interactor.SelectedInteractable?.name}, wrist={interactor.WristPoint?.position}").ToArray(),
                tools = UnityEngine.Object.FindObjectsByType<HandGrabInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Select(tool => $"{tool.transform.parent?.name}: active={tool.isActiveAndEnabled}, colliders={tool.Colliders?.Length}, position={tool.Rigidbody?.position}").ToArray()
            };
            File.WriteAllText("Temp/QuestGrabDiagnostics.json", JsonUtility.ToJson(data, true));
        }
        catch (Exception exception)
        {
            File.WriteAllText("Temp/QuestGrabDiagnostics.json", exception.ToString());
        }
    }

    [Serializable]
    private class Snapshot
    {
        public bool playing;
        public string time;
        public string scene;
        public int bindings;
        public bool focus;
        public string controllers;
        public string step;
        public string[] interactors;
        public string[] tools;
        public string[] sources;
    }

    private static string DescribePath(Transform target)
    {
        string path = "";
        for (Transform ancestor = target; ancestor != null; ancestor = ancestor.parent)
        {
            path = ancestor.name + "[" + ancestor.gameObject.activeSelf + "]/" + path;
        }
        return path;
    }

    private static string DescribeSource(MonoBehaviour source)
    {
        string path = DescribePath(source.transform);
        try
        {
            return source is Oculus.Interaction.Input.IController controller
                ? $"{path} {source.GetType().Name} connected={controller.IsConnected}"
                : $"{path} active={((Oculus.Interaction.OVR.OVRControllerInHandActiveState)source).Active}";
        }
        catch (NullReferenceException)
        {
            return path + " not initialized";
        }
    }
}
