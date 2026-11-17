using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Own apparatus, controls and records. Does not use acceleration state or sensor singletons.</summary>
public class IsothermalGasExperiment : MonoBehaviour
{
    public Transform chamber, piston, gasFill, gaugeNeedle;
    public MeshFilter glassMesh, pistonMesh, fillMesh, frameMesh, gaugeMesh, needleMesh;
    public GraphDrawer pressureGraph;
    public TMP_Text worldReadout, desktopReadout, modeCaption;
    public TMP_Text[] parameterValues;
    public Slider volumeSlider, temperatureSlider;
    public KnobAngle volumeKnob;
    public GameObject desktopPanel;
    public Camera previewCamera;
    public RawImage previewImage;
    [Range(200,1000)] public float volumeMl = 500;
    [Range(250,400)] public float temperatureK = 300;
    public bool isothermal = true;
    public float thermostatKelvin = 300;

    [Serializable] public class Record {
        public double volumeMl, pressureKPa, kelvin, pv;
        public bool isothermal;
    }
    public readonly List<Record> records = new List<Record>();
    private RenderTexture preview, graphPreview;
    private Camera graphPreviewCamera;
    [SerializeField] private Canvas sharedDesktopCanvas;
    private bool ready;
    private float knobReadyAt;
    private string message = "Set volume, keep temperature, then RECORD each equilibrium state.";
    public IsothermalGasPhysics.State Current => IsothermalGasPhysics.At(volumeMl, temperatureK);

    private void Awake()
    {
        desktopPanel.GetComponentInParent<CanvasScaler>().matchWidthOrHeight = 1;
        AssignMesh(glassMesh,"Cylinder"); AssignMesh(pistonMesh,"Piston");
        AssignMesh(fillMesh,"GasFill"); AssignMesh(frameMesh,"Frame");
        AssignMesh(gaugeMesh,"Gauge"); AssignMesh(needleMesh,"Needle");
        // Private preview: only the apparatus is rendered, with a full-image UV rectangle.
        preview = new RenderTexture(1000,700,24,RenderTextureFormat.ARGB32) { name = "GasStationPreview" };
        preview.Create();
        previewCamera.targetTexture=preview;
        previewImage.texture=preview;
        previewImage.uvRect=new Rect(0,0,1,1);
        previewCamera.orthographic=false;
        previewCamera.fieldOfView=50;
        previewCamera.aspect=3f;
        previewCamera.stereoTargetEye=StereoTargetEyeMask.None;
        foreach(var child in chamber.parent.GetComponentsInChildren<Transform>(true)) child.gameObject.layer=30;
        previewCamera.cullingMask=1<<30;
        previewCamera.useOcclusionCulling=false;
        previewCamera.transform.position=chamber.position-transform.forward*0.75f+transform.right*0.4f+Vector3.up*0.42f;
        previewCamera.transform.LookAt(chamber.position+Vector3.up*0.12f);
        previewCamera.enabled=false;
        previewImage.enabled=false; // Desktop uses the room apparatus, like acceleration.
        if (sharedDesktopCanvas == null)
        {
            // The saved URP diagnostic scene still uses its original station.
            var acceleration = FindObjectOfType<AccelerationExperiment>();
            if (acceleration != null && acceleration.desktopReadout != null)
                sharedDesktopCanvas = acceleration.desktopReadout.GetComponentInParent<Canvas>();
        }
        desktopPanel.SetActive(false);
        gaugeNeedle.parent.localRotation=Quaternion.Euler(0,180,0);
    }

    private static void AssignMesh(MeshFilter filter, string name)
    {
        var mesh = Resources.LoadAll<Mesh>("IsothermalGas/"+name).FirstOrDefault();
        if (mesh == null) { Debug.LogError("Gas apparatus mesh missing: "+name); return; }
        filter.sharedMesh = mesh;
    }

    private void Start()
    {
        ready = true;
        knobReadyAt = Time.time + 0.3f;
        pressureGraph.Reset();
        pressureGraph.yAxis.centerPos = -1;
        pressureGraph.yAxis.minIndex = 0; pressureGraph.yAxis.maxIndex = 400;
        pressureGraph.xAxis.valueRange = 1000;
        pressureGraph.xAxis.unitValue = 200;
        pressureGraph.xAxis.maxIndex = 1000; pressureGraph.xAxis.minIndex = 0;
        pressureGraph.Show(); pressureGraph.HideLineLabel();
        ResetExperiment();
        // Keep the existing world graph; no desktop preview overlay.
    }

