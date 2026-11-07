using System;
using System.Collections.Generic;
using System.Linq;
using Oculus.Interaction;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Single acceleration station. All apparatus and UI references belong to Main.unity.</summary>
public class AccelerationExperiment : MonoBehaviour
{
    public Transform track, cart, bucket, pulley, weights;
    public MeshFilter pulleyMesh, bucketMesh, weightMesh, supportMesh, wheelMesh;
    public Material ropeMaterial;
    public GraphDrawer[] graphs;
    public TMP_Text worldReadout, desktopReadout;
    public TMP_Text[] parameterValues;
    public Slider[] parameterSliders;
    public KnobAngle knob;
    public KnobRotate hardwareKnob;
    public Behaviour[] handInputs;
    public PressInput pressInput;
    public AngleInput angleInput;
    public InteractableUnityEventWrapper cartGrabEvents;
    public Grabbable cartGrab;
    public TMP_FontAsset valueFont;
    [Range(0.2f, 2f)] public float cartMass = 0.5f;
    [Range(0, 0.25f)] public float hangingMass = 0.05f;
    [Range(0, 0.3f)] public float friction = 0;
    [Range(0, 12)] public float angleDegrees;
    public bool useHardwareKnob;

    public enum Phase { Ready, Running, Paused, Complete }
    public Phase phase;
    public int selectedParameter, graphMode;
    public double elapsed;
    public float travel = 0.8f;
    public readonly List<AccelerationDynamics.Sample> tape = new List<AccelerationDynamics.Sample>();
    [Serializable] public class Trial {
        public AccelerationDynamics.Settings settings;
        public double measuredAcceleration, force;
        public int samples;
    }
    public readonly List<Trial> trials = new List<Trial>();
    private AccelerationDynamics.Settings runSettings;
    private AccelerationDynamics.Solution solution;
    private double duration;
    private Vector3 trackHome, cartHome;
    private Quaternion trackRotation;
    private LineRenderer rope;
    private bool initialized;
    private float knobReadyAt;

    private void Awake()
    {
        trackHome = track.localPosition;
        trackRotation = track.localRotation;
        cartHome = cart.localPosition;
        AssignMesh(pulleyMesh, "Pulley");
        AssignMesh(bucketMesh, "Bucket");
        AssignMesh(weightMesh, "Weights");
        AssignMesh(supportMesh, "Support");
        AssignMesh(wheelMesh, "Wheels");
        rope = pulley.gameObject.AddComponent<LineRenderer>();
        rope.sharedMaterial = ropeMaterial;
        rope.startWidth = rope.endWidth = 0.003f;
        rope.positionCount = 3;
        rope.useWorldSpace = true;
        foreach (var g in graphs) g.valueFont = valueFont;
    }

    private void Start()
    {
        initialized = true;
        knobReadyAt = Time.time + 0.3f;
        InputModeUtil.Apply(!useHardwareKnob, handInputs, new Behaviour[] { hardwareKnob });
        if (pressInput != null) pressInput.onReleased.AddListener(PrimaryAction);
        if (cartGrabEvents != null) cartGrabEvents.WhenUnselect.AddListener(ResetExperiment);
        SetSliders();
        ResetExperiment();
        SelectGraph(0);
    }

    private static void AssignMesh(MeshFilter filter, string name)
    {
        var mesh = Resources.LoadAll<Mesh>("Acceleration/" + name).FirstOrDefault();
        if (mesh == null) { Debug.LogError("Acceleration apparatus mesh missing: " + name); return; }
        filter.sharedMesh = mesh;
    }

    private void OnDestroy()
    {
        if (pressInput != null) pressInput.onReleased.RemoveListener(PrimaryAction);
        if (cartGrabEvents != null) cartGrabEvents.WhenUnselect.RemoveListener(ResetExperiment);
    }

    private AccelerationDynamics.Settings Settings => new AccelerationDynamics.Settings(cartMass, hangingMass, friction, angleDegrees);

