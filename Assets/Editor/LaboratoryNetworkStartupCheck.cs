using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
[InitializeOnLoad]
public static class LaboratoryNetworkStartupCheck {
 const string Flag="LaboratoryNetworkStartupCheck.Running";
 static double began, readyAt;
 static bool failed;
 static LaboratoryNetworkStartupCheck() {
  EditorApplication.update += Tick;
  Application.logMessageReceived += Log;
 }
 [MenuItem("Tools/Lab/Check Shared Avatar Startup (Play)")]
 public static void Begin() {
  if(EditorApplication.isPlayingOrWillChangePlaymode) {File.WriteAllText("Temp/LaboratoryNetworkStartupCheck.result.txt","SKIPPED: user is already in Play");return;}
  if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!="Assets/Scenes/Main.unity") {File.WriteAllText("Temp/LaboratoryNetworkStartupCheck.result.txt","SKIPPED: Main is not open");return;}
  began=readyAt=0; failed=false;
  SessionState.SetBool(Flag,true);
  SessionState.SetString(Flag+".start",EditorApplication.timeSinceStartup.ToString(System.Globalization.CultureInfo.InvariantCulture));
  EditorApplication.isPlaying=true;
 }
 static void Log(string message,string stack,LogType type) {
  if(!SessionState.GetBool(Flag,false)) return;
  if((type==LogType.Exception || type==LogType.Error || type==LogType.Assert) && (stack.Contains("LaboratoryAvatarSession") || stack.Contains("Simulation.GetNextId") || message.Contains("already owned"))) {
   failed=true; File.WriteAllText("Temp/LaboratoryNetworkStartupCheck.error.txt",message+"\n"+stack);
  }
 }
 static void Tick() {
  if(!SessionState.GetBool(Flag,false)) return;
  if(began==0) began=double.Parse(SessionState.GetString(Flag+".start","0"),System.Globalization.CultureInfo.InvariantCulture);
  if(!EditorApplication.isPlaying) {if(EditorApplication.timeSinceStartup-began>70) Finish("FAIL: Play did not start");return;}
  var s=LaboratoryAvatarSession.Instance;
  if(failed || (s!=null && s.SpawnFailed)) {Finish("FAIL: startup spawn exception");return;}
  if(s!=null && s.AvatarReady) {
   if(readyAt==0) readyAt=EditorApplication.timeSinceStartup;
   if(EditorApplication.timeSinceStartup-readyAt>4) {
    int count=UnityEngine.Object.FindObjectsByType<LaboratoryNetworkAvatar>(FindObjectsSortMode.None).Count(a=>a.Object!=null && a.HasStateAuthority);
    Finish(count==1?"PASS: Shared startup completed; one local avatar, no GetNextId or duplicate ownership errors during startup and 4-second observation. Room="+s.Runner.SessionInfo.Name:"FAIL: local avatar count="+count);
   }
  } else if(EditorApplication.timeSinceStartup-began>70) Finish("INCOMPLETE: connection/startup timeout; cannot validate spawn against cloud");
 }
 static void Finish(string result) {
  SessionState.SetBool(Flag,false);File.WriteAllText("Temp/LaboratoryNetworkStartupCheck.result.txt",result);
  Debug.Log("[LaboratoryNetworkStartupCheck] "+result);if(EditorApplication.isPlaying) EditorApplication.isPlaying=false;
 }
}
