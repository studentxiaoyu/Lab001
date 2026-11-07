using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;
[InitializeOnLoad] public static class SceneDisplayDiagnosis {
 static SceneDisplayDiagnosis(){ EditorApplication.delayCall+=Run; }
 static void Run(){
 if(SessionState.GetBool("SceneDisplayDiagnosis1",false))return;
 SessionState.SetBool("SceneDisplayDiagnosis1",true);
 Directory.CreateDirectory("Validation");
 var rows=Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Select(r=>r.name+" | "+r.lightmapIndex+" | "+r.lightmapScaleOffset+" | "+(r.GetComponent<MeshFilter>()?.sharedMesh?.name));
 File.WriteAllLines("Validation/renderers.txt",rows);
 File.WriteAllLines("Validation/lightmaps.txt",LightmapSettings.lightmaps.Select(l=>l.lightmapColor?l.lightmapColor.name+" "+l.lightmapColor.width:"null"));
 LightmapSettings.lightmaps=new LightmapData[0];
 SceneView.RepaintAll();
 }
}
