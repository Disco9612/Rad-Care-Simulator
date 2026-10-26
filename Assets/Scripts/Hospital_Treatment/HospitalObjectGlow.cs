using System;
using System.Collections.Generic;
using UnityEngine;
namespace RadCare.HospitalTreatment {
    public sealed class HospitalObjectGlow : MonoBehaviour {
        [Serializable] public class Binding { public HospitalInteractable target; public Renderer[] renderers; }
        public HospitalScenario scenario;
        public Binding[] bindings;
        public Renderer[] returnCart;
        readonly Dictionary<Renderer,MaterialPropertyBlock[]> originals=new Dictionary<Renderer,MaterialPropertyBlock[]>();
        readonly Dictionary<Renderer,Material[]> sourceMaterials=new Dictionary<Renderer,Material[]>();
        readonly List<Material> instances=new List<Material>();
        readonly HashSet<Renderer> active=new HashSet<Renderer>();
        MaterialPropertyBlock block;
        static readonly int Emission=Shader.PropertyToID("_EmissionColor");
        void LateUpdate(){RefreshGlow();}
        public void RefreshGlow() {
            if(block==null)block=new MaterialPropertyBlock();
            active.Clear();
            if(scenario.Ready&&!scenario.Procedure.Complete) {
                bool returning=(scenario.Procedure.Current.Action==HospitalAction.ClearWaste||scenario.Procedure.Current.Action==HospitalAction.RespondPain)&&scenario.input.Held!=null&&scenario.input.Held.kind==HospitalInteraction.Irrigator;
                if(returning) {foreach(var r in returnCart)if(r!=null)active.Add(r);}
                else foreach(var b in bindings) {
                    if(b.target==null||b.target.kind==HospitalInteraction.WrongChoice||!scenario.CanUse(b.target)||b.target.IsHeld)continue;
                    if(b.target.kind==HospitalInteraction.Socket&&scenario.input.Held==null)continue;
                    foreach(var r in b.renderers)if(r!=null)active.Add(r);
                }
            }
            foreach(var r in active)if(!originals.ContainsKey(r)) {
                sourceMaterials[r]=r.sharedMaterials;
                var local=r.materials;
                foreach(var material in local){material.EnableKeyword("_EMISSION");instances.Add(material);}
                var saved=new MaterialPropertyBlock[local.Length];
                for(int i=0;i<saved.Length;i++){saved[i]=new MaterialPropertyBlock();r.GetPropertyBlock(saved[i],i);}
                originals.Add(r,saved);
            }
            float pulse=.35f+1.3f*(.5f+.5f*Mathf.Sin(Time.unscaledTime*3));
            foreach(var pair in originals) {
                var r=pair.Key;if(r==null)continue;
                for(int i=0;i<pair.Value.Length;i++) {
                    if(active.Contains(r)) {r.GetPropertyBlock(block,i);block.SetColor(Emission,new Color(.65f,.9f,1)*pulse);r.SetPropertyBlock(block,i);}
                    else r.SetPropertyBlock(pair.Value[i],i);
                }
            }
        }
        public bool IsGlowing(Renderer r)=>active.Contains(r);
        void OnDestroy(){foreach(var p in sourceMaterials)if(p.Key!=null)p.Key.sharedMaterials=p.Value;foreach(var m in instances)if(m!=null)Destroy(m);}
        void OnDisable(){foreach(var p in originals)if(p.Key!=null)for(int i=0;i<p.Value.Length;i++)p.Key.SetPropertyBlock(p.Value[i],i);active.Clear();}
    }
}
