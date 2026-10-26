using UnityEngine;
namespace RadCare.HospitalTreatment {
    // Scene-specific fitted meshes; the original Studio mesh remains flat while carried.
    public sealed class HospitalPlacementShape : MonoBehaviour {
        public MeshFilter[] parts;
        public Mesh[] fittedMeshes;
        public void Apply() {
            for(int i=0;i<parts.Length;i++)parts[i].sharedMesh=fittedMeshes[i];
            foreach(var lod in GetComponentsInChildren<LODGroup>())lod.RecalculateBounds();
        }
    }
}
