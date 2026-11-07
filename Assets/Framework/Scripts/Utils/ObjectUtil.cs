using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class ObjectUtil
{
    /// <summary>
    /// 依据绝对路径获取组件
    /// </summary>
    /// <param name="path">子物体路径</param>
    /// <typeparam name="T">组件类型</typeparam>
    public static T GetComponent<T>(string path) where T : Object {
        GameObject obj = GameObject.Find(path);
        T comp = obj != null ? obj.GetComponent<T>() : null;

        if (comp == null)
            Debug.LogError($"Script ObjectUtil.cs: \"{path}\" is null");

        return comp;
    }

    /// <summary>
    /// 获取 trans 上的组件
    /// </summary>
    /// <param name="trans">物体 transform</param>
    /// <typeparam name="T">组件类型</typeparam>
    public static T GetComponent<T>(Transform trans) where T : Object {
        T comp = trans != null ? trans.GetComponent<T>() : null;

        if (comp == null)
            Debug.LogError($"Script ObjectUtil.cs: \"{(trans != null ? trans.name : "<null>")}\" has no component \"{typeof(T)}\"");

        return comp;
    }

    /// <summary>
    /// 从子物体中获取组件
    /// </summary>
    /// <param name="path">子物体路径</param>
    /// <param name="parent">父物体</param>
    /// <typeparam name="T">组件类型</typeparam>
    public static T GetComponentFromChild<T>(Transform parent, string path) where T : Object {
        Transform transPath = parent != null ? parent.Find(path) : null;
        
        if (transPath == null) {
            Debug.LogError($"Script ObjectUtil.cs: \"{(parent != null ? parent.name : "<null>")}: {path}\" is null");
            return null;
        }

        T comp = transPath.GetComponent<T>();

        if (comp == null)
            Debug.LogError($"Script ObjectUtil.cs: \"{transPath.name}\" has no component \"{typeof(T)}\"");
        
        return comp;
    }
}