    private void Update()
    {
        if (!initialized) return;
        if (Input.GetKeyDown(KeyCode.Space)) PrimaryAction();
        if (Input.GetKeyDown(KeyCode.R)) ResetExperiment();
        if (Input.GetKeyDown(KeyCode.Tab)) selectedParameter = (selectedParameter + 1) % 4;
        if (phase == Phase.Ready) {
            float delta = useHardwareKnob && angleInput != null ? angleInput.Delta : knob != null ? knob.Delta : 0;
            if (Time.time >= knobReadyAt && Mathf.Abs(delta) > 0.01f) ChangeSelected(delta / 180f);
        }
        if (phase == Phase.Running) {
            elapsed = Math.Min(duration, elapsed + Time.deltaTime);
            while (tape.Count * AccelerationDynamics.SampleInterval <= elapsed + 1e-9) {
                double time = tape.Count * AccelerationDynamics.SampleInterval;
                var point = new AccelerationDynamics.Sample(time, solution.Acceleration);
                tape.Add(point);
                if (graphMode == 0) graphs[0].AddValue((float)time, (float)point.Velocity);
            }
            if (graphMode == 0) graphs[0].DrawGraph();
            if (elapsed >= duration) FinishTrial();
        }
        double displacement = 0.5 * solution.Acceleration * elapsed * elapsed;
        PoseApparatus((float)Math.Min(travel, displacement));
        UpdateReadouts();
    }

    public void PrimaryAction()
    {
        if (!initialized) return;
        if (phase == Phase.Complete) { ResetExperiment(); return; }
        if (phase == Phase.Running) { phase = Phase.Paused; return; }
        if (phase == Phase.Paused) { phase = Phase.Running; return; }
        runSettings = Settings;
        solution = AccelerationDynamics.Solve(runSettings);
        duration = AccelerationDynamics.Duration(solution, travel);
        elapsed = 0;
        tape.Clear();
        tape.Add(new AccelerationDynamics.Sample(0, solution.Acceleration));
        cartGrab.enabled = false;
        phase = Phase.Running;
        SelectGraph(0);
        graphs[0].AddValue(0, 0); // Actual first measured state at t=0, released from rest.
    }

    public void ResetExperiment()
    {
        if (!initialized) return;
        phase = Phase.Ready;
        elapsed = 0;
        tape.Clear();
        solution = AccelerationDynamics.Solve(Settings);
        cart.localPosition = cartHome;
        cartGrab.enabled = true;
        SetSliders();
        if (graphMode == 0) SelectGraph(0);
        PoseApparatus(0);
    }

    private void FinishTrial()
    {
        phase = Phase.Complete;
        if (tape.Count >= 3) trials.Add(new Trial {
            settings = runSettings, measuredAcceleration = AccelerationDynamics.MeasuredAcceleration(tape),
            force = solution.CartNetForce, samples = tape.Count
        });
        // Session records are retained across resets; nothing is written to project files.
    }

    private void PoseApparatus(float position)
    {
        track.localRotation = trackRotation * Quaternion.Euler(angleDegrees, 0, 0);
        // Keep the inclined rail referenced to its outboard pulley end. Presentation only.
        track.localPosition = trackHome + Vector3.up * (Mathf.Sin(angleDegrees * Mathf.Deg2Rad) * pulley.localPosition.z);
        cart.localPosition = cartHome + Vector3.forward * position;
        bucket.position = pulley.TransformPoint(new Vector3(0, -0.23f, 0.07f)) + Vector3.down * position;
        bucket.rotation = Quaternion.identity;
        weights.localScale = new Vector3(1, Mathf.Lerp(0.4f, 2, (cartMass - 0.2f) / 1.8f), 1);
        rope.SetPosition(0, cart.TransformPoint(new Vector3(0, 0.03f, 0.05f)));
        rope.SetPosition(1, pulley.position + track.up * 0.065f);
        rope.SetPosition(2, bucket.position + Vector3.up * 0.13f);
        pulleyMesh.transform.localRotation = Quaternion.Euler(position * Mathf.Rad2Deg / 0.065f, 0, 90);
    }

