using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Independent virtual AC transformer station; no real power or serial protocol control.</summary>
public class TransformerExperiment : MonoBehaviour
{
    public Transform apparatus;
    public MeshFilter coreMesh,primaryMesh,secondaryMesh,supplyMesh,metersMesh,switchMesh;
    public GraphDrawer ratioGraph;
    public TMP_Text worldReadout,desktopReadout,powerCaption;
    public TMP_Text[] parameterValues;
    public Slider voltageSlider,primarySlider,secondarySlider;
    public KnobAngle knob;
    public GameObject desktopPanel;
    public Camera previewCamera;
    public RawImage previewImage;
    [Range(0,24)] public float inputVoltage=6;
    [Range(100,1000)] public int primaryTurns=200,secondaryTurns=400;
    public bool powered;
    [Serializable] public class Record {
        public int n1,n2;
        public double setting,u1,u2;
        public bool powered;
        public double Ratio => (double)n2/n1;
    }
    public readonly List<Record> records=new List<Record>();
    public TransformerPhysics.State Current => TransformerPhysics.At(inputVoltage,primaryTurns,secondaryTurns,powered);
    private Canvas accelerationDesktopCanvas;
    private IsothermalGasExperiment gasStation;
    private bool ready;
    private int knobTarget;
    private float knobReadyAt;
    private Camera graphCamera;
    private RenderTexture preview,graphPreview;
    private Material switchMaterial;
    private TMP_Text primaryLabel,secondaryLabel,inputMeter,outputMeter;
    private string message="Select turns and input voltage, turn AC ON, then RECORD.";

