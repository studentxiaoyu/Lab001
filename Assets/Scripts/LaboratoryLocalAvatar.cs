using UnityEngine;
using UnityEngine.XR;

/// <summary>Drives a visual-only Humanoid from the lab's local XR rig.</summary>
[DefaultExecutionOrder(10010)]
public sealed class LaboratoryLocalAvatar : MonoBehaviour
{
    [SerializeField] private OVRCameraRig cameraRig;
    [SerializeField] private GameObject humanoidModel;
    [SerializeField] private bool showInQuest = true;
    [SerializeField, Min(0.6f)] private float standingEyeHeight = 1.65f;
    [SerializeField] private Vector3 headToEye = new Vector3(0f, 0.08f, 0.13f);
    [SerializeField] private Vector3 leftGripRotationOffset;
    [SerializeField] private Vector3 rightGripRotationOffset;

    private Transform body;
    private Transform head;
    private Quaternion headRestRotation;
    private Arm leftArm;
    private Arm rightArm;
    private bool remoteDriven;

    public OVRCameraRig CameraRig => cameraRig;

    // Remote copies contain only a visual driver, never another camera, input rig or network bridge.
    public LaboratoryLocalAvatar CreateRemote(string playerName)
    {
        var root = new GameObject(playerName);
        root.SetActive(false);
        var copy = root.AddComponent<LaboratoryLocalAvatar>();
        copy.remoteDriven = true;
        copy.cameraRig = cameraRig;
        copy.humanoidModel = humanoidModel;
        copy.standingEyeHeight = standingEyeHeight;
        copy.headToEye = headToEye;
        copy.leftGripRotationOffset = leftGripRotationOffset;
        copy.rightGripRotationOffset = rightGripRotationOffset;
        root.SetActive(true);
        return copy;
    }

    public void ShowRemote(bool visible, Pose eye, Pose? left, Pose? right)
    {
        if (!remoteDriven || body == null) return;
        if (visible) ApplyPose(eye, left, right);
        else body.gameObject.SetActive(false);
    }

    public bool TryGetTrackedPose(out Pose eye, out Pose? left, out Pose? right)
    {
        eye = default; left = right = null;
        if (remoteDriven || cameraRig == null || cameraRig.centerEyeAnchor == null
            || cameraRig.trackingSpace == null || !OVRManager.OVRManagerinitialized
            || OVRManager.instance == null || !OVRManager.hasInputFocus || !OVRManager.instance.isUserPresent
            || !OVRPlugin.GetNodePositionTracked(OVRPlugin.Node.EyeCenter)
            || !OVRPlugin.GetNodeOrientationTracked(OVRPlugin.Node.EyeCenter)) return false;
        eye = new Pose(cameraRig.centerEyeAnchor.position, cameraRig.centerEyeAnchor.rotation);
        left = GetTouchPose(OVRInput.Controller.LTouch);
        right = GetTouchPose(OVRInput.Controller.RTouch);
        return true;
    }

#if UNITY_EDITOR
    private bool simulatorActive;
    private bool desktopPreview;
    private Pose previewOrigin;
#endif

    private void Awake()
    {
        if (cameraRig == null || humanoidModel == null)
        {
            FailInitialization("Assign the scene's OVRCameraRig and a Humanoid model.");
            return;
        }

        body = Instantiate(humanoidModel, transform).transform;
        body.gameObject.SetActive(false);
        Animator animator = body.GetComponent<Animator>();
        if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.isHuman)
        {
            FailInitialization("The model needs a valid Humanoid Avatar.");
            return;
        }

        head = animator.GetBoneTransform(HumanBodyBones.Head);
        Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
        leftArm = new Arm(animator, true);
        rightArm = new Arm(animator, false);
        if (head == null || leftFoot == null || rightFoot == null || !leftArm.IsValid || !rightArm.IsValid)
        {
            FailInitialization("The model needs mapped head, foot, and arm bones.");
            return;
        }