    private void ChangeSelected(float delta)
    {
        if (selectedParameter == 0) SetCartMass(cartMass + delta * 1.8f);
        else if (selectedParameter == 1) SetHangingMass(hangingMass + delta * 0.25f);
        else if (selectedParameter == 2) SetFriction(friction + delta * 0.3f);
        else SetAngle(angleDegrees + delta * 12);
    }
    public void SetCartMass(float value) { if (phase == Phase.Ready) { cartMass = Mathf.Clamp(value, 0.2f, 2); Changed(); } }
    public void SetHangingMass(float value) { if (phase == Phase.Ready) { hangingMass = Mathf.Clamp(value, 0, 0.25f); Changed(); } }
    public void SetFriction(float value) { if (phase == Phase.Ready) { friction = Mathf.Clamp(value, 0, 0.3f); Changed(); } }
    public void SetAngle(float value) { if (phase == Phase.Ready) { angleDegrees = Mathf.Clamp(value, 0, 12); Changed(); } }
    public void AdjustCartMass(float sign) => SetCartMass(cartMass + Mathf.Sign(sign) * 0.05f);
    public void AdjustHangingMass(float sign) => SetHangingMass(hangingMass + Mathf.Sign(sign) * 0.01f);
    public void AdjustFriction(float sign) => SetFriction(friction + Mathf.Sign(sign) * 0.01f);
    public void SelectParameter() { selectedParameter = (selectedParameter + 1) % 4; }
    public void CompensateFriction() { SetAngle(Mathf.Atan(friction) * Mathf.Rad2Deg); }
    private void Changed() { solution = AccelerationDynamics.Solve(Settings); SetSliders(); SelectGraph(graphMode); }
    private void SetSliders()
    {
        float[] values = {cartMass, hangingMass, friction, angleDegrees};
        for (int i = 0; i < parameterSliders.Length; i++) {
            parameterSliders[i].SetValueWithoutNotify(values[i]);
            parameterSliders[i].interactable = phase == Phase.Ready;
        }
    }

    public void ShowTimeGraph() => SelectGraph(0);
    public void ShowForceGraph() => SelectGraph(1);
    public void ShowMassGraph() => SelectGraph(2);
    public void SelectGraph(int mode)
    {
        graphMode = Mathf.Clamp(mode, 0, 2);
        for (int i = 0; i < graphs.Length; i++) {
            graphs[i].Reset();
            if (i == graphMode) graphs[i].Show(); else graphs[i].Hide();
            graphs[i].HideHighlightPoints();
            graphs[i].HideLineLabel();
        }
        var graph = graphs[graphMode];
        if (graphMode == 0) {
            // Reserve the complete run's extent so the plot stays stable while sampling.
            double runDuration = AccelerationDynamics.Duration(solution, travel);
            double maxTime = Math.Max(runDuration, tape.Count > 0 ? tape[tape.Count - 1].Time : 0);
            double maxSpeed = Math.Max(solution.Acceleration * runDuration,
                tape.Count > 0 ? tape.Max(point => point.Velocity) : 0);
            ConfigureGraphAxes(graph, maxTime, maxSpeed);
            foreach (var p in tape) graph.AddValue((float)p.Time, (float)p.Velocity);
        } else {
            var selected = trials.Where(t => graphMode == 1
                ? AccelerationDynamics.SameForceSeries(t.settings, Settings)
                : AccelerationDynamics.SameMassSeries(t.settings, Settings));
            var sorted = selected.OrderBy(t => graphMode == 1 ? t.force : 1 / t.settings.CartMass).ToList();
            double maxX = sorted.Count > 0
                ? sorted.Max(t => graphMode == 1 ? t.force : 1 / t.settings.CartMass)
                : graphMode == 1 ? 1 : 5;
            double maxY = sorted.Count > 0 ? sorted.Max(t => t.measuredAcceleration) : 1;
            ConfigureGraphAxes(graph, maxX, maxY);
            double last = double.NegativeInfinity;
            foreach (var t in sorted) {
                double x = graphMode == 1 ? t.force : 1 / t.settings.CartMass;
                if (Math.Abs(x - last) < 1e-6) continue;
                graph.AddValue((float)x, (float)t.measuredAcceleration);
                graph.SetHighlightPoint((float)x, (float)t.measuredAcceleration);
                graph.ShowHighlightPoints(); last = x;
            }
        }
        graph.DrawGraph();
    }

