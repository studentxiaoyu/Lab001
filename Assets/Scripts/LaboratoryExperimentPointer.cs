using System;
using Oculus.Interaction;
using Oculus.Interaction.Surfaces;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.UI;
using OpenXRAimControl = UnityEngine.XR.OpenXR.Input.PoseControl;
using XRTrackingState = UnityEngine.XR.InputTrackingState;
using InputUsages = UnityEngine.InputSystem.CommonUsages;

/// <summary>Lets Touch controllers select the lab's existing world-space controls.</summary>
[DefaultExecutionOrder(10020)]
public sealed class LaboratoryExperimentPointer : MonoBehaviour
{
    [Serializable]
    private sealed class PokeButton
    {
        public PokeInteractable surface;
        public PointableUnityEventWrapper pointableEvents;
        public InteractableUnityEventWrapper interactableEvents;

        public void Press(SurfaceHit hit, int pointerId)
        {
            if (pointableEvents != null)
                pointableEvents.WhenSelect.Invoke(new PointerEvent(pointerId, PointerEventType.Select,
                    new Pose(hit.Point, surface.transform.rotation)));
            else
                interactableEvents.WhenSelect.Invoke();
        }
    }

    [SerializeField] private OVRCameraRig cameraRig;
    [SerializeField] private PokeButton[] buttons;
    [SerializeField] private Toggle[] graphTabs;
    [SerializeField] private TransformerExperiment transformer;
    [SerializeField] private AmpereExperiment ampere;
    [SerializeField] private Transform transformerParameterPanel;
    [SerializeField] private Material rayMaterial;

    private const float MaxDistance = 2.5f;
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly Color IdleColor = new Color(0.16f, 0.55f, 0.85f);
    private static readonly Color TargetColor = new Color(0.12f, 0.87f, 0.54f);
    private readonly Pointer left = new Pointer(OVRInput.Controller.LTouch, InputUsages.LeftHand, -101);
    private readonly Pointer right = new Pointer(OVRInput.Controller.RTouch, InputUsages.RightHand, -102);
    private MaterialPropertyBlock rayColors;
    private TextMeshPro powerLabel;
    private Shapes.Rectangle powerBackground;
    private bool powerWasOn;
    private UnityEngine.Object pressedThisFrame;

    private void Awake()
    {
        rayColors = new MaterialPropertyBlock();
        var switchObject = new GameObject("AC power control");
        switchObject.transform.SetParent(transformerParameterPanel, false);
        switchObject.transform.localPosition = new Vector3(0f, 0.292f, 0f);
        powerBackground = switchObject.AddComponent<Shapes.Rectangle>();
        powerBackground.Type = Shapes.Rectangle.RectangleType.RoundedSolid;
        powerBackground.Width = 0.346f;
        powerBackground.Height = 0.082f;
        powerBackground.CornerRadius = 0.018f;
        powerBackground.BlendMode = Shapes.ShapesBlendMode.Opaque;

        powerLabel = new GameObject("AC power status").AddComponent<TextMeshPro>();
        powerLabel.transform.SetParent(switchObject.transform, false);
        powerLabel.transform.localPosition = Vector3.back * 0.002f;
        powerLabel.rectTransform.sizeDelta = new Vector2(powerBackground.Width, powerBackground.Height);
        powerLabel.font = ampere ? ampere.readout.font : transformer.worldReadout.font;
        powerLabel.fontSize = 0.32f;
        powerLabel.isOrthographic = false;
        powerLabel.fontStyle = FontStyles.Bold;
        powerLabel.alignment = TextAlignmentOptions.Center;
        powerLabel.textWrappingMode = TextWrappingModes.NoWrap;
        powerLabel.color = Color.white;
        RefreshPowerLabel();

        left.beam = CreateBeam("Left experiment pointer");
        right.beam = CreateBeam("Right experiment pointer");
    }

    private LineRenderer CreateBeam(string name)
    {
        var beam = new GameObject(name).AddComponent<LineRenderer>();
        beam.transform.SetParent(transform, false);
        beam.sharedMaterial = rayMaterial;
        beam.positionCount = 2;
        beam.useWorldSpace = true;
        beam.startWidth = 0.0035f;
        beam.endWidth = 0.002f;
        beam.shadowCastingMode = ShadowCastingMode.Off;
        beam.receiveShadows = false;
        beam.enabled = false;
        return beam;
    }

    private void LateUpdate()
    {
        if ((ampere ? ampere.induction : transformer.powered) != powerWasOn)
            RefreshPowerLabel();

        pressedThisFrame = null;
        if (!OVRManager.OVRManagerinitialized || OVRManager.instance == null
            || !OVRManager.hasInputFocus || !OVRManager.instance.isUserPresent || cameraRig.trackingSpace == null)
        {
            left.Hide();
            right.Hide();
            return;
        }

        UpdatePointer(left);
        UpdatePointer(right);
    }

    private void OnDisable()
    {
        left.Hide();
        right.Hide();
    }

    private void OnDestroy()
    {
        if (powerBackground != null)
            Destroy(powerBackground.gameObject);
    }