        animator.enabled = false;
        headRestRotation = Quaternion.Inverse(body.rotation) * head.rotation;
        float footHeight = Mathf.Min(body.InverseTransformPoint(leftFoot.position).y,
            body.InverseTransformPoint(rightFoot.position).y);
        float modelEyeHeight = body.InverseTransformPoint(head.position).y + headToEye.y - footHeight;
        if (modelEyeHeight <= 0.1f)
        {
            FailInitialization("The model has an invalid standing eye height.");
            return;
        }
        body.localScale *= standingEyeHeight / modelEyeHeight;
        if (!remoteDriven && GetComponent<LaboratoryAvatarSession>() == null)
            gameObject.AddComponent<LaboratoryAvatarSession>();
    }

    private void FailInitialization(string message)
    {
        Debug.LogError(message, this);
        if (body != null) Destroy(body.gameObject);
        enabled = false;
    }

    private void OnDisable()
    {
        if (body != null) body.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (remoteDriven) return;
        Transform eye = cameraRig.centerEyeAnchor;
        bool xrActive = XRSettings.isDeviceActive || OVRManager.isHmdPresent;
#if UNITY_EDITOR
        if (eye != null && Input.GetKeyDown(KeyCode.F2))
        {
            simulatorActive = xrActive && UnityEngine.XR.OpenXR.OpenXRRuntime.name == "Meta XR Simulator";
            if (!xrActive || simulatorActive)
            {
                desktopPreview = !desktopPreview;
                if (desktopPreview)
                    previewOrigin = new Pose(eye.position, Yaw(eye.forward, cameraRig.transform.rotation));
            }
        }
        if (desktopPreview && (!xrActive || simulatorActive))
        {
            ApplyDesktopPreview(Time.unscaledTime);
            return;
        }
#endif

        bool headTracked = showInQuest && xrActive && eye != null && OVRManager.OVRManagerinitialized
            && OVRManager.instance != null && OVRManager.hasInputFocus && OVRManager.instance.isUserPresent
            && OVRPlugin.GetNodePositionTracked(OVRPlugin.Node.EyeCenter)
            && OVRPlugin.GetNodeOrientationTracked(OVRPlugin.Node.EyeCenter);
        if (!headTracked || cameraRig.trackingSpace == null)
        {
            body.gameObject.SetActive(false);
            return;
        }

        ApplyPose(new Pose(eye.position, eye.rotation),
            GetTouchPose(OVRInput.Controller.LTouch), GetTouchPose(OVRInput.Controller.RTouch));
    }

    private Pose? GetTouchPose(OVRInput.Controller controller)
    {
        if (!OVRInput.GetControllerPositionTracked(controller)
            || !OVRInput.GetControllerOrientationTracked(controller))
            return null;

        // Use the existing tracking space so snap turns and joystick motion are applied once.
        Transform space = cameraRig.trackingSpace;
        return new Pose(space.TransformPoint(OVRInput.GetLocalControllerPosition(controller)),
            space.rotation * OVRInput.GetLocalControllerRotation(controller));
    }

    private void ApplyPose(Pose eye, Pose? left, Pose? right)
    {
        body.rotation = Yaw(eye.rotation * Vector3.forward, body.rotation);
        head.rotation = eye.rotation * headRestRotation;
        body.position += eye.position - (head.position + eye.rotation * (headToEye * body.lossyScale.y));

        leftArm.Apply(body, left, leftGripRotationOffset);
        rightArm.Apply(body, right, rightGripRotationOffset);
        body.gameObject.SetActive(true);
    }

    private static Quaternion Yaw(Vector3 forward, Quaternion fallback)
    {
        Vector3 horizontal = Vector3.ProjectOnPlane(forward, Vector3.up);
        return horizontal.sqrMagnitude > 0.01f ? Quaternion.LookRotation(horizontal) : fallback;
    }

#if UNITY_EDITOR
    private void ApplyDesktopPreview(float time)
    {
        Quaternion facing = previewOrigin.rotation * Quaternion.Euler(0f, 180f, 0f);
        Vector3 eye = previewOrigin.position + previewOrigin.rotation * new Vector3(0f, 0f, 2.8f);
        float lift = Mathf.Sin(time * 1.8f) * 0.18f;
        var left = new Pose(eye + facing * new Vector3(-0.34f, -0.5f + lift, 0.33f), facing);
        var right = new Pose(eye + facing * new Vector3(0.34f, -0.5f - lift, 0.33f), facing);
        ApplyPose(new Pose(eye, facing * Quaternion.Euler(0f, 0f, lift * 40f)), left, right);
    }
#endif

    private sealed class Arm
    {
        private readonly Transform upper;
        private readonly Transform lower;
        private readonly Transform hand;
        private readonly float side;
        private readonly Quaternion upperRest;
        private readonly Quaternion lowerRest;
        private readonly Quaternion handRest;
        private readonly Quaternion gripToHand;

        public bool IsValid => upper != null && lower != null && hand != null;

        public Arm(Animator animator, bool isLeft)
        {
            side = isLeft ? -1f : 1f;
            upper = animator.GetBoneTransform(isLeft ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
            lower = animator.GetBoneTransform(isLeft ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
            hand = animator.GetBoneTransform(isLeft ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            if (!IsValid) return;

            upperRest = upper.localRotation;
            lowerRest = lower.localRotation;
            handRest = hand.localRotation;
            // Relate the model's wrist axes to a forward-pointing grip via its rest-pose forearm.
            Quaternion restGrip = Quaternion.LookRotation(hand.position - lower.position, animator.transform.up);
            gripToHand = Quaternion.Inverse(restGrip) * hand.rotation;
        }

        public void Apply(Transform body, Pose? trackedGrip, Vector3 gripRotationOffset)
        {
            upper.localRotation = upperRest;
            lower.localRotation = lowerRest;
            hand.localRotation = handRest;

            float upperLength = Vector3.Distance(upper.position, lower.position);
            float lowerLength = Vector3.Distance(lower.position, hand.position);
            if (upperLength < 0.01f || lowerLength < 0.01f) return;

            Vector3 relaxed = body.rotation * new Vector3(side * 0.12f, -0.9f, 0.12f);
            Vector3 target = trackedGrip?.position ?? upper.position + relaxed * (upperLength + lowerLength);
            Vector3 delta = target - upper.position;
            Vector3 direction = delta.sqrMagnitude > 0.000001f ? delta.normalized : -body.up;
            float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(upperLength - lowerLength) + 0.001f,
                upperLength + lowerLength - 0.001f);
            target = upper.position + direction * distance;

            // Adapted from VirtualEmbodiment's PythonAvatarDriver.SolveElbow / SolveArm.
            float along = (upperLength * upperLength - lowerLength * lowerLength + distance * distance)
                / (2f * distance);
            float height = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
            Vector3 hint = body.rotation * new Vector3(side * 0.75f, -0.1f, -0.25f);
            Vector3 bend = Vector3.ProjectOnPlane(hint, direction);
            if (bend.sqrMagnitude < 0.000001f) bend = Vector3.ProjectOnPlane(body.up, direction);
            Vector3 elbow = upper.position + direction * along + bend.normalized * height;

            upper.rotation = Quaternion.FromToRotation(lower.position - upper.position, elbow - upper.position)
                * upper.rotation;
            lower.rotation = Quaternion.FromToRotation(hand.position - lower.position, target - lower.position)
                * lower.rotation;
            if (trackedGrip.HasValue)
                hand.rotation = trackedGrip.Value.rotation * Quaternion.Euler(gripRotationOffset) * gripToHand;
        }
    }
}
