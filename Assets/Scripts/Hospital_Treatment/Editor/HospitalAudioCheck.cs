using System;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace RadCare.HospitalTreatment.Editor
{
    public static class HospitalAudioCheck
    {
        [InitializeOnLoadMethod] static void Install() { EditorApplication.delayCall += Check; }
        [MenuItem("Tools/RadCare/Validate Hospital Effects")]
        public static void Run()
        {
            File.WriteAllText("Temp/HospitalAudioCheck.request", "check");
            Check();
        }
        static void Check()
        {
            const string request="Temp/HospitalAudioCheck.request";
            if (!File.Exists(request)) return;
            File.Delete(request);
            try
            {
                foreach(var key in new[]{"cloth","tape","waste","metal","plastic","click","switch","irrigation"})
                {
                    var clip=Resources.Load<AudioClip>("HospitalTreatment/SFX/"+key);
                    if(clip==null || clip.samples<=0 || clip.channels!=1) throw new Exception("Invalid effect: "+key);
                    clip.LoadAudioData();
                    var samples=new float[clip.samples];
                    if(!clip.GetData(samples,0))throw new Exception("Cannot decode: "+key);
                    float peak=0;foreach(float sample in samples)peak=Mathf.Max(peak,Mathf.Abs(sample));
                    if(peak<=.01f || peak>=.99f)throw new Exception("Silent or clipping effect: "+key);
                }
                File.WriteAllText("Temp/HospitalAudioCheck-result.txt","8 mono clips imported and decoded; no silence or clipping. Hospital runtime scripts compiled.");
            }
            catch(Exception e){File.WriteAllText("Temp/HospitalAudioCheck-error.txt",e.ToString());Debug.LogException(e);}
        }
    }
}
