using UnityEngine;

namespace RadCare.HospitalTreatment
{
    public enum HospitalInteraction { Action, Carry, Socket, Irrigator, WrongChoice }
    public sealed class HospitalInteractable : MonoBehaviour
    {
        public HospitalInteraction kind;
        public HospitalAction action;
        public string label;
        public string itemId;
        public Transform movable;
        public Transform tip;
        public bool retired;
        public Collider grabCollider;
        public Vector3 gripLocalPosition;
        public Vector3 heldEuler;
        public bool IsHeld { get; private set; }
        public bool IsMovable => kind==HospitalInteraction.Carry||kind==HospitalInteraction.Irrigator;
        public void SetHeld(bool held) { IsHeld=held; }
        [HideInInspector] public Vector3 homePosition;
        [HideInInspector] public Quaternion homeRotation;
        void Awake() {
            if(movable==null)movable=transform;homePosition=movable.position;homeRotation=movable.rotation;

        }
        public void SetHeldPose(Vector3 position,Quaternion aim) {
            // Editor calibration aligns the model's nozzle direction with the controller's forward axis.
            movable.SetPositionAndRotation(position,aim*Quaternion.Euler(heldEuler));
        }
        public void SetGripPose(Vector3 palm,Quaternion aim) {
            movable.rotation=aim*Quaternion.Euler(heldEuler);
            movable.position+=palm-movable.TransformPoint(gripLocalPosition);
        }
        public void ReturnHome() { IsHeld=false;movable.SetPositionAndRotation(homePosition,homeRotation); }
    }
}
