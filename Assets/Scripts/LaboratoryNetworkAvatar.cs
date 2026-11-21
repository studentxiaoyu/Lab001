using Fusion;
using UnityEngine;

/// <summary>Replicates only the owner's head and grips; remote bodies reuse the existing two-bone IK.</summary>
public sealed class LaboratoryNetworkAvatar : NetworkBehaviour
{
    [Networked] public Vector3 EyePosition { get; set; }
    [Networked] public Quaternion EyeRotation { get; set; }
    [Networked] public Vector3 LeftPosition { get; set; }
    [Networked] public Quaternion LeftRotation { get; set; }
    [Networked] public Vector3 RightPosition { get; set; }
    [Networked] public Quaternion RightRotation { get; set; }
    [Networked] public int Tracking { get; set; }
    [Networked] public int PoseSequence { get; set; }
    [Networked] public NetworkBool Simulated { get; set; }
    private LaboratoryLocalAvatar visual;
    private Pose eye, left, right;
    private bool initialized, visible;
    private int lastSequence;
    private float receivedAt;
    private bool simulatedVisible;
    public static int VisibleRemoteCount { get; private set; }
    public static int SimulatedRemoteCount { get; private set; }

    public override void Spawned()
    {
        receivedAt = Time.unscaledTime;
        Debug.Log("[LabNetwork] Spawned " + Object.StateAuthority + " (local=" + HasStateAuthority + ")");
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;
        var session = LaboratoryAvatarSession.Instance;
        Tracking = 0;
        if (session != null && session.Sample(out var e, out var l, out var r))
        {
            EyePosition = e.position; EyeRotation = e.rotation; Tracking = 1;
            if (l.HasValue) { LeftPosition = l.Value.position; LeftRotation = l.Value.rotation; Tracking |= 2; }
            if (r.HasValue) { RightPosition = r.Value.position; RightRotation = r.Value.rotation; Tracking |= 4; }
        }
        Simulated = session != null && (session.DesktopTest
            || UnityEngine.XR.OpenXR.OpenXRRuntime.name == "Meta XR Simulator");
        PoseSequence++;
    }

    public override void Render()
    {
        if (HasStateAuthority) return; // Local body is already rendered by LaboratoryLocalAvatar.
        var session = LaboratoryAvatarSession.Instance;
        if (session == null) return;
        if (visual == null) visual = session.Source.CreateRemote("Remote Avatar " + Object.StateAuthority);
        if (lastSequence != PoseSequence)
        {
            lastSequence = PoseSequence;
            receivedAt = Time.unscaledTime;
        }
        bool active = (Tracking & 1) != 0 && Time.unscaledTime - receivedAt < 2;
        if (active)
        {
            float blend = initialized && visible ? 1 - Mathf.Exp(-18 * Time.unscaledDeltaTime) : 1;
            eye = Blend(eye, new Pose(EyePosition, EyeRotation), blend);
            left = Blend(left, new Pose(LeftPosition, LeftRotation), blend);
            right = Blend(right, new Pose(RightPosition, RightRotation), blend);
            initialized = true;
        }
        visual.ShowRemote(active, eye, (Tracking & 2) != 0 ? left : (Pose?)null,
            (Tracking & 4) != 0 ? right : (Pose?)null);
        if (visible != active) { VisibleRemoteCount += active ? 1 : -1; visible = active; }
        bool simulated = active && Simulated;
        if (simulatedVisible != simulated) { SimulatedRemoteCount += simulated ? 1 : -1; simulatedVisible = simulated; }
    }

    private static Pose Blend(Pose from, Pose to, float t) =>
        new Pose(Vector3.Lerp(from.position, to.position, t), Quaternion.Slerp(from.rotation, to.rotation, t));

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        Cleanup();
    }

    private void OnDestroy() { Cleanup(); }
    private void Cleanup()
    {
        if (visible) { VisibleRemoteCount--; visible = false; }
        if (simulatedVisible) { SimulatedRemoteCount--; simulatedVisible = false; }
        if (visual != null) Destroy(visual.gameObject);
    }
}
