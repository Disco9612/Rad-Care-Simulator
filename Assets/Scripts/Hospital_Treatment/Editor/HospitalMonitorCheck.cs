using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using TMPro;
namespace RadCare.HospitalTreatment.Editor {
 [InitializeOnLoad] public static class HospitalMonitorCheck {
  static HospitalMonitorCheck(){EditorApplication.update+=Poll;}
  static double due;
  static int phase;
  static string original;
  static void Poll(){
   const string request="Temp/HospitalMonitorCheck.request";
   if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
   if(File.Exists(request)) {File.Delete(request);SessionState.SetBool("HospitalMonitorCheck",true);SessionState.SetBool("HospitalMonitorCheckStarted",!EditorApplication.isPlaying);if(!EditorApplication.isPlaying)EditorApplication.isPlaying=true;due=0;phase=0;}
   if(!SessionState.GetBool("HospitalMonitorCheck",false)||!EditorApplication.isPlaying)return;
   if(due==0){due=EditorApplication.timeSinceStartup+8;return;}if(EditorApplication.timeSinceStartup<due)return;

   try {
    var s=UnityEngine.Object.FindAnyObjectByType<HospitalScenario>();var monitor=s.GetComponent<HospitalChartScreen>();
    if(!s.Ready)throw new Exception("Scenario not ready");
    if(phase==0) {
     EditorApplication.isPaused=false;
     original=s.chartText.text;monitor.RefreshScreen();Save(monitor,"before");
     s.chartText.text="SIM-P001 · 가상 성인 · 왼쪽 전완\n현장 이력: 초기 세척 및 임시 드레싱\n알레르기·처방: 모의 인계 확인 완료\n통증: 5/10 → 7/10 → 중단·확인 후 4/10 (모의 응답)\n모의 단위 (임상 기준 아님)\n상처 / 주변 / 대조\n처치 전: 120 / 90 / 30\n처치 후: 35 / 28 / 12\n모의 지시: 추가 세척 보류. 보호 드레싱 적용 후 통증·피부 상태를 관찰하고 변화 시 보고.";
     monitor.RefreshScreen();phase=1;due=EditorApplication.timeSinceStartup+.5;return;
    }
    if(phase==1){Save(monitor,"long");s.chartText.text=original;monitor.RefreshScreen();phase=2;due=EditorApplication.timeSinceStartup+.5;return;}
    Save(monitor,"restored");
    File.WriteAllText("Temp/HospitalMonitorCheck-result.txt","Captured before, long and restored monitor textures. "+DateTime.Now);
   }catch(Exception e){phase=2;File.WriteAllText("Temp/HospitalMonitorCheck-error.txt",e.ToString());}
   if(phase==2){SessionState.SetBool("HospitalMonitorCheck",false);if(SessionState.GetBool("HospitalMonitorCheckStarted",false))EditorApplication.isPlaying=false;}
  }
  static void Save(HospitalChartScreen m,string name){var prev=RenderTexture.active;var tex=new Texture2D(m.Display.width,m.Display.height,TextureFormat.RGB24,false);try{RenderTexture.active=m.Display;tex.ReadPixels(new Rect(0,0,tex.width,tex.height),0,0);tex.Apply();File.WriteAllBytes("Temp/HospitalMonitor-"+name+".png",tex.EncodeToPNG());}finally{RenderTexture.active=prev;UnityEngine.Object.DestroyImmediate(tex);}}
 }
}