    private void Awake()
    {
        desktopPanel.GetComponentInParent<CanvasScaler>().matchWidthOrHeight=1;
        // This additional slider belongs only to this panel; no scene-wide lookup.
        if(secondarySlider==null)secondarySlider=desktopPanel.transform.Find("SecondaryTurns").GetComponent<Slider>();
        // Match the fixed, readable typography of the other desktop station controls.
        desktopReadout.enableAutoSizing=false;
        Assign(coreMesh,"Core");Assign(primaryMesh,"PrimaryCoil");Assign(secondaryMesh,"SecondaryCoil");
        Assign(supplyMesh,"ACSupply");Assign(metersMesh,"Meters");Assign(switchMesh,"Switch");
        primaryLabel=Label("PrimaryLabel",new Vector3(-.13f,.42f,0),.023f);
        secondaryLabel=Label("SecondaryLabel",new Vector3(.13f,.42f,0),.023f);
        inputMeter=Label("InputRMS",new Vector3(-.22f,.145f,-.307f),.018f);
        outputMeter=Label("OutputRMS",new Vector3(.22f,.145f,-.307f),.018f);
        switchMaterial=switchMesh.GetComponent<Renderer>().material;
        foreach(var child in apparatus.GetComponentsInChildren<Transform>(true))child.gameObject.layer=28;
        preview=new RenderTexture(1000,350,24,RenderTextureFormat.ARGB32){name="TransformerApparatusPreview"};
        preview.Create();previewImage.texture=preview;previewImage.uvRect=new Rect(0,0,1,1);
        previewImage.rectTransform.sizeDelta=new Vector2(840,280);
        previewCamera.targetTexture=preview;previewCamera.orthographic=false;previewCamera.fieldOfView=50;
        previewCamera.aspect=3;previewCamera.stereoTargetEye=StereoTargetEyeMask.None;
        previewCamera.cullingMask=1<<28;previewCamera.useOcclusionCulling=false;
        previewCamera.transform.position=apparatus.position-transform.forward*.85f+transform.right*.32f+Vector3.up*.45f;
        previewCamera.transform.LookAt(apparatus.position+Vector3.up*.16f);
        previewCamera.enabled=false;
        previewImage.enabled=false;
        var acceleration=FindObjectOfType<AccelerationExperiment>();
        if(acceleration!=null && acceleration.desktopReadout!=null)
            accelerationDesktopCanvas=acceleration.desktopReadout.GetComponentInParent<Canvas>();
        gasStation=FindObjectOfType<IsothermalGasExperiment>();
        desktopPanel.SetActive(false);
    }
    private static void Assign(MeshFilter filter,string name)
    {
        var mesh=Resources.LoadAll<Mesh>("Transformer/"+name).FirstOrDefault();
        if(mesh==null){Debug.LogError("Transformer mesh missing: "+name);return;}
        filter.sharedMesh=mesh;
    }
    private TMP_Text Label(string name,Vector3 position,float height)
    {
        var text=new GameObject(name).AddComponent<TextMeshPro>();
        text.transform.SetParent(apparatus,false);text.transform.localPosition=position;
        text.rectTransform.sizeDelta=new Vector2(.21f,.055f);
        text.font=ratioGraph.valueFont;text.fontSize=height*10;text.alignment=TextAlignmentOptions.Center;
        text.color=new Color(.08f,.12f,.15f);text.enableWordWrapping=false;
        return text;
    }
    private void Start()
    {
        ready=true;knobReadyAt=Time.time+.3f;
        ratioGraph.xAxis.valueRange=10;ratioGraph.xAxis.unitValue=2;
        ratioGraph.xAxis.minIndex=0;ratioGraph.xAxis.maxIndex=10;
        ratioGraph.yAxis.centerPos=-1;ratioGraph.yAxis.minIndex=0;ratioGraph.yAxis.maxIndex=10;
        ratioGraph.Show();ratioGraph.HideLineLabel();
        ResetExperiment(); // Use the existing world graph, without a desktop preview overlay.
    }
    private void Update()
    {
        if(!ready)return;
        if(knob!=null&&Time.time>=knobReadyAt&&Mathf.Abs(knob.Delta)>.01f){
            if(knobTarget==0)SetInputVoltage(inputVoltage+knob.Delta*.1f);
            else if(knobTarget==1)SetPrimaryTurns(primaryTurns+knob.Delta*10);
            else SetSecondaryTurns(secondaryTurns+knob.Delta*10);
        }
        Refresh();
    }
    public void SetInputVoltage(float value){inputVoltage=(float)TransformerPhysics.ClampVoltage(value);Synchronize();}
    public void SetPrimaryTurns(float value){primaryTurns=TransformerPhysics.ClampTurns(value);Synchronize();}
    public void SetSecondaryTurns(float value){secondaryTurns=TransformerPhysics.ClampTurns(value);Synchronize();}
    public void AdjustVoltage(float direction)=>SetInputVoltage(inputVoltage+Mathf.Sign(direction));
    public void AdjustPrimary(float direction)=>SetPrimaryTurns(primaryTurns+Mathf.Sign(direction)*50);
    public void AdjustSecondary(float direction)=>SetSecondaryTurns(secondaryTurns+Mathf.Sign(direction)*50);
    public void SelectKnob(){knobTarget=(knobTarget+1)%3;}
    public void TogglePower(){powered=!powered;message=powered?"Virtual AC supply ON (RMS).":"AC OFF: actual U1 and U2 are zero; setting retained.";}
    public void StepUp(){primaryTurns=200;secondaryTurns=400;Synchronize();}
    public void StepDown(){primaryTurns=200;secondaryTurns=100;Synchronize();}
    public void EqualTurns(){primaryTurns=secondaryTurns=200;Synchronize();}
    public void ResetExperiment()
    {
        inputVoltage=6;primaryTurns=200;secondaryTurns=400;powered=false;knobTarget=0;records.Clear();
        message="Reset: AC OFF, U1 setting 6 V, N1=200, N2=400; transformer records cleared.";
        Synchronize();if(ready)Replot();
    }
    public void ClearRecords(){records.Clear();message="Transformer records cleared.";if(ready)Replot();}
    public void RecordState()
    {
        if(!ready)return;
        var s=Current;
        if(records.Count>=100){message="100 records reached; clear records before continuing.";return;}
        if(records.Any(r=>r.n1==s.N1&&r.n2==s.N2&&Math.Abs(r.setting-s.SetVoltage)<.001&&r.powered==powered)){
            message="This state is already recorded. Change voltage, turns or power.";return;
        }
        records.Add(new Record{n1=s.N1,n2=s.N2,setting=s.SetVoltage,u1=s.InputVoltage,u2=s.OutputVoltage,powered=s.Powered});
        message="Recorded #"+records.Count+". OFF / zero-input rows are excluded from the ratio graph.";
        Debug.Log(string.Format("[Transformer] record {0}: ON={1} N1={2} N2={3} U1={4:F3} U2={5:F3} RMS",records.Count,s.Powered,s.N1,s.N2,s.InputVoltage,s.OutputVoltage));
        Replot();
    }
    private void Synchronize()
    {
        if(!ready)return;
        voltageSlider.SetValueWithoutNotify(inputVoltage);primarySlider.SetValueWithoutNotify(primaryTurns);secondarySlider.SetValueWithoutNotify(secondaryTurns);
    }
    private void Replot()
    {
        ratioGraph.Reset();ratioGraph.xAxis.valueRange=10;ratioGraph.xAxis.minIndex=0;ratioGraph.xAxis.maxIndex=10;
        ratioGraph.yAxis.minIndex=0;ratioGraph.yAxis.maxIndex=10;
        foreach(var r in records.Where(r=>r.powered&&r.u1>0).GroupBy(r=>Math.Round(r.Ratio,6)).Select(g=>g.First()).OrderBy(r=>r.Ratio))
            ratioGraph.AddValue((float)r.Ratio,(float)(r.u2/r.u1));
        ratioGraph.DrawGraph();
    }
    public void TogglePanel()
    {
        bool show=!desktopPanel.activeSelf;
        if(show && gasStation!=null && gasStation.desktopPanel.activeSelf)
            gasStation.TogglePanel();
        desktopPanel.SetActive(show);
        previewCamera.enabled=false;
        if(accelerationDesktopCanvas!=null) {
            var view=FindObjectOfType<LaboratoryPlayerView>();
            accelerationDesktopCanvas.enabled=!show && view!=null && view.DebugVisible
                && (gasStation==null || !gasStation.desktopPanel.activeSelf);
        }
        if(graphCamera!=null)graphCamera.enabled=show;
        if(show){Synchronize();Refresh();}
    }
    private void LateUpdate()
    {
        // The gas panel can also be opened from the shared navigation buttons.
        if(desktopPanel.activeSelf && gasStation!=null && gasStation.desktopPanel.activeSelf)
            desktopPanel.SetActive(false);
        if(desktopPanel.activeSelf && accelerationDesktopCanvas!=null)
            accelerationDesktopCanvas.enabled=false;
    }

