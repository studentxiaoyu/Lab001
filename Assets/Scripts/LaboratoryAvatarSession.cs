using System.Linq;
using Fusion;
using UnityEngine;

/// <summary>One independent process per Quest Link player. Uses the existing Shared-mode bootstrap.</summary>
public sealed class LaboratoryAvatarSession : MonoBehaviour
{
    public const string PrefabPath = "LaboratoryNetworkAvatar";
    public static LaboratoryAvatarSession Instance { get; private set; }
    public LaboratoryLocalAvatar Source { get; private set; }
    public bool DesktopTest { get; private set; }
    public NetworkRunner Runner { get; private set; }
    private NetworkObject localObject;
    private NetworkObject prefab;
    private float nextAttempt;
    private string status = "Waiting for Fusion connection";

    private void Awake()
    {
        Source = GetComponent<LaboratoryLocalAvatar>();
        Instance = this;
        prefab = Resources.Load<NetworkObject>(PrefabPath);
        if (prefab == null) Debug.LogError("[LabNetwork] Missing Resources/" + PrefabPath + ".prefab");
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F3))
        {
            // Never substitute synthetic poses for a connected real headset.
            bool realHeadset = (UnityEngine.XR.XRSettings.isDeviceActive || OVRManager.isHmdPresent)
                && UnityEngine.XR.OpenXR.OpenXRRuntime.name != "Meta XR Simulator";
            if (!realHeadset)
            {
                DesktopTest = !DesktopTest;
                Debug.Log("[LabNetwork] Simulated network pose: " + DesktopTest);
            }
        }
        if (Runner == null || !Runner.IsRunning)
        {
            localObject = null;
            Runner = NetworkRunner.Instances.FirstOrDefault(r => r.IsRunning && r.GameMode == GameMode.Shared);
            status = "Waiting for Fusion Shared connection";
        }
        if (Runner == null || prefab == null || localObject != null || Time.unscaledTime < nextAttempt) return;
        nextAttempt = Time.unscaledTime + 3;
        try
        {
            if (!Runner.TryGetPlayerObject(Runner.LocalPlayer, out localObject))
            {
                localObject = Runner.Spawn(prefab, inputAuthority: Runner.LocalPlayer);
                Runner.SetPlayerObject(Runner.LocalPlayer, localObject);
            }
            status = "Avatar spawned";
            Debug.Log("[LabNetwork] Avatar ready for " + Runner.LocalPlayer + ", room=" + Runner.SessionInfo.Name);
        }
        catch (System.Exception e)
        {
            status = "Avatar spawn failed (see Console)";
            Debug.LogException(e);
        }
    }

    public bool Sample(out Pose eye, out Pose? left, out Pose? right)
    {
        if (Source.TryGetTrackedPose(out eye, out left, out right)) return true;
        bool realHeadset = (UnityEngine.XR.XRSettings.isDeviceActive || OVRManager.isHmdPresent)
            && UnityEngine.XR.OpenXR.OpenXRRuntime.name != "Meta XR Simulator";
        if (realHeadset) { DesktopTest = false; return false; }
        if (!DesktopTest || Source.CameraRig == null) return false;
        Transform camera = Source.CameraRig.centerEyeAnchor;
        if (camera == null) return false;
        // Deliberately artificial figure ahead of the camera; never labelled as real tracking.
        var facing = Quaternion.Euler(0, 180, 0);
        float side = Runner != null && Runner.LocalPlayer.PlayerId % 2 == 0 ? 0.8f : -0.8f;
        eye = new Pose(camera.position + new Vector3(side, 0, 2.5f), facing);
        float wave = Mathf.Sin(Time.unscaledTime * 2) * 0.2f;
        left = new Pose(eye.position + facing * new Vector3(-0.35f, -0.45f + wave, 0.3f), facing);
        right = new Pose(eye.position + facing * new Vector3(0.35f, -0.45f - wave, 0.3f), facing);
        return true;
    }

    private void OnGUI()
    {
        string connection = Runner != null && Runner.IsRunning
            ? "Room: " + Runner.SessionInfo.Name + " | Region: " + Runner.SessionInfo.Region + " | Players: " + Runner.ActivePlayers.Count()
            : status;
        GUILayout.BeginArea(new Rect(12, Screen.height - 130, 680, 120), GUI.skin.box);
        GUILayout.Label("Fusion | " + connection);
        GUILayout.Label("Pose: " + (DesktopTest ? "SIMULATED (F3)" : "Headset tracking required"));
        GUILayout.Label("Remote avatars: " + LaboratoryNetworkAvatar.VisibleRemoteCount + " | F3: desktop network test");
        if (LaboratoryNetworkAvatar.SimulatedRemoteCount > 0) GUILayout.Label("Remote pose is SIMULATED; not headset validation");
        GUILayout.EndArea();
    }

    private void OnDestroy()
    {
        if (Runner != null && Runner.IsRunning && localObject != null && localObject.HasStateAuthority)
            Runner.Despawn(localObject);
        if (Instance == this) Instance = null;
    }
}