    private void UpdatePointer(Pointer pointer)
    {
        if (!TryGetRay(pointer, out Ray ray))
        {
            pointer.Hide();
            return;
        }

        ProcessRay(pointer, ray, OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, pointer.controller));
    }

    private void ProcessRay(Pointer pointer, Ray ray, bool down)
    {
        PokeButton button = null;
        Toggle tab = null;
        SurfaceHit buttonHit = default;
        float distance = MaxDistance;
        foreach (PokeButton candidate in buttons)
        {
            if (!candidate.surface.isActiveAndEnabled || candidate.surface.SurfacePatch == null
                || !candidate.surface.SurfacePatch.Raycast(ray, out SurfaceHit hit, distance))
                continue;
            button = candidate;
            buttonHit = hit;
            distance = hit.Distance;
        }

        foreach (Toggle candidate in graphTabs)
        {
            if (!candidate.isActiveAndEnabled || !candidate.IsInteractable()
                || !HitRect(ray, (RectTransform)candidate.transform, distance, out float hitDistance))
                continue;
            tab = candidate;
            distance = hitDistance;
        }

        bool powerHit = (ampere ? ampere.isActiveAndEnabled : transformer.isActiveAndEnabled) && powerLabel.isActiveAndEnabled
            && HitRect(ray, powerLabel.rectTransform, distance, out distance);
        UnityEngine.Object target = powerHit ? powerLabel : tab != null ? (UnityEngine.Object)tab : button?.surface;
        // A short beam when idle keeps the lab view clear. A hit extends it to the control.
        pointer.beam.SetPosition(0, ray.origin);
        pointer.beam.SetPosition(1, ray.GetPoint(target != null ? distance : 0.28f));
        rayColors.SetColor(BaseColor, target != null ? TargetColor : IdleColor);
        pointer.beam.SetPropertyBlock(rayColors);
        pointer.beam.enabled = true;

        // A held trigger cannot activate a control when focus or tracking returns.
        if (down && pointer.released && target != null && target != pressedThisFrame)
        {
            pressedThisFrame = target;
            if (powerHit)
            {
                if (ampere) ampere.ToggleMode();
                else transformer.TogglePower();
                RefreshPowerLabel();
            }
            else if (tab != null)
                tab.isOn = true;
            else
                button.Press(buttonHit, pointer.id);
        }
        pointer.released = !down;
    }

    private bool TryGetRay(Pointer pointer, out Ray ray)
    {
        ray = default;
        Transform space = cameraRig.trackingSpace;
        // OpenXR's aim pose points along the trigger. Older runtimes may only expose the grip pose.
        var device = InputSystem.GetDevice<XRController>(pointer.usage);
        // OpenXR and Input System provide different controls with the same short type name.
        var aim = device?.TryGetChildControl("pointer");
        if (aim is OpenXRAimControl openXRAim)
        {
            var pose = openXRAim.ReadValue();
            if (pose.isTracked && HasPositionAndRotation(pose.trackingState))
            {
                ray = CreateRay(space, pose.position, pose.rotation);
                return true;
            }
        }
        else if (aim is PoseControl inputSystemAim)
        {
            var pose = inputSystemAim.ReadValue();
            if (pose.isTracked && HasPositionAndRotation(pose.trackingState))
            {
                ray = CreateRay(space, pose.position, pose.rotation);
                return true;
            }
        }

        if (!OVRInput.GetControllerPositionTracked(pointer.controller)
            || !OVRInput.GetControllerOrientationTracked(pointer.controller))
            return false;

        ray = CreateRay(space, OVRInput.GetLocalControllerPosition(pointer.controller),
            OVRInput.GetLocalControllerRotation(pointer.controller));
        return true;
    }

    private static bool HasPositionAndRotation(XRTrackingState state)
    {
        const XRTrackingState required = XRTrackingState.Position | XRTrackingState.Rotation;
        return (state & required) == required;
    }

    private static Ray CreateRay(Transform space, Vector3 position, Quaternion rotation)
    {
        return new Ray(space.TransformPoint(position), space.rotation * rotation * Vector3.forward);
    }

    private static bool HitRect(Ray ray, RectTransform rect, float maxDistance, out float distance)
    {
        var plane = new Plane(-rect.forward, rect.position);
        distance = maxDistance;
        if (Vector3.Dot(plane.normal, ray.direction) >= 0f
            || !plane.Raycast(ray, out float hitDistance) || hitDistance >= maxDistance
            || !rect.rect.Contains((Vector2)rect.InverseTransformPoint(ray.GetPoint(hitDistance))))
            return false;
        distance = hitDistance;
        return true;
    }

    private void RefreshPowerLabel()
    {
        powerWasOn = ampere ? ampere.induction : transformer.powered;
        powerLabel.text = ampere ? (powerWasOn ? "Induction" : "Constant current") : (powerWasOn ? "AC POWER: ON" : "AC POWER: OFF");
        powerBackground.Color = ampere ? new Color(0, 0.2901961f, 0.7254902f) : (powerWasOn ? new Color(0.06f, 0.4f, 0.28f) : new Color(0.21f, 0.26f, 0.31f));
    }

    private sealed class Pointer
    {
        public readonly OVRInput.Controller controller;
        public readonly UnityEngine.InputSystem.Utilities.InternedString usage;
        public readonly int id;
        public LineRenderer beam;
        public bool released;

        public Pointer(OVRInput.Controller controller, UnityEngine.InputSystem.Utilities.InternedString usage, int id)
        {
            this.controller = controller;
            this.usage = usage;
            this.id = id;
        }

        public void Hide()
        {
            released = false;
            if (beam != null) beam.enabled = false;
        }
    }
}