    private void Refresh()
    {
        var s=Current;
        if(s.VoltageRatio.HasValue){ratioGraph.SetHighlightPoint((float)s.TurnsRatio,(float)s.VoltageRatio.Value);ratioGraph.ShowHighlightPoints();}
        else ratioGraph.HideHighlightPoints();
        var sb=new StringBuilder();
        sb.AppendLine("IDEAL TRANSFORMER | AC RMS");
        sb.AppendFormat("AC POWER: {0} (virtual only)\nU1 setting = {1:F2} V\nActual U1 = {2:F3} V RMS\nActual U2 = {3:F3} V RMS\nN1 = {4} turns; N2 = {5} turns\nN2/N1 = {6:F4}\n",s.Powered?"ON":"OFF",s.SetVoltage,s.InputVoltage,s.OutputVoltage,s.N1,s.N2,s.TurnsRatio);
        sb.AppendLine("U2/U1 = "+(s.VoltageRatio.HasValue?s.VoltageRatio.Value.ToString("F4"):"undefined (actual U1 = 0)"));
        sb.AppendLine(s.N2>s.N1?"STEP-UP":s.N2<s.N1?"STEP-DOWN":"1:1 ISOLATION RATIO");
        sb.AppendLine("U2 = U1 * N2 / N1; ideal no-load, no losses.");
        sb.AppendLine("Virtual supply only; no real electrical hardware.");
        sb.AppendLine("A: AC / input   B: turns   C: record");
        sb.AppendLine("Range: U1 0-24 V; integer turns 100-1000.");
        sb.AppendLine("Graph: x=N2/N1; y=U2/U1. Powered, U1>0 only.");
        sb.AppendLine("Equal x values share one graph point; table keeps all rows.");
        sb.AppendLine("Live marker is not a saved record.");
        sb.AppendLine("Knob target: "+new[]{"INPUT VOLTAGE","PRIMARY TURNS","SECONDARY TURNS"}[knobTarget]);
        sb.AppendLine("Records: "+records.Count+" / 100");sb.AppendLine(message);
        sb.AppendLine("Last 5: POWER / N1 / N2 / U1 / U2 (RMS V)");
        foreach(var r in records.Skip(Math.Max(0,records.Count-5)))sb.AppendFormat("{0} / {1} / {2} / {3:F2} / {4:F2}\n",r.powered?"ON":"OFF",r.n1,r.n2,r.u1,r.u2);
        worldReadout.text=sb.ToString();
        int plottedCount=records.Where(r=>r.powered && r.u1>0)
            .Select(r=>Math.Round(r.Ratio,6)).Distinct().Count();
        desktopReadout.text=string.Format(
            "<b><color=#176B70>TRANSFORMER EXPERIMENT</color></b>\n" +
            "AC power: <b>{0}</b>\n\n" +
            "Input setting  {1:F2} V RMS\n" +
            "U1  {2:F2} V RMS\n" +
            "<b>U2  {3:F2} V RMS</b>\n" +
            "N1  {4} turns    N2  {5} turns\n" +
            "N2/N1  {6:F2}    U2/U1  {7}\n\n" +
            "Knob: {8}\nRecords: {9}\n" +
            "Plot: {10} distinct ratio point(s)\n",
            s.Powered?"ON":"OFF",s.SetVoltage,s.InputVoltage,s.OutputVoltage,s.N1,s.N2,
            s.TurnsRatio,s.VoltageRatio.HasValue?s.VoltageRatio.Value.ToString("F2"):"--",
            new[]{"INPUT VOLTAGE","PRIMARY TURNS","SECONDARY TURNS"}[knobTarget],records.Count,plottedCount);
        var recordTable=new StringBuilder();
        recordTable.Append("\n<size=80%><b>RECENT RECORDS (last 5; voltages in RMS V)</b>\n");
        recordTable.Append("<mspace=0.52em>");
        recordTable.AppendFormat("{0,3} {1,3} {2,4} {3,4} {4,5} {5,5} {6,6}\n",
            "#","AC","N1","N2","Set","U1","U2");
        for(int i=Math.Max(0,records.Count-5);i<records.Count;i++) {
            var row=records[i];
            recordTable.AppendFormat("{0,3} {1,3} {2,4} {3,4} {4,5:F2} {5,5:F2} {6,6:F2}\n",
                i+1,row.powered?"ON":"OFF",row.n1,row.n2,row.setting,row.u1,row.u2);
        }
        if(records.Count==0)recordTable.AppendLine("No saved records. Press RECORD.");
        recordTable.Append("</mspace>Same ratio shares one plot point.\n\n");
        recordTable.Append(message).Append("</size>");
        desktopReadout.text+=recordTable.ToString();
        powerCaption.text=powered?"AC POWER: ON":"AC POWER: OFF";
        if(parameterValues.Length==3){parameterValues[0].text=inputVoltage.ToString("F1");parameterValues[1].text=primaryTurns.ToString();parameterValues[2].text=secondaryTurns.ToString();}
        primaryLabel.text="PRIMARY\nN1 = "+s.N1;secondaryLabel.text="SECONDARY\nN2 = "+s.N2;
        inputMeter.text="INPUT\n"+s.InputVoltage.ToString("F2")+" V RMS";outputMeter.text="OUTPUT\n"+s.OutputVoltage.ToString("F2")+" V RMS";
        switchMaterial.color=powered?new Color(.12f,.8f,.3f):new Color(.3f,.32f,.35f);
    }
    private void OnDestroy()
    {
        if(previewCamera!=null)previewCamera.targetTexture=null;if(graphCamera!=null)graphCamera.targetTexture=null;
        if(preview!=null){preview.Release();Destroy(preview);}if(graphPreview!=null){graphPreview.Release();Destroy(graphPreview);}
        if(switchMaterial!=null)Destroy(switchMaterial);
    }
}
