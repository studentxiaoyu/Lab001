using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>Quest-only conductor experiment. All physical quantities use metres, seconds, amperes and teslas.</summary>
public sealed class AmpereExperiment : MonoBehaviour
{
    public Transform rod, field;
    public Renderer rodRenderer;
    public Material solidMaterial, transparentMaterial;
    public TMP_Text readout, modeCaption;
    public TMP_Text[] parameters;
    public TMP_Text fieldIncrease, fieldDecrease, northLabel, southLabel;
    public Renderer northPole, southPole;
    public Material northMaterial, southMaterial;
    public Transform[] electrons;
    public Arrow fieldArrow, currentArrow, forceArrow;
    public GraphDrawer graph;
    public float current = .4f, magneticStrength = 6;
    public bool induction, micro;
    public float resistance = 1;
    public float rodLength = .34f;
    public Vector3 fieldSize = new Vector3(.48f, .30f, .34f);
    public Vector3 Velocity { get; private set; }
    public Vector3 Force { get; private set; }
    public float Emf { get; private set; }
    public float EffectiveCurrent { get; private set; }
    public float ActiveLength { get; private set; }
    [Serializable] public struct Record
    {
        public bool induction;
        public float field, current, emf, force, speed, length;
    }
    public readonly List<Record> records = new List<Record>();
    private OVRCameraRig rig;
    private Vector3 homePosition, previousPosition, gripOffset;
    private Quaternion homeRotation, gripRotation;
    private OVRInput.Controller holder;
    private bool leftReady, rightReady;
    private float electronPhase;
    private string note = "Grip near the rod to move it. Release to stop.";

    private void Awake()
    {
        rig = FindFirstObjectByType<OVRCameraRig>();
        homePosition = rod.localPosition;
        homeRotation = rod.localRotation;
        previousPosition = rod.position;
    }

    private void Start()
    {
        graph.Reset();
        graph.Show();
        graph.HideLineLabel();
        graph.HideHighlightPoints();
        Refresh();
    }

    private void Update()
    {
        UpdateGrab();
        Vector3 displacement = rod.position - previousPosition;
        previousPosition = rod.position;
        Velocity = holder == OVRInput.Controller.None || Time.deltaTime <= 0
            ? Vector3.zero : Vector3.Lerp(Velocity, displacement / Time.deltaTime, 1 - Mathf.Exp(-20 * Time.deltaTime));
        Evaluate(Velocity);
        electronPhase = Mathf.Repeat(electronPhase - EffectiveCurrent * Time.deltaTime, 1);
        for (int i = 0; i < electrons.Length; i++)
        {
            electrons[i].gameObject.SetActive(micro);
            electrons[i].localPosition = Vector3.up * (Mathf.Repeat(electronPhase + (float)i / electrons.Length, 1) - .5f) * 1.8f;
        }
        Refresh();
    }

    // Clip the conductor segment against the field volume before applying vector laws.
    public static Vector3 SegmentInField(Vector3 a, Vector3 b, Vector3 size)
    {
        Vector3 delta = b - a;
        float enter = 0, exit = 1;
        for (int axis = 0; axis < 3; axis++)
        {
            float half = size[axis] * .5f;
            if (Mathf.Abs(delta[axis]) < 1e-7f)
            {
                if (Mathf.Abs(a[axis]) > half) return Vector3.zero;
                continue;
            }
            float near = (-half - a[axis]) / delta[axis];
            float far = (half - a[axis]) / delta[axis];
            if (near > far) (near, far) = (far, near);
            enter = Mathf.Max(enter, near);
            exit = Mathf.Min(exit, far);
            if (enter >= exit) return Vector3.zero;
        }
        return delta * (exit - enter);
    }

    public void Evaluate(Vector3 velocity)
    {
        Vector3 half = rod.up * rodLength * .5f;
        Vector3 segment = SegmentInField(field.InverseTransformPoint(rod.position - half),
            field.InverseTransformPoint(rod.position + half), fieldSize);
        Vector3 length = field.TransformVector(segment);
        Vector3 magnetic = field.up * magneticStrength;
        ActiveLength = length.magnitude;
        Emf = induction ? Vector3.Dot(Vector3.Cross(velocity, magnetic), length) : 0;
        EffectiveCurrent = induction ? Emf / resistance : current;
        Force = EffectiveCurrent * Vector3.Cross(length, magnetic);
        DrawArrow(fieldArrow, magnetic, 9);
        DrawArrow(currentArrow, rod.up * EffectiveCurrent, .6f);
        DrawArrow(forceArrow, Force, 2);
    }

    private void DrawArrow(Arrow arrow, Vector3 value, float range)
    {
        arrow.transform.position = rod.position;
        arrow.gameObject.SetActive(value.sqrMagnitude > 1e-10f);
        if (value.sqrMagnitude <= 1e-10f) return;
        arrow.transform.rotation = Quaternion.FromToRotation(Vector3.right, value.normalized);
        arrow.SetValue(value.magnitude, Mathf.Max(range, value.magnitude));
    }

    private bool HandPose(OVRInput.Controller controller, out Vector3 position, out Quaternion rotation)
    {
        position = default;
        rotation = Quaternion.identity;
        if (!rig || !OVRManager.OVRManagerinitialized || !OVRManager.hasInputFocus
            || !OVRManager.instance || !OVRManager.instance.isUserPresent
            || !OVRInput.GetControllerPositionTracked(controller) || !OVRInput.GetControllerOrientationTracked(controller))
            return false;
        position = rig.trackingSpace.TransformPoint(OVRInput.GetLocalControllerPosition(controller));
        rotation = rig.trackingSpace.rotation * OVRInput.GetLocalControllerRotation(controller);
        return true;
    }

