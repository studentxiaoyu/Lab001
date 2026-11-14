using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

/// <summary>Presentation only: keep debug overlays opt-in and reuse the existing OVR camera.</summary>
[DefaultExecutionOrder(10000)]
public sealed class LaboratoryPlayerView : MonoBehaviour
{
    [SerializeField] private OVRCameraRig cameraRig;
    [SerializeField] private Canvas[] desktopDebugCanvases;
    [Header("Quest locomotion")]
    [SerializeField, Min(0)] private float walkSpeed = 1.5f;
    [SerializeField, Min(0)] private float turnSpeedDegreesPerSecond = 60f;
    [SerializeField, Range(0.1f, 0.5f)] private float stickDeadzone = 0.2f;
    [SerializeField, Min(0.6f)] private float standingEyeHeight = 1.65f;
    private CharacterController body;
    private float verticalSpeed;
    private Quaternion initialRotation;
    private float yaw, pitch;
    private bool wasXR;

    public bool DebugVisible { get; private set; }
    public bool XRActive => XRSettings.isDeviceActive || OVRManager.isHmdPresent;

    private void Awake()
    {
        initialRotation = cameraRig.transform.rotation;
        yaw = cameraRig.transform.eulerAngles.y;
        SetDebugVisible(false);
    }

    public void SetDebugVisible(bool visible)
    {
        DebugVisible = visible && !XRActive;
        // Keep experiment scripts, slider references and world-space canvases alive.
        foreach (var canvas in desktopDebugCanvases)
        {
            if (canvas == null || canvas.renderMode == RenderMode.WorldSpace) continue;
            canvas.enabled = DebugVisible;
            var raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster != null) raycaster.enabled = DebugVisible;
        }
    }

    private void Update()
    {
        bool xr = XRActive;
        if (xr)
        {
            if (!wasXR) cameraRig.transform.rotation = initialRotation;
            if (DebugVisible) SetDebugVisible(false);
            UpdateQuestLocomotion();
        }
        else
        {
            if (body != null) body.enabled = false;
            verticalSpeed = 0;
            if (Input.GetKeyDown(KeyCode.F1)) SetDebugVisible(!DebugVisible);
            if (Input.GetMouseButton(1))
            {
                yaw += Input.GetAxis("Mouse X") * 2f;
                pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * 2f, -65f, 65f);
            }
            float turn = (Input.GetKey(KeyCode.RightArrow) ? 1f : 0f)
                       - (Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
            yaw += turn * 65f * Time.unscaledDeltaTime;
        }
        wasXR = xr;
    }

    private void UpdateQuestLocomotion()
    {
        // Move the rig root, leaving tracked eyes and hands under SDK control.
        if (!OVRManager.hasInputFocus || OVRManager.instance == null || !OVRManager.instance.isUserPresent) return;
        Transform rig = cameraRig.transform;
        Transform head = cameraRig.centerEyeAnchor;
        if (head == null) return;

        if (body == null)
        {
            body = cameraRig.GetComponent<CharacterController>();
            if (body == null) body = cameraRig.gameObject.AddComponent<CharacterController>();
            body.enabled = false;
            body.radius = 0.22f;
            body.skinWidth = 0.02f;
            body.stepOffset = 0.2f;
            body.slopeLimit = 45f;
            body.minMoveDistance = 0;
            foreach (Collider child in cameraRig.GetComponentsInChildren<Collider>(true))
                if (child != body) Physics.IgnoreCollision(body, child);
        }

        Vector3 localHead = rig.InverseTransformPoint(head.position);
        float height = Mathf.Clamp(standingEyeHeight + localHead.y, 0.6f, 2.3f);
        body.height = height;
        body.center = new Vector3(localHead.x, localHead.y - height * 0.5f, localHead.z);
        body.enabled = true;

        Vector2 move = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch);
        float magnitude = move.magnitude;
        move = magnitude > stickDeadzone
            ? move.normalized * Mathf.InverseLerp(stickDeadzone, 1f, magnitude)
            : Vector2.zero;
        Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.01f)
            forward = Vector3.ProjectOnPlane(head.up, Vector3.up).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Vector3 velocity = (forward * move.y + right * move.x) * walkSpeed;
        verticalSpeed = body.isGrounded && verticalSpeed < 0 ? -1f : verticalSpeed - 9.81f * Time.deltaTime;
        velocity.y = verticalSpeed;
        CollisionFlags collisions = body.Move(velocity * Time.deltaTime);
        if ((collisions & CollisionFlags.Below) != 0) verticalSpeed = -1f;

        float turn = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch).x;
        float turnMagnitude = Mathf.InverseLerp(stickDeadzone, 1f, Mathf.Abs(turn));
        if (turnMagnitude > 0f)
        {
            // Pivot at the player's head so turning does not orbit the room.
            float turnAngle = Mathf.Sign(turn) * turnMagnitude * turnSpeedDegreesPerSecond * Time.deltaTime;
            rig.RotateAround(head.position, Vector3.up, turnAngle);
        }
    }

    private void LateUpdate()
    {
        // Do not write tracked eye/hand anchors or replace OVR tracking in either mode.
        if (!XRActive) cameraRig.transform.rotation = Quaternion.Euler(pitch, yaw, 0);
    }

    private void OnGUI()
    {
        if (XRActive) return;
        if (GUI.Button(new Rect(12, 12, 190, 28), DebugVisible ? "Hide desktop debug [F1]" : "Desktop debug [F1]"))
            SetDebugVisible(!DebugVisible);
        if (!DebugVisible)
            GUI.Label(new Rect(12, 44, 400, 24), "Look: hold right mouse / Left & Right arrows");
    }
}
