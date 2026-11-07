using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
public static class SceneDisplayInvestigation {
 public static void Run() {
  EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
  var w=new StreamWriter("Validation/renderers.txt");
  foreach(var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)) { if(r.bounds.size.magnitude>3) w.WriteLine(r.name+" | "+r.bounds+" | lightmap="+r.lightmapIndex+" | "+string.Join(",",System.Array.ConvertAll(r.sharedMaterials,m=>m?m.name+":"+(m.shader?m.shader.name:"NULL"):"NULL"))); }
  foreach(var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) w.WriteLine("LIGHT "+l.name+" "+l.type+" "+l.lightmapBakeType+" "+l.transform.position);
  w.Close();
  var go=new GameObject("diagnostic camera"); var cam=go.AddComponent<Camera>(); cam.transform.position=new Vector3(0,1.5f,0); cam.transform.rotation=Quaternion.Euler(-20,0,0); cam.clearFlags=CameraClearFlags.Skybox; cam.farClipPlane=200;
  var rt=new RenderTexture(1200,700,24);cam.targetTexture=rt;
  for(int i=0;i<2;i++){if(i==1) LightmapSettings.lightmaps=new LightmapData[0];cam.Render();RenderTexture.active=rt;var t=new Texture2D(1200,700,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,1200,700),0,0);t.Apply();File.WriteAllBytes("Validation/display-"+i+".png",t.EncodeToPNG());Object.DestroyImmediate(t);}
  RenderTexture.active=null; Object.DestroyImmediate(go); EditorApplication.Exit(0);
 }
}
