using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR;

// Temporary read-only probe. Does not change the scene, camera or tracking.
[InitializeOnLoad]
public static class QuestTrackingDiagnostics
{
    private static double next;
    private static int samples;
    private static readonly string Output = Path.GetFullPath("Validation/QuestTrackingDiagnostics.log");

    static QuestTrackingDiagnostics() { EditorApplication.update += Sample; }

    private static string Device(XRNode node)
    {
        var d = InputDevices.GetDeviceAtXRNode(node);
        bool p = d.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 pos);
        bool r = d.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rot);
        d.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked);
        return $"{node}: valid={d.isValid} tracked={tracked} posValid={p} rotValid={r} pos={pos:F3} rot={rot.eulerAngles:F1}";
    }

    private static void Sample()
    {
        if (samples >= 240 || EditorApplication.timeSinceStartup < next || EditorApplication.isCompiling) return;
        next = EditorApplication.timeSinceStartup + 1;
        samples++;
        try
        {
            var rig = UnityEngine.Object.FindFirstObjectByType<OVRCameraRig>(FindObjectsInactive.Include);
            var displays = new System.Collections.Generic.List<XRDisplaySubsystem>();
            SubsystemManager.GetSubsystems(displays);
            string text = $"\n{DateTime.Now:HH:mm:ss} play={EditorApplication.isPlaying} pause={EditorApplication.isPaused} frame={Time.frameCount} xr={XRSettings.isDeviceActive} display={displays.Any(d => d.running)} ovrInit={OVRManager.OVRManagerinitialized} hmd={OVRManager.isHmdPresent}\n";
            if (EditorApplication.isPlaying)
            {
                text += Device(XRNode.Head) + "\n" + Device(XRNode.CenterEye) + "\n" + Device(XRNode.LeftHand) + "\n" + Device(XRNode.RightHand) + "\n";
                if (OVRManager.instance != null)
                    text += $"focus={OVRManager.hasInputFocus} worn={OVRManager.instance.isUserPresent} positionTracking={OVRManager.instance.usePositionTracking} rotationTracking={OVRManager.instance.useRotationTracking}\n";
                if (rig != null && rig.centerEyeAnchor != null)
                    text += $"rigEnabled={rig.isActiveAndEnabled} fixedTracking={rig.useFixedUpdateForTracking} anchorPos={rig.centerEyeAnchor.localPosition:F3} anchorRot={rig.centerEyeAnchor.localEulerAngles:F1} worldPos={rig.centerEyeAnchor.position:F3} worldRot={rig.centerEyeAnchor.eulerAngles:F1}\n";
                foreach (var c in Camera.allCameras)
                    text += $"camera={c.name} stereo={c.stereoEnabled} eyes={c.stereoTargetEye} texture={(c.targetTexture != null)} display={c.targetDisplay} rot={c.transform.eulerAngles:F1}\n";
            }
            Directory.CreateDirectory(Path.GetDirectoryName(Output));
            File.AppendAllText(Output, text);
        }
        catch (Exception e) { File.AppendAllText(Output, "Probe error: " + e.Message + "\n"); }
    }
}
