using UnityEngine;
public sealed class DeconWoundAppearance : MonoBehaviour {
 Renderer surface; MaterialPropertyBlock properties;
 void Awake(){surface=GetComponent<Renderer>();properties=new MaterialPropertyBlock();}
 void LateUpdate(){if(!surface)return;bool washed=EvaluationGameManager.Instance!=null?EvaluationGameManager.Instance.IsWashingComplete:GameManager.Instance!=null&&GameManager.Instance.IsWashingComplete;surface.GetPropertyBlock(properties);properties.SetFloat("_Opacity",washed?.85f:1f);surface.SetPropertyBlock(properties);}
}