    // This is local to acceleration; shared graph components and other stations stay unchanged.
    private static void ConfigureGraphAxes(GraphDrawer graph, double maxX, double maxY)
    {
        float xMax = GraphDisplayMaximum(maxX);
        graph.xAxis.valueRange = xMax;
        graph.xAxis.minIndex = 0;
        graph.xAxis.maxIndex = xMax;
        graph.xAxis.unitValue = graph.yAxis.GetUnitValue(xMax);
        graph.yAxis.centerPos = -1; // Numeric zero and the horizontal axis both at the bottom.
        graph.yAxis.minIndex = 0;
        graph.yAxis.maxIndex = GraphDisplayMaximum(maxY);
        // Apply immediately too, avoiding a frame at the legacy centered origin on graph switches.
        Vector3 axisPosition = graph.xAxis.transform.localPosition;
        axisPosition.y = -graph.yAxis.WorldLength / 2;
        graph.xAxis.transform.localPosition = axisPosition;
    }

    private static float GraphDisplayMaximum(double maximum)
    {
        // 10% headroom, rounded to readable 1/2/5 intervals; minimum avoids repeated 0.0 ticks.
        double padded = Math.Max(0.5, maximum * 1.1);
        double targetStep = padded / 5;
        double magnitude = Math.Pow(10, Math.Floor(Math.Log10(targetStep)));
        double fraction = targetStep / magnitude;
        double step = (fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 5 ? 5 : 10) * magnitude;
        return (float)(Math.Ceiling(padded / step) * step);
    }

    private void UpdateReadouts()
    {
        SetSliders();
        var p = phase == Phase.Ready ? Settings : runSettings;
        double velocity = phase == Phase.Complete ? 0 : solution.Acceleration * elapsed;
        string[] names = {"CART MASS", "BUCKET MASS", "FRICTION", "DOWNHILL ANGLE"};
        if (worldReadout != null) worldReadout.text = string.Format(
            "NEWTON'S SECOND LAW  |  {0}\n" +
            "M {1:F2} kg   m {2:F2} kg\n" +
            "mu {3:F2}   angle {4:F1} deg\n" +
            "t {5:F2} s   x {6:F3} m\n" +
            "v {7:F3} m/s   a {8:F3} m/s^2\n" +
            "Tension {9:F3} N\nTrials {10}",
            phase, p.CartMass, p.HangingMass, p.Friction, p.AngleDegrees,
            elapsed, 0.5 * solution.Acceleration * elapsed * elapsed,
            velocity, solution.Acceleration, solution.Tension, trials.Count);
        if (desktopReadout != null) {
            // Show only operation-relevant measurements; recording retains every trial.
            string status = phase == Phase.Ready ? "Ready" : phase == Phase.Running ? "Running"
                : phase == Phase.Paused ? "Paused" : "Complete";
            string summary = string.Format(
                "<b><color=#176B70>ACCELERATION EXPERIMENT</color></b>\n" +
                "Status: <b>{0}</b>\n\n" +
                "Time  {1:F2} s    Distance  {2:F3} m\n" +
                "Speed  {3:F3} m/s\n" +
                "<b>Acceleration  {4:F3} m/s^2</b>\n" +
                "Cart net force  {5:F3} N\n",
                status, elapsed, 0.5 * solution.Acceleration * elapsed * elapsed,
                velocity, solution.Acceleration, solution.CartNetForce);
            if (phase == Phase.Complete && tape.Count >= 3)
                summary += string.Format("Measured a  {0:F3} m/s^2\n",
                    AccelerationDynamics.MeasuredAcceleration(tape));
            summary += "\nKnob: " + names[selectedParameter];
            if (trials.Count > 0) summary += "   |   Trials: " + trials.Count;
            if (phase == Phase.Ready && friction > Mathf.Tan(12 * Mathf.Deg2Rad))
                summary += "\n<color=#9B4C22>Friction balance exceeds the angle limit.</color>";
            desktopReadout.text = summary;
        }
        if (parameterValues.Length >= 3) {
            parameterValues[0].text = cartMass.ToString("F2");
            parameterValues[1].text = hangingMass.ToString("F2");
            parameterValues[2].text = friction.ToString("F2");
        }
    }
}
