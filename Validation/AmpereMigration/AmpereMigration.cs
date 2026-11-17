using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using TMPro;
using Object = UnityEngine.Object;

public static class AmpereMigration
{
    public static void Finish()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        var a = Object.FindFirstObjectByType<AmpereExperiment>();
        var menu = a.transform.Find("ParameterMenu");
        foreach (var t in menu.GetComponentsInChildren<TMP_Text>(true))
            if(t.text.Contains("SECONDARY")) t.text = "Directions";
        a.fieldIncrease = menu.Find("MassB/Add").GetComponentInChildren<TMP_Text>(true);
        a.fieldDecrease = menu.Find("MassB/Sub").GetComponentInChildren<TMP_Text>(true);
        a.northPole = a.transform.Find("MagneticApparatus/North pole").GetComponent<Renderer>();
        a.southPole = a.transform.Find("MagneticApparatus/South pole").GetComponent<Renderer>();
        a.northLabel = a.transform.Find("MagneticApparatus/N").GetComponent<TMP_Text>();
        a.southLabel = a.transform.Find("MagneticApparatus/S").GetComponent<TMP_Text>();
        a.northMaterial = Mat("Track/BlockA"); a.southMaterial = Mat("Track/BlockB");
        a.northLabel.color = a.southLabel.color = Color.white;
        a.readout.fontSize = 1.65f;
        a.modeCaption.gameObject.SetActive(false);
        // Keep the new controls inside the original transparent panel.
        var panel = menu.GetComponentsInChildren<Shapes.Rectangle>(true).FirstOrDefault();
        if(panel) panel.Height += .12f;
        foreach(var t in a.GetComponentsInChildren<TMP_Text>(true))
            if(t.text.Contains("TRANSFORMER")) t.text="AMPERE FORCE | GRIP: MOVE ROD | RED BUTTON: RESET";
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        EditorApplication.Exit(0);
    }
    static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>("Assets/ArtRes/Material/LabTable/" + name + ".mat");
    static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/ArtRes/Fonts/STZHONGS/STZHONGS SDF.asset");
    static Transform Shape(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        return go.transform;
    }
    static TMP_Text Label(Transform parent, string name, string text, Vector3 position, float width, float height, float size)
    {
        var t = new GameObject(name).AddComponent<TextMeshPro>();
        t.transform.SetParent(parent, false);
        t.transform.localPosition = position;
        t.font = Font;
        t.fontSize = size;
        t.color = Color.black;
        t.text = text;
        t.rectTransform.sizeDelta = new Vector2(width,height);
        t.alignment = TextAlignmentOptions.Center;
        return t;
    }
    static Arrow Arrow(Transform parent, string name, Color color, string prefix, string suffix)
    {
        var source = GameObject.Find("Objects/LabTable/Track/Couple/BlockA/ForceArrow");
        var go = Object.Instantiate(source, parent);
        go.name = name;
        go.transform.localScale = Vector3.one;
        var arrow = go.GetComponent<Arrow>();
        arrow.line = go.transform.Find("Line").GetComponent<Shapes.Line>();
        arrow.triangle = go.transform.Find("Triangle").GetComponent<Shapes.Triangle>();
        arrow.text = go.transform.Find("TextValue").GetComponent<TextMeshPro>();
        arrow.line.Color = color;
        arrow.triangle.Color = color;
        arrow.text.color = color;
        arrow.maxLength = .18f;
        arrow.lineWidth = .002f;
        arrow.arrowWidth = .008f;
        arrow.arrowLength = .02f;
        arrow.preFix = prefix;
        arrow.sufFix = suffix;
        arrow.text.fontSize = .14f;
        return arrow;
    }
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        var old = Object.FindFirstObjectByType<TransformerExperiment>();
        if (!old) throw new Exception("Expected the existing transformer station");
        var station = old.gameObject.AddComponent<AmpereExperiment>();
        station.name = "AmpereLab";
        station.readout = old.worldReadout;
        station.parameters = old.parameterValues;
        station.graph = old.ratioGraph;
        station.graph.valueFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/ArtRes/Fonts/Times New Roman/TIMES SDF.asset");
        station.readout.font = Font;
        station.readout.color = Color.black;
        station.readout.fontSize = .8f;
        station.readout.enableAutoSizing = false;
        station.readout.text = "Ampere force and electromagnetic induction";
        var apparatus = old.apparatus;
        foreach (Transform child in apparatus.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
        apparatus.name = "MagneticApparatus";
        apparatus.localScale = Vector3.one;
        Shape(apparatus,"Base",PrimitiveType.Cube,new Vector3(0,.02f,0),new Vector3(.65f,.04f,.4f),Mat("Knob/Bottom"));
        Shape(apparatus,"Magnet yoke",PrimitiveType.Cube,new Vector3(0,.26f,.15f),new Vector3(.55f,.07f,.06f),Mat("Track/Spring"));
        Shape(apparatus,"North pole",PrimitiveType.Cube,new Vector3(0,.38f,0),new Vector3(.5f,.06f,.28f),Mat("Track/BlockA"));
        Shape(apparatus,"South pole",PrimitiveType.Cube,new Vector3(0,.08f,0),new Vector3(.5f,.06f,.28f),Mat("Track/BlockB"));
        Label(apparatus,"N","N",new Vector3(0,.38f,-.145f),.1f,.04f,.25f);
        Label(apparatus,"S","S",new Vector3(0,.08f,-.145f),.1f,.04f,.25f);
        station.field = new GameObject("Field volume").transform;
        station.field.SetParent(apparatus,false);
        station.field.localPosition = new Vector3(0,.23f,0);
        // North above south: the initial field points down.
        station.field.localRotation = Quaternion.Euler(180,0,0);
        station.rod = Shape(apparatus,"Conductor",PrimitiveType.Cylinder,new Vector3(0,.23f,0),
            new Vector3(.022f,.17f,.022f),Mat("Track/Spring"));
        station.rod.localRotation = Quaternion.Euler(0,0,90);
        station.rodRenderer = station.rod.GetComponent<Renderer>();
        station.solidMaterial = Mat("Track/Spring");
        station.transparentMaterial = Mat("Track/Thin Glass 1");
        station.electrons = new Transform[8];
        for(int i=0;i<8;i++)
        {
            station.electrons[i]=Shape(station.rod,"Electron "+i,PrimitiveType.Sphere,Vector3.zero,
                new Vector3(.35f,.045f,.35f),Mat("Track/BlockB"));
            station.electrons[i].gameObject.SetActive(false);
        }
        station.fieldArrow = Arrow(apparatus,"B arrow",Color.blue,"B "," T");
        station.currentArrow = Arrow(apparatus,"I arrow",Color.red,"I "," A");
        station.forceArrow = Arrow(apparatus,"F arrow",Color.green,"F "," N");
        var menu = station.transform.Find("ParameterMenu");
        station.modeCaption = Label(menu,"Mode caption","Constant current",new Vector3(0,.36f,0),.65f,.07f,.3f);
        foreach(var t in menu.GetComponentsInChildren<TMP_Text>(true))
        {
            t.font=Font;
            if(t.name=="Text")
            {
                if(t.transform.parent.name=="MassA")t.text="Current I";
                if(t.transform.parent.name=="MassB")t.text="Field B";
                if(t.transform.parent.name=="SpringK")t.text="Directions";
            }
        }
        foreach(var entry in new[]{("MassA", "0.2"),("MassB","3"),("SpringK","")})
        {
            var p=menu.Find(entry.Item1);
            foreach(var t in p.GetComponentsInChildren<TMP_Text>(true))
                if(t.transform.parent.name=="ButtonVisual")t.text=entry.Item1=="SpringK" ? (t.transform.parent.parent.parent.name=="Add"?"Flip I":"Flip B") : (t.transform.parent.parent.parent.name=="Add"?"+":"-")+entry.Item2;
        }
        var sourceButton=menu.Find("MassA/Add").gameObject;
        var record=Object.Instantiate(sourceButton,menu);record.name="Record";
        record.transform.localPosition=new Vector3(-.09f,-.28f,0);
        var micro=Object.Instantiate(sourceButton,menu);micro.name="Micro";
        micro.transform.localPosition=new Vector3(.09f,-.28f,0);
        Bind(record,station.RecordState,"Record");
        Bind(micro,station.ToggleMicro,"Micro");
        var pointer=Object.FindFirstObjectByType<LaboratoryExperimentPointer>();
        var pointerSO=new SerializedObject(pointer);
        pointerSO.FindProperty("ampere").objectReferenceValue=station;
        pointerSO.FindProperty("transformer").objectReferenceValue=null;
        var buttons=pointerSO.FindProperty("buttons");
        foreach(var button in new[]{record,micro})
        {
            int n=buttons.arraySize++;
            var element=buttons.GetArrayElementAtIndex(n);
            element.FindPropertyRelative("surface").objectReferenceValue=button.GetComponent<Oculus.Interaction.PokeInteractable>();
            element.FindPropertyRelative("pointableEvents").objectReferenceValue=button.GetComponent<Oculus.Interaction.PointableUnityEventWrapper>();
            element.FindPropertyRelative("interactableEvents").objectReferenceValue=null;
        }
        pointerSO.ApplyModifiedPropertiesWithoutUndo();
        var map=new Dictionary<string,string>{
            {"AdjustVoltage","AdjustCurrent"},{"AdjustPrimary","AdjustField"},{"AdjustSecondary","ReverseField"},
            {"SelectKnob","ToggleMicro"},{"TogglePower","ToggleMode"},{"RecordState","ResetExperiment"},
            {"ResetExperiment","ResetExperiment"},{"ClearRecords","ResetExperiment"},{"TogglePanel","ToggleMode"}
        };
        foreach(var component in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            if(!component || component==old)continue;
            var so=new SerializedObject(component);
            var it=so.GetIterator();
            bool changed=false;
            while(it.Next(true))
            {
                if(it.name!="m_Target" || it.propertyType!=SerializedPropertyType.ObjectReference || it.objectReferenceValue!=old)continue;
                var prefix=it.propertyPath.Substring(0,it.propertyPath.Length-"m_Target".Length);
                var method=so.FindProperty(prefix+"m_MethodName");
                if(method==null)continue;
                string next=map.TryGetValue(method.stringValue,out var value)?value:"ResetExperiment";
                // The two former turns controls flip I and B.
                if(method.stringValue=="AdjustSecondary")
                    next=so.FindProperty(prefix+"m_Arguments.m_FloatArgument").floatValue>0?"ReverseCurrent":"ReverseField";
                it.objectReferenceValue=station;
                method.stringValue=next;
                so.FindProperty(prefix+"m_TargetAssemblyTypeName").stringValue="AmpereExperiment, Assembly-CSharp";
                if(next=="ReverseCurrent"||next=="ReverseField")so.FindProperty(prefix+"m_Mode").intValue=1;
                changed=true;
            }
            if(changed)so.ApplyModifiedPropertiesWithoutUndo();
        }
        old.desktopPanel.transform.root.gameObject.SetActive(false);
        old.previewCamera.gameObject.SetActive(false);
        foreach(var text in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            if(text.text=="Transformer"||text.text=="TRANSFORMER")text.text="Ampere";
        station.graph.transform.Find("TextTitle").GetComponent<TMP_Text>().text="Force records";
        foreach(var t in station.graph.GetComponentsInChildren<TMP_Text>(true))
        {
            t.font=station.graph.valueFont;t.color=Color.black;
            if(t.name=="LabelTime")t.text="Record";
            if(t.name.StartsWith("Label")&&t.transform.parent.name=="Y_Axis")t.text="F (N)";
        }
        Object.DestroyImmediate(old);
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("[AmpereMigration] Scene saved");
        EditorApplication.Exit(0);
    }
    static void Bind(GameObject button,UnityAction action,string caption)
    {
        var wrapper=button.GetComponent<Oculus.Interaction.PointableUnityEventWrapper>();
        var so=new SerializedObject(wrapper);
        var it=so.GetIterator();
        while(it.Next(true)) if(it.name=="m_Calls"&&it.isArray)it.ClearArray();
        so.ApplyModifiedPropertiesWithoutUndo();
        UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(wrapper.WhenSelect,action);
        foreach(var t in button.GetComponentsInChildren<TMP_Text>(true))t.text=caption;
    }
}

