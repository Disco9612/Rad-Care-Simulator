using UnityEngine;
using UnityEngine.XR;

// Reacquire devices every frame: OpenXR device handles can change after headset sleep.
public sealed class TrainingMenuPointer : MonoBehaviour
{
    public bool Recovered { get; private set; }
    public bool Pressed { get; private set; }
    public Ray Ray { get; private set; }
    bool available, releaseRequired=true;
    string lastState;
    LineRenderer line;
    Material material;
    void OnEnable(){OVRManager.HMDMounted+=Mounted;OVRManager.HMDUnmounted+=Unmounted;OVRManager.InputFocusLost+=ResetInput;OVRManager.InputFocusAcquired+=ResetInput;}
    void OnDisable(){OVRManager.HMDMounted-=Mounted;OVRManager.HMDUnmounted-=Unmounted;OVRManager.InputFocusLost-=ResetInput;OVRManager.InputFocusAcquired-=ResetInput;ResetInput();}
    void Mounted(){ResetInput();}
    void Unmounted(){ResetInput();}
    void OnApplicationFocus(bool focus){ResetInput();}
    void OnApplicationPause(bool pause){ResetInput();}
    void ResetInput(){available=false;releaseRequired=true;Pressed=false;Hide();}
    public void Hide(){if(line!=null)line.enabled=false;}
    public bool Read(OVRCameraRig rig)
    {
        Recovered=false;Pressed=false;
        // Poll current presence rather than latching a possibly missed Mounted event.
        var head=InputDevices.GetDeviceAtXRNode(XRNode.Head);
        if(head.isValid && head.TryGetFeatureValue(CommonUsages.userPresence,out bool present) && !present)
        {ResetInput();Report("headset removed");return false;}
        if(!Application.isEditor && !Application.isFocused){ResetInput();Report("application unfocused");return false;}
        bool held;
        Ray ray;
        bool valid=Controller(rig,XRNode.RightHand,OVRInput.Controller.RTouch,out ray,out held)
            || Controller(rig,XRNode.LeftHand,OVRInput.Controller.LTouch,out ray,out held);
        if(!valid)
        {
            foreach(var hand in rig.GetComponentsInChildren<OVRHand>(true))
                if(hand.IsTracked && hand.IsPointerPoseValid){ray=new Ray(hand.PointerPose.position,hand.PointerPose.forward);held=hand.GetFingerIsPinching(OVRHand.HandFinger.Index);valid=true;break;}
        }
        if(!valid){ResetInput();Report("waiting for tracked controller/hand");return false;}
        Recovered=!available;available=true;Ray=ray;
        // Focus controls selection, not pointer visibility. Editor desktop focus is
        // independent of Quest Link input focus after taking off the headset.
        if(!OVRManager.hasInputFocus){releaseRequired=true;Report("pointer tracked; waiting for XR input focus");return true;}
        Report("pointer and XR input ready");
        if(!held)releaseRequired=false;
        Pressed=held&&!releaseRequired;
        return true;
    }
    static bool Controller(OVRCameraRig rig,XRNode node,OVRInput.Controller controller,out Ray ray,out bool held)
    {
        ray=default;held=false;
        var device=InputDevices.GetDeviceAtXRNode(node);
        bool xrTracked=device.isValid && device.TryGetFeatureValue(CommonUsages.isTracked,out bool tracked) && tracked;
        bool ovrConnected=OVRInput.IsControllerConnected(controller);
        if(!xrTracked && !ovrConnected)return false;
        // Match HospitalInput exactly: aim from the hand anchor, never the raw XR grip pose.
        var anchor=controller==OVRInput.Controller.RTouch?rig.rightHandAnchor:rig.leftHandAnchor;
        ray=new Ray(anchor.position,anchor.forward);
        held=OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger,controller);
        if(!ovrConnected && xrTracked)
        {
            if(device.TryGetFeatureValue(CommonUsages.triggerButton,out bool button))held=button;
            else if(device.TryGetFeatureValue(CommonUsages.trigger,out float trigger))held=trigger>.75f;
        }
        return true;
    }
    void Report(string state)
    {
        if(lastState==state)return;
        lastState=state;
        Debug.Log("[TrainingMenuPointer] "+state,this);
    }
    public void Draw(float distance)
    {
        if(line==null)
        {
            var root=new GameObject("Menu Ray");root.transform.SetParent(transform,false);
            line=root.AddComponent<LineRenderer>();line.useWorldSpace=true;line.positionCount=2;line.startWidth=.003f;line.endWidth=.0015f;
            material=new Material(Shader.Find("Sprites/Default"));line.sharedMaterial=material;
            line.startColor=line.endColor=new Color(.4f,1f,.9f);line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;line.receiveShadows=false;
        }
        line.enabled=true;line.SetPosition(0,Ray.origin);line.SetPosition(1,Ray.GetPoint(Mathf.Clamp(distance,.05f,5f)));
    }
    void OnDestroy(){if(material!=null)Destroy(material);}
}
