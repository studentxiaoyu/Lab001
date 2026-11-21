using System;
using Fusion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Creates the small network-state prefab without modifying Main or its unsaved changes.</summary>
public static class LaboratoryNetworkSetup
{
    [MenuItem("Tools/Lab/Create Network Avatar Prefab")]
    public static void CreatePrefab()
    {
        const string path = "Assets/Resources/LaboratoryNetworkAvatar.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        var root = new GameObject("LaboratoryNetworkAvatar");
        root.AddComponent<NetworkObject>().Flags |= NetworkObjectFlags.DestroyWhenStateAuthorityLeaves;
        root.AddComponent<LaboratoryNetworkAvatar>();
        PrefabUtility.SaveAsPrefabAsset(root, path);
        UnityEngine.Object.DestroyImmediate(root);
        AssetDatabase.SetLabels(AssetDatabase.LoadAssetAtPath<GameObject>(path), new[] { "FusionPrefab" });
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[LabNetwork] Network prefab created");
    }

    // Run in an isolated batch editor, outside Play: model/IK and prefab checks, no headset or cloud claim.
    [MenuItem("Tools/Lab/Verify Network Avatar")]
    public static void Verify()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play before running this check");
        CreatePrefab();
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != "Assets/Scenes/Main.unity")
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
                throw new Exception("Open Main first; current unsaved scene will not be replaced");
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        }
        var source = UnityEngine.Object.FindFirstObjectByType<LaboratoryLocalAvatar>();
        if (source == null) throw new Exception("Main has no local avatar");
        var remote = source.CreateRemote("Verification Remote");
        // Awake is a Play-mode callback; invoke explicitly for this edit-mode structural check.
        remote.SendMessage("Awake", SendMessageOptions.RequireReceiver);
        var e = new Pose(new Vector3(0, 1.65f, 2), Quaternion.Euler(0, 180, 0));
        remote.ShowRemote(true, e, null, null);
        if (remote.GetComponentsInChildren<SkinnedMeshRenderer>().Length != 1)
            throw new Exception("Remote model not instantiated");
        if (remote.GetComponent<LaboratoryAvatarSession>() != null || remote.GetComponentInChildren<Camera>() != null)
            throw new Exception("Remote incorrectly created input/session/camera");
        remote.ShowRemote(false, e, null, null);
        if (remote.GetComponentInChildren<SkinnedMeshRenderer>() != null)
            throw new Exception("Lost tracking did not hide remote body");
        UnityEngine.Object.DestroyImmediate(remote.gameObject);
        var prefab = Resources.Load<NetworkObject>(LaboratoryAvatarSession.PrefabPath);
        if (prefab == null || prefab.GetComponent<LaboratoryNetworkAvatar>() == null)
            throw new Exception("Network prefab missing components");
        Debug.Log("[LabNetwork] PASS remote model, loss hiding, no extra rig, network prefab. Dual-client and Quest tests pending.");
    }
}
