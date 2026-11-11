using System.Text;
using Oculus.Interaction.HandGrab;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

// Observe recovery without restarting XR or changing controller poses.
public sealed class TrainingInputDiagnostics : MonoBehaviour
{
    string previous;
    float nextSample;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if(FindAnyObjectByType<TrainingInputDiagnostics>()!=null)return;
        var root=new GameObject("Training input diagnostics");
        DontDestroyOnLoad(root);
        root.AddComponent<TrainingInputDiagnostics>();
    }
    void Update()
    {
        if(Time.unscaledTime<nextSample)return;
        nextSample=Time.unscaledTime+.5f;
        string state=Snapshot();
        if(state==previous)return;
        previous=state;
        Debug.Log("[TrainingInput] "+state,this);
    }
    public static string Snapshot()
    {
        var result=new StringBuilder();
        var manager=OVRManager.instance;
        result.Append($"scene={SceneManager.GetActiveScene().name} appFocus={Application.isFocused} xrFocus={OVRManager.hasInputFocus} allowed={TrainingInputFocus.Available} manager={manager!=null&&manager.isActiveAndEnabled} timeScale={Time.timeScale:0.##}");
        var head=InputDevices.GetDeviceAtXRNode(XRNode.Head);
        result.Append($" headValid={head.isValid} present={(head.TryGetFeatureValue(CommonUsages.userPresence,out bool present)?present.ToString():"unknown")}");
        AppendHand(result,XRNode.LeftHand,OVRInput.Controller.LTouch);
        AppendHand(result,XRNode.RightHand,OVRInput.Controller.RTouch);
        var rig=FindAnyObjectByType<OVRCameraRig>();
        if(rig!=null){
            var move=rig.GetComponent<QuestControllerScene>();
            var evalMove=rig.GetComponent<EvaluationControllerScene>();
            result.Append($" move={move!=null&&move.isActiveAndEnabled} evalMove={evalMove!=null&&evalMove.isActiveAndEnabled}");
            foreach(var hand in rig.GetComponentsInChildren<HandGrabInteractor>(true))
                if(hand.GetComponentInChildren<Oculus.Interaction.GrabAPI.ControllerPinchInjector>(true)!=null)
                    result.Append($" grab[{hand.Hand?.Handedness}]={hand.isActiveAndEnabled}/{hand.Hand?.IsTrackedDataValid}");
        }
        return result.ToString();
    }
    static void AppendHand(StringBuilder text,XRNode node,OVRInput.Controller controller)
    {
        var device=InputDevices.GetDeviceAtXRNode(node);
        device.TryGetFeatureValue(CommonUsages.isTracked,out bool tracked);
        device.TryGetFeatureValue(CommonUsages.primary2DAxis,out Vector2 stick);
        device.TryGetFeatureValue(CommonUsages.triggerButton,out bool trigger);
        var ovrStick=OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick,controller);
        text.Append($" {node}[id={device.GetHashCode()},valid={device.isValid},tracked={tracked},ovr={OVRInput.IsControllerConnected(controller)},stickXR={stick.sqrMagnitude>.0225f},stickOVR={ovrStick.sqrMagnitude>.0225f},triggerXR={trigger},triggerOVR={OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger,controller)}]");
    }
}
