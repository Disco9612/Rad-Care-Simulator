using UnityEngine;
// Desktop Game-view focus must not block Quest Link after the headset is put back on.
public static class TrainingInputFocus
{
    public static bool Available => (Application.isEditor || Application.isFocused) && OVRManager.hasInputFocus;
}