    private void Update()
    {
        if (!ready) return;
        if (volumeKnob != null && Time.time >= knobReadyAt && Mathf.Abs(volumeKnob.Delta)>0.01f)
            SetVolumeMl(volumeMl + volumeKnob.Delta * 4);
        RefreshDisplays();
    }

    public void SetVolumeMl(float value)
    {
        volumeMl=(float)IsothermalGasPhysics.At(value,temperatureK).VolumeMl;
        SynchronizeControls();
    }
    public void SetTemperatureK(float value)
    {
        if (isothermal) {
            message="Thermostat holds "+thermostatKelvin.ToString("F1")+" K. Turn ISOTHERMAL OFF to change T.";
            SynchronizeControls();return;
        }
        temperatureK=(float)IsothermalGasPhysics.At(volumeMl,value).TemperatureK;
        thermostatKelvin=temperatureK;
        message="Free-temperature equilibrium. Records are labelled FREE and excluded from isotherms.";
        SynchronizeControls(); Replot();
    }
    public void ToggleIsothermal() => SetIsothermal(!isothermal);
    private void SetIsothermal(bool enabled)
    {
        if (enabled) thermostatKelvin=temperatureK;
        isothermal=enabled;
        message=enabled ? "Thermostat holds current T; record different volumes for this isotherm."
                        : "Temperature unlocked; n remains fixed. No transient process is modelled.";
        SynchronizeControls(); Replot();
    }
    public void AdjustVolume(float direction) => SetVolumeMl(volumeMl+Mathf.Sign(direction)*50);
    public void AdjustTemperature(float direction) => SetTemperatureK(temperatureK+Mathf.Sign(direction)*5);
    public void SetModeFromStep(float direction) => SetIsothermal(direction>0);
    public void Volume500() => SetVolumeMl(500);
    public void Volume250() => SetVolumeMl(250);
    public void Temperature300() => SetTemperatureK(300);
    public void Temperature350() => SetTemperatureK(350);

    public void RecordState()
    {
        if (!ready) return;
        var state=Current;
        if (records.Any(r=>Math.Abs(r.volumeMl-state.VolumeMl)<0.01 && Math.Abs(r.kelvin-state.TemperatureK)<0.01 && r.isothermal==isothermal)) {
            message="This equilibrium state is already recorded; change V or T.";return;
        }
        if (records.Count>=200) {message="200 records reached. CLEAR RECORDS before a new session.";return;}
        records.Add(new Record {volumeMl=state.VolumeMl,pressureKPa=state.PressureKPa,
            kelvin=state.TemperatureK,pv=state.PV,isothermal=isothermal});
        message="Recorded equilibrium #"+records.Count+" ("+(isothermal?"ISOTHERMAL":"FREE")+").";
        Debug.Log(string.Format("[Gas] record {0}: V={1:F1} mL T={2:F1} K p={3:F3} kPa pV={4:F5} J mode={5}",
            records.Count,state.VolumeMl,state.TemperatureK,state.PressureKPa,state.PV,isothermal));
        Replot();
    }

    public void ResetExperiment()
    {
        if (!ready) return;
        volumeMl=500;temperatureK=300;thermostatKelvin=300;isothermal=true;
        message="Reset to 500 mL / 300 K. Recorded states retained; CLEAR RECORDS removes only gas data.";
        SynchronizeControls();Replot();
    }
    public void ClearRecords()
    {
        records.Clear();message="Gas records cleared. Acceleration records are untouched.";
        if (ready) Replot();
    }

    public void TogglePanel()
    {
        bool show=!desktopPanel.activeSelf;
        desktopPanel.SetActive(show);
        previewCamera.enabled=false; // No floating apparatus preview in the desktop controls.
        if (sharedDesktopCanvas != null) {
            var view = FindObjectOfType<LaboratoryPlayerView>();
            sharedDesktopCanvas.enabled = !show && view != null && view.DebugVisible;
        }
        if(graphPreviewCamera!=null)graphPreviewCamera.enabled=show;
        if(show) { SynchronizeControls();RefreshDisplays(); }
    }