    private void UpdateGrab()
    {
        if (holder != OVRInput.Controller.None)
        {
            if (!HandPose(holder, out Vector3 position, out Quaternion rotation)
                || !OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, holder))
            {
                holder = OVRInput.Controller.None;
                Velocity = Vector3.zero;
                leftReady = rightReady = false;
                return;
            }
            rod.SetPositionAndRotation(position + rotation * gripOffset, rotation * gripRotation);
            return;
        }
        TryGrab(OVRInput.Controller.LTouch, ref leftReady);
        if (holder == OVRInput.Controller.None) TryGrab(OVRInput.Controller.RTouch, ref rightReady);
    }

    private void TryGrab(OVRInput.Controller controller, ref bool ready)
    {
        if (!HandPose(controller, out Vector3 position, out Quaternion rotation)) { ready = false; return; }
        bool pressed = OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, controller);
        if (!pressed) { ready = true; return; }
        if (!ready) return;
        ready = false;
        Vector3 a = rod.position - rod.up * rodLength * .5f;
        Vector3 nearest = a + rod.up * Mathf.Clamp(Vector3.Dot(position - a, rod.up), 0, rodLength);
        if (Vector3.Distance(position, nearest) > .13f) return;
        holder = controller;
        gripOffset = Quaternion.Inverse(rotation) * (rod.position - position);
        gripRotation = Quaternion.Inverse(rotation) * rod.rotation;
        previousPosition = rod.position;
        Velocity = Vector3.zero;
    }

    public void AdjustCurrent(float direction)
    {
        if (induction) { note = "Induced current is determined by rod motion."; return; }
        float sign = current < 0 ? -1 : 1;
        current = sign * Mathf.Clamp(Mathf.Abs(current) + Mathf.Sign(direction) * .2f, 0, .6f);
    }
    public void AdjustField(float direction)
    {
        float sign = magneticStrength < 0 ? -1 : 1;
        float step = induction ? 1 : 3;
        magneticStrength = sign * Mathf.Clamp(Mathf.Abs(magneticStrength) + Mathf.Sign(direction) * step, 0, induction ? 3 : 9);
    }
    public void ReverseCurrent() { if (!induction) current = -current; }
    public void ReverseField() => magneticStrength = -magneticStrength;
    public void ToggleMode()
    {
        induction = !induction;
        magneticStrength = 0;
        Velocity = Vector3.zero;
        records.Clear();
        graph.Reset();
        note = induction ? "Move across B. Ideal closed circuit: I = E / R, R = 1 ohm." : "Rotate the rod; compare I, B and F directions.";
    }
    public void ToggleMicro()
    {
        micro = !micro;
        rodRenderer.sharedMaterial = micro ? transparentMaterial : solidMaterial;
    }
    public void ResetExperiment()
    {
        holder = OVRInput.Controller.None;
        leftReady = rightReady = false;
        rod.localPosition = homePosition;
        rod.localRotation = homeRotation;
        previousPosition = rod.position;
        Velocity = Vector3.zero;
        current = .4f;
        magneticStrength = induction ? 3 : 6;
        records.Clear();
        graph.Reset();
        note = "Reset. Grip near the rod to move it.";
    }
    public void RecordState()
    {
        if (records.Count >= 100) { note = "100 records reached. Reset to begin a new series."; return; }
        records.Add(new Record { induction = induction, field = magneticStrength, current = EffectiveCurrent,
            emf = Emf, force = Force.magnitude, speed = Velocity.magnitude, length = ActiveLength });
        graph.AddValue(records.Count, Force.magnitude);
        graph.DrawGraph();
        note = $"Recorded #{records.Count}: B={magneticStrength:F1} T, I={EffectiveCurrent:F3} A, E={Emf:F3} V, F={Force.magnitude:F3} N";
    }

    private void Refresh()
    {
        if (fieldIncrease) fieldIncrease.text = induction ? "+1" : "+3";
        if (fieldDecrease) fieldDecrease.text = induction ? "-1" : "-3";
        bool reversed = magneticStrength < 0;
        if (northLabel) northLabel.text = reversed ? "S" : "N";
        if (southLabel) southLabel.text = reversed ? "N" : "S";
        if (northPole) northPole.sharedMaterial = reversed ? southMaterial : northMaterial;
        if (southPole) southPole.sharedMaterial = reversed ? northMaterial : southMaterial;
        modeCaption.text = induction ? "Electromagnetic induction" : "Constant current";
        parameters[0].text = $"{EffectiveCurrent:F2} A";
        parameters[1].text = $"{magneticStrength:F0} T";
        parameters[2].text = micro ? "Micro ON" : "Micro OFF";
        readout.text = $"<i>F</i> = <i>I L</i> × <i>B</i>\n<i>E</i> = (<i>v</i> × <i>B</i>) · <i>L</i>\n\n"
            + $"<color=red>I = {EffectiveCurrent:F3} A</color>\n<color=blue>B = {magneticStrength:F1} T</color>\n"
            + $"<color=#008000>F = {Force.magnitude:F3} N</color>\nE = {Emf:F3} V\nv = {Velocity.magnitude:F2} m/s\n"
            + $"L = {ActiveLength:F3} m\n\n{note}";
    }

    private void OnDisable() { holder = OVRInput.Controller.None; Velocity = Vector3.zero; leftReady = rightReady = false; }
}

