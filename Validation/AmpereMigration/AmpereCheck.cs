using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class AmpereCheck
{
    const string Flag="AmpereCheck";
    static int frames;
    static AmpereCheck(){EditorApplication.update+=Tick;}
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        SessionState.SetBool(Flag,true);
        EditorApplication.isPlaying=true;
    }
    static void Require(bool value,string text){if(!value)throw new Exception(text);Debug.Log("[AmpereCheck] "+text);}
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying||++frames<70)return;
        SessionState.SetBool(Flag,false);
        try{
            var a=Object.FindFirstObjectByType<AmpereExperiment>();
            Require(a && !Object.FindFirstObjectByType<TransformerExperiment>(),"Ampere replaced transformer");
            Require(Object.FindFirstObjectByType<Main>()&&Object.FindFirstObjectByType<IsothermalGasExperiment>(),"Spring and gas present");
            Require(a.rodRenderer.sharedMaterial.shader.name=="Universal Render Pipeline/Lit","Rod uses existing URP material");
            Require(a.GetComponentsInChildren<Transform>(true).All(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0),"No missing station scripts");
            var menu=a.transform.Find("ParameterMenu");
            a.ResetExperiment();
            menu.Find("MassA/Add").GetComponent<Oculus.Interaction.PointableUnityEventWrapper>().WhenSelect.Invoke(default);
            Require(Mathf.Abs(a.current-.6f)<.001f,"Current button event");
            menu.Find("MassB/Sub").GetComponent<Oculus.Interaction.PointableUnityEventWrapper>().WhenSelect.Invoke(default);
            Require(a.magneticStrength==3,"Field button event");
            menu.Find("SpringK/Add").GetComponent<Oculus.Interaction.PointableUnityEventWrapper>().WhenSelect.Invoke(default);
            Require(a.current<0,"Reverse current button event");
            menu.Find("Micro").GetComponent<Oculus.Interaction.PointableUnityEventWrapper>().WhenSelect.Invoke(default);
            Require(a.micro,"Micro button event");a.ToggleMicro();
            menu.Find("Record").GetComponent<Oculus.Interaction.PointableUnityEventWrapper>().WhenSelect.Invoke(default);
            Require(a.records.Count==1,"Record button event");
            Require(AmpereExperiment.SegmentInField(new Vector3(2,0,0),new Vector3(3,0,0),Vector3.one)==Vector3.zero,"Outside field has no active segment");
            Require(Mathf.Abs(AmpereExperiment.SegmentInField(new Vector3(-1,0,0),new Vector3(1,0,0),Vector3.one).magnitude-1)<.001f,"Partial conductor clipping");
            a.ResetExperiment();
            a.Evaluate(Vector3.zero);
            float force=a.Force.magnitude;
            Require(Mathf.Abs(force-.4f*6*.34f)<.001f,"Perpendicular F=BIL");
            a.ReverseCurrent();a.Evaluate(Vector3.zero);
            var negative=a.Force;
            a.ReverseCurrent();a.Evaluate(Vector3.zero);
            Require(Vector3.Dot(negative,a.Force)<0,"Current reversal flips force");
            var old=a.rod.rotation;
            a.rod.rotation=Quaternion.FromToRotation(a.rod.up,a.field.up)*a.rod.rotation;
            a.Evaluate(Vector3.zero);
            Require(a.Force.magnitude<1e-5f,"Parallel conductor has zero force");
            a.rod.rotation=old;
            a.ToggleMode();a.AdjustField(1);
            a.Evaluate(a.field.forward);
            Require(Mathf.Abs(a.Emf)>.01f && Vector3.Dot(a.Force,a.field.forward)<0,"Induction produces opposing force");
            a.Evaluate(Vector3.zero);
            Require(a.Emf==0&&a.Force==Vector3.zero,"Stopped conductor has no induced current");
            a.ToggleMicro();Require(a.rodRenderer.sharedMaterial==a.transparentMaterial,"Micro transparency");
            a.RecordState();Require(a.records.Count==1,"Record stored");
            a.ToggleMicro();a.ToggleMode();a.ResetExperiment();a.Evaluate(Vector3.zero);
            a.SendMessage("Refresh");
            Capture(a);
            File.WriteAllText("Validation/AmpereMigration/result.txt","PASS: scene, vector physics, induction, records and micro material. Quest hardware not tested.");
            EditorApplication.Exit(0);
        }catch(Exception e){Debug.LogException(e);File.WriteAllText("Validation/AmpereMigration/result.txt",e.ToString());EditorApplication.Exit(1);}
    }
    static void Capture(AmpereExperiment a)
    {
        var camera=Object.FindFirstObjectByType<OVRCameraRig>().centerEyeAnchor.GetComponent<Camera>();
        var aim=a.rod.position+Vector3.up*.35f;
        var front=a.readout.transform.forward;
        camera.transform.position=aim-front*1.6f+Vector3.up*.2f;
        camera.transform.LookAt(aim);
        var rt=RenderTexture.GetTemporary(1400,1000,24);
        var tex=new Texture2D(1400,1000,TextureFormat.RGB24,false);
        camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
        tex.ReadPixels(new Rect(0,0,1400,1000),0,0);tex.Apply();
        File.WriteAllBytes("Validation/AmpereMigration/station.png",tex.EncodeToPNG());
        camera.targetTexture=null;RenderTexture.active=null;
        RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(tex);
    }
}