    private void LateUpdate()
    {
        if (desktopPanel.activeSelf && sharedDesktopCanvas != null)
            sharedDesktopCanvas.enabled = false;
    }

    private void SynchronizeControls()
    {
        if (!ready) return;
        if (isothermal) temperatureK=thermostatKelvin;
        volumeSlider.SetValueWithoutNotify(volumeMl);
        temperatureSlider.SetValueWithoutNotify(temperatureK);
        temperatureSlider.interactable=!isothermal;
    }

    private void Replot()
    {
        if (!ready) return;
        pressureGraph.Reset();
        pressureGraph.xAxis.valueRange=1000;pressureGraph.xAxis.minIndex=0;pressureGraph.xAxis.maxIndex=1000;
        pressureGraph.yAxis.minIndex=0;pressureGraph.yAxis.maxIndex=400;
        var selected=records.Where(r=>IsothermalGasPhysics.SameIsotherm(r.kelvin,r.isothermal,temperatureK))
            .OrderBy(r=>r.volumeMl);
        foreach(var r in selected) pressureGraph.AddValue((float)r.volumeMl,(float)r.pressureKPa);
        pressureGraph.DrawGraph();
    }

    private void RefreshDisplays()
    {
        var s=Current;
        // V=A*h: piston height and pressure always refer to the same gas volume.
        piston.localPosition=new Vector3(0,(float)s.PistonHeightMetres+0.02f,0);
        gasFill.localPosition=new Vector3(0,0.02f,0);
        gasFill.localScale=new Vector3(1,(float)s.PistonHeightMetres,1);
        gaugeNeedle.localRotation=Quaternion.Euler(0,0,120-(float)(s.PressurePa/400000)*240);
        if (isothermal) {
            pressureGraph.SetHighlightPoint((float)s.VolumeMl,(float)s.PressureKPa);
            pressureGraph.ShowHighlightPoints();
        } else pressureGraph.HideHighlightPoints();
        int count=records.Count(r=>IsothermalGasPhysics.SameIsotherm(r.kelvin,r.isothermal,temperatureK));
        worldReadout.text = $"<b>BOYLE'S LAW</b>\nIsothermal: {(isothermal ? "ON" : "OFF")}\n\n" +
            $"Volume  {s.VolumeMl:F0} mL\n<b>Pressure  {s.PressureKPa:F2} kPa</b>\n" +
            $"Temperature  {s.TemperatureK:F1} K\npV  {s.PV:F3} J\n\n" +
            $"p = nRT / V   |   n = 0.020 mol\nPlotted: {count}   |   Records: {records.Count}\n{message}";
        desktopReadout.text=string.Format(
            "<b><color=#176B70>GAS EXPERIMENT</color></b>\n" +
            "Temperature: <b>{0}</b>\n\n" +
            "Volume  {1:F1} mL\n" +
            "<b>Pressure  {2:F2} kPa</b>\n" +
            "Temperature  {3:F1} K\n" +
            "pV  {4:F3} J\n\n" +
            "Records: {5}   |   This isotherm: {6}\n" +
            "<size=80%>{7}</size>",
            isothermal ? "Locked" : "Adjustable", s.VolumeMl, s.PressureKPa,
            s.TemperatureK, s.PV, records.Count, count, message);
        modeCaption.text=isothermal?"ISOTHERMAL: ON":"ISOTHERMAL: OFF";
        if(parameterValues.Length==3) {
            parameterValues[0].text=$"{volumeMl:F0} mL";parameterValues[1].text=$"{temperatureK:F1} K";
            parameterValues[2].text=isothermal?"ON":"OFF";
        }

    }

    private void OnDestroy()
    {
        if(previewCamera!=null)previewCamera.targetTexture=null;
        if(preview!=null){preview.Release();Destroy(preview);}
        if(graphPreviewCamera!=null)graphPreviewCamera.targetTexture=null;
        if(graphPreview!=null){graphPreview.Release();Destroy(graphPreview);}
    }
}
