﻿using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System;
using BDFramework.Editor.Tools;

public class EditorWindow_GenAssetBundle : EditorWindow
{
    /// <summary>
    /// 资源下面根节点
    /// </summary>
    public string rootResourceDir = "Resource/Runtime/";
    private bool isSelectWindows  = true;
    private bool isSelectIOS      = false;
    private bool isSelectAndroid  = false;
    //
    void DrawToolsBar()
    {
        GUILayout.Label("平台选择:");
        GUILayout.BeginHorizontal();
        {
            GUILayout.Space(30);
            isSelectWindows = GUILayout.Toggle(isSelectWindows, "生成Windows资源");
        }
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        {
            GUILayout.Space(30);
            isSelectAndroid = GUILayout.Toggle(isSelectAndroid, "生成Android资源");
        }
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        {
            GUILayout.Space(30);
            isSelectIOS = GUILayout.Toggle(isSelectIOS, "生成IOS资源");
        }
        GUILayout.EndHorizontal();
    }

    public void OnGUI()
    {
        GUILayout.BeginVertical();
        TipsGUI();
        DrawToolsBar();
        GUILayout.Space(10);
        LastestGUI();
        GUILayout.EndVertical();
    }

    /// <summary>
    /// 提示UI
    /// </summary>
    void TipsGUI()
    {
        GUILayout.Label("2.资源打包",EditorGUIHelper.TitleStyle);
        GUILayout.Space(5);
        GUILayout.Label(string.Format("资源根目录:Assets/{0}", rootResourceDir));
        GUILayout.Label(string.Format("AB输出目录:Assets/Streamming/{0}", ""));
    }

    /// <summary>
    /// 最新包
    /// </summary>
    void LastestGUI()
    {
        GUILayout.BeginVertical();

        if (GUILayout.Button("检测资源", GUILayout.Width(100)))
        {
            var exportPath = EditorUtility.OpenFolderPanel("选择导出目录", Application.dataPath,"");
            if (isSelectWindows)
                AssetBundleEditorTools.CheackAssets(rootResourceDir,exportPath+"/Windows", BuildTarget.StandaloneWindows);
            if (isSelectAndroid)
                AssetBundleEditorTools.CheackAssets(rootResourceDir,exportPath+"/Android", BuildTarget.Android);
            if (isSelectIOS)
                AssetBundleEditorTools.CheackAssets(rootResourceDir,exportPath+"/iOS", BuildTarget.iOS);
            
            AssetDatabase.Refresh();
            Debug.Log("资源打包完毕"); 
        }
        
        if (GUILayout.Button("一键打包[美术资源]", GUILayout.Width(380), GUILayout.Height(30)))
        {
            var exportPath = EditorUtility.OpenFolderPanel("选择导出目录", Application.dataPath,"");
            //开始打包
            if (isSelectWindows)
                AssetBundleEditorTools.GenAssetBundle(rootResourceDir,exportPath+"/Windows", BuildTarget.StandaloneWindows);
            if (isSelectAndroid)
                AssetBundleEditorTools.GenAssetBundle(rootResourceDir,exportPath+"/Android", BuildTarget.Android);
            if (isSelectIOS)
                AssetBundleEditorTools.GenAssetBundle(rootResourceDir,exportPath+"/iOS", BuildTarget.iOS);
            
            AssetDatabase.Refresh();
            Debug.Log("资源打包完毕");
        }

        GUILayout.EndVertical();
    }
}