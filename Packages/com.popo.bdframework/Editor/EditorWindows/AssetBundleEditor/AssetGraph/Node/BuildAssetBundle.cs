﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using BDFramework.Core.Tools;
using BDFramework.Editor.AssetBundle;
using BDFramework.ResourceMgr;
using BDFramework.ResourceMgr.V2;
using LitJson;
using ServiceStack.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AssetGraph;
using UnityEngine.AssetGraph.DataModel.Version2;
using Debug = UnityEngine.Debug;

namespace BDFramework.Editor.AssetGraph.Node
{
    [CustomNode("BDFramework/打包AssetBundle", 100)]
    public class BuildAssetBundle : UnityEngine.AssetGraph.Node, IBDFrameowrkAssetEnvParams
    {
        public BuildInfo              BuildInfo   { get; set; }
        public BuildAssetBundleParams BuildParams { get; set; }

        public override string ActiveStyle
        {
            get { return "node 7 on"; }
        }

        public override string InactiveStyle
        {
            get { return "node 7"; }
        }

        public override string Category
        {
            get { return "打包AssetBundle"; }
        }

        public override void Initialize(NodeData data)
        {
            data.AddDefaultInputPoint();
            //data.AddDefaultOutputPoint();
        }

        public override UnityEngine.AssetGraph.Node Clone(NodeData newData)
        {
            return new BuildAssetBundle();
        }

        public override void OnInspectorGUI(NodeGUI node, AssetReferenceStreamManager streamManager, NodeGUIEditor editor, Action onValueChanged)
        {
        }


        public override void Prepare(BuildTarget target, NodeData nodeData, IEnumerable<PerformGraph.AssetGroups> incoming, IEnumerable<ConnectionData> connectionsToOutput, PerformGraph.Output outputFunc)
        {
            Debug.Log("执行Build AB Prepare");

            //这里只做临时的输出，预览用，不做实际更改
            BuildInfo tempBuildInfo = null;
            if (this.BuildInfo == null)
            {
                var json = JsonMapper.ToJson(BDFrameworkAssetsEnv.BuildInfo);
                tempBuildInfo = JsonMapper.ToObject<BuildInfo>(json);
            }

            if (this.BuildParams == null)
            {
                this.BuildParams = BDFrameworkAssetsEnv.BuildParams;
            }

            var platform = AssetBundleEditorToolsV2.GetRuntimePlatform(target);
            this.MergeABName(tempBuildInfo, BuildParams);
            var artConfig = this.GenArtConfig(tempBuildInfo, BuildParams, platform);
            
            //输出节点 预览
            var outMap = new Dictionary<string, List<AssetReference>>();
            
            
            
            var output = connectionsToOutput?.FirstOrDefault();
            if (output != null)
            {
                outputFunc(output, outMap);
            }
        }

        public override void Build(BuildTarget buildTarget, NodeData nodeData, IEnumerable<PerformGraph.AssetGroups> incoming, IEnumerable<ConnectionData> connectionsToOutput, PerformGraph.Output outputFunc, Action<NodeData, string, float> progressFunc)
        {
            Debug.Log("执行Build AB!");
            if (this.BuildInfo == null)
            {
                this.BuildInfo = BDFrameworkAssetsEnv.BuildInfo;
            }

            if (this.BuildParams == null)
            {
                this.BuildParams = BDFrameworkAssetsEnv.BuildParams;
            }

            //-----------------------开始打包AssetBundle逻辑---------------------------
            var platform = AssetBundleEditorToolsV2.GetRuntimePlatform(buildTarget);
            //1.整理abname
            this.MergeABName(BuildInfo, BuildParams);

            //2.生成artconfig
            var artConfig  = this.GenArtConfig(BuildInfo, BuildParams, platform);
            var outputPath = Path.Combine(BuildParams.OutputPath, BDApplication.GetPlatformPath(platform));
            var configPath = IPath.Combine(outputPath, BResources.ASSET_CONFIG_PATH);
            var csv        = CsvSerializer.SerializeToString(artConfig);
            FileHelper.WriteAllText(configPath, csv);

            //3.打包
            this.BuildAB(incoming, BuildInfo, BuildParams, platform);
        }


        static string RUNTIME_PATH = "/runtime/";

        /// <summary>
        /// 合并ABname
        /// </summary>
        private void MergeABName(BuildInfo buildInfo, BuildAssetBundleParams buildParams)
        {
            #region 整理依赖关系

            //1.把依赖资源替换成AB Name，
            foreach (var mainAsset in buildInfo.AssetDataMaps.Values)
            {
                for (int i = 0; i < mainAsset.DependAssetList.Count; i++)
                {
                    var dependAssetName = mainAsset.DependAssetList[i];
                    if (buildInfo.AssetDataMaps.TryGetValue(dependAssetName, out var dependAssetData))
                    {
                        //替换成真正AB名
                        if (!string.IsNullOrEmpty(dependAssetData.ABName))
                        {
                            mainAsset.DependAssetList[i] = dependAssetData.ABName;
                        }
                    }
                    else
                    {
                        Debug.Log("【AssetbundleV2】资源整理出错: " + dependAssetName);
                    }
                }

                //去重，移除自己
                mainAsset.DependAssetList = mainAsset.DependAssetList.Distinct().ToList();
                mainAsset.DependAssetList.Remove(mainAsset.ABName);
            }

            //2.按规则纠正ab名
            if (buildParams.IsUseHashName)
            {
                //使用guid 作为ab名
                foreach (var mainAsset in buildInfo.AssetDataMaps)
                {
                    var guid = AssetDatabase.AssetPathToGUID(mainAsset.Value.ABName);
                    if (!string.IsNullOrEmpty(guid)) //不存在的资源（如ab.shader之类）,则用原名
                    {
                        mainAsset.Value.ABName = guid;
                    }
                    else
                    {
                        Debug.LogError("获取GUID失败：" + mainAsset.Value.ABName);
                    }

                    for (int i = 0; i < mainAsset.Value.DependAssetList.Count; i++)
                    {
                        var dependAssetName = mainAsset.Value.DependAssetList[i];

                        guid = AssetDatabase.AssetPathToGUID(dependAssetName);
                        if (!string.IsNullOrEmpty(guid))
                        {
                            mainAsset.Value.DependAssetList[i] = guid;
                        }
                        else
                        {
                            Debug.LogError("获取GUID失败：" + dependAssetName);
                        }
                    }
                }
            }
            else
            {
                //2.整理runtime路径 替换路径名为Resource规则的名字
                // 非Hash命名时，runtime目录的都放在一起，方便调试
                foreach (var assetData in buildInfo.AssetDataMaps)
                {
                    if (assetData.Key.Contains(RUNTIME_PATH))
                    {
                        var newName = assetData.Value.ABName;
                        //移除runtime之前的路径、后缀
                        var index = newName.IndexOf(RUNTIME_PATH);
                        newName = newName.Substring(index + 1); //runtimeStr.Length);

                        var extension = Path.GetExtension(newName);
                        if (!string.IsNullOrEmpty(extension))
                        {
                            newName = newName.Replace(extension, "");
                        }

                        //设置新ab名，并且引用到自己的也会被修改
                        buildInfo.SetABName(assetData.Key, newName, BuildInfo.SetABNameMode.ForceAndFixAllRef);
                    }
                }
            }

            #endregion
        }

        /// <summary>
        ///生成Runtime下的Art.Config
        /// </summary>
        private List<AssetBundleItem> GenArtConfig(BuildInfo buildInfo, BuildAssetBundleParams buildParams, RuntimePlatform platform)
        {
            //根据buildinfo 生成加载用的 Config
            //1.导出配置
            var artConfig = new List<AssetBundleItem>();
            //占位，让id和idx恒相等
            artConfig.Add(new AssetBundleItem(0, null, null, AssetBundleItem.AssetTypeEnum.Others, new List<int>()));
            //先搜集非runtime的
            foreach (var item in buildInfo.AssetDataMaps)
            {
                //非runtime的只需要被索引AB
                if (!item.Key.Contains(RUNTIME_PATH))
                {
                    var ret = artConfig.FirstOrDefault((ab) => ab.AssetBundlePath == item.Value.ABName);
                    if (ret == null) //不保存重复内容
                    {
                        var mi = new AssetBundleItem(artConfig.Count, null, item.Value.ABName, (AssetBundleItem.AssetTypeEnum)item.Value.Type, new List<int>());
                        artConfig.Add(mi);
                    }
                }
            }

            //再搜集runtime的
            foreach (var item in buildInfo.AssetDataMaps)
            {
                //runtime路径下，写入配置
                if (item.Key.Contains(RUNTIME_PATH))
                {
                    var key = item.Key;
                    //移除runtime之前的路径、后缀
                    var index = key.IndexOf(RUNTIME_PATH);
                    if (buildParams.IsUseHashName)
                    {
                        key = key.Substring(index + RUNTIME_PATH.Length); //hash要去掉runtime
                    }
                    else
                    {
                        key = key.Substring(index + 1); // 保留runtime
                    }

                    var exten = Path.GetExtension(key);
                    if (!string.IsNullOrEmpty(exten))
                    {
                        key = key.Replace(exten, "");
                    }

                    //添加
                    var mi = new AssetBundleItem(artConfig.Count, key, item.Value.ABName, (AssetBundleItem.AssetTypeEnum)item.Value.Type, new List<int>());
                    artConfig.Add(mi); //.ManifestMap[key] = mi;
                }
            }

            //将depend替换成Id
            foreach (var assetbundleData in artConfig)
            {
                if (!string.IsNullOrEmpty(assetbundleData.LoadPath))
                {
                    //dependAsset 替换成ID
                    var buildAssetData = buildInfo.AssetDataMaps.Values.FirstOrDefault((asset) => asset.ABName == assetbundleData.AssetBundlePath);
                    for (int i = 0; i < buildAssetData.DependAssetList.Count; i++)
                    {
                        var dependAssetName = buildAssetData.DependAssetList[i];
                        //寻找保存列表中依赖的id（可以认为是下标）
                        var dependAssetBuildData = artConfig.FirstOrDefault((asset) => asset.AssetBundlePath == dependAssetName);
                        assetbundleData.DependAssetIds.Add(dependAssetBuildData.Id);
                    }
                }
            }

            //检查同名文件
            foreach (var abi in artConfig)
            {
                if (string.IsNullOrEmpty(abi.LoadPath))
                {
                    continue;
                }

                var result = artConfig.FindAll((ab) => ab.LoadPath == abi.LoadPath);
                if (result.Count > 1)
                {
                    Debug.LogError("【AssetbundleV2】有同名文件(不包含后缀)，加载存在不确定性，请修改! -" + abi.LoadPath);
                }
            }

            return artConfig;
        }


        /// <summary>
        /// 打包Asset Bundle
        /// </summary>
        /// <param name="buildInfo"></param>
        /// <param name="buildParams"></param>
        private void BuildAB(IEnumerable<PerformGraph.AssetGroups> incomingAssets, BuildInfo buildInfo, BuildAssetBundleParams buildParams, RuntimePlatform platform)
        {
            //----------------------------开始设置build ab name-------------------------------
            //根据传进来的资源,设置AB name
            foreach (var incoming in incomingAssets)
            {
                foreach (var ag in incoming.assetGroups)
                {
                    foreach (var assetReference in ag.Value)
                    {
                        var assetPath = assetReference.importFrom;
                        var assetData = buildInfo.AssetDataMaps[assetPath];
                        //设置ab name
                        var ai = GetAssetImporter(assetPath);
                        if (ai)
                        {
                            ai.assetBundleName = assetData.ABName;
                        }
                    }
                }
            }

            //----------------------------生成AssetBundle-------------------------------
            var    platformOutputPath = Path.Combine(buildParams.OutputPath, BDApplication.GetPlatformPath(platform));
            string artOutputPath      = IPath.Combine(platformOutputPath, BResources.ASSET_ROOT_PATH);
            try
            {
                AssetDatabase.RemoveUnusedAssetBundleNames();
                if (!Directory.Exists(artOutputPath))
                {
                    Directory.CreateDirectory(artOutputPath);
                }

                var buildTarget = AssetBundleEditorToolsV2.GetBuildTarget(platform);
                var buildOpa    = BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.DeterministicAssetBundle;
                BuildPipeline.BuildAssetBundles(artOutputPath, buildOpa, buildTarget);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                throw;
            }

            //----------------------------清理-------------------------------------
            RemoveAllAssetbundleName();
            AssetImpoterCacheMap.Clear();
            var delFiles = Directory.GetFiles(artOutputPath, "*", SearchOption.AllDirectories);
            foreach (var df in delFiles)
            {
                var ext = Path.GetExtension(df);
                if (ext == ".meta" || ext == ".manifest")
                {
                    File.Delete(df);
                }
            }

            //BuildInfo配置处理
            var buildinfoPath = IPath.Combine(platformOutputPath, BResources.ASSET_BUILD_INFO_PATH);
            //移动老配置
            if (File.Exists(buildinfoPath))
            {
                string oldBuildInfoPath = Path.Combine(platformOutputPath, BResources.ASSET_OLD_BUILD_INFO_PATH);
                File.Delete(oldBuildInfoPath);
                File.Move(buildinfoPath, oldBuildInfoPath);
            }

            //缓存buildinfo
            var json = JsonMapper.ToJson(buildInfo, true);
            FileHelper.WriteAllText(buildinfoPath, json);
            //BD生命周期触发
            BDEditorBehaviorHelper.OnEndBuildAssetBundle(platformOutputPath);
            AssetHelper.AssetHelper.GenPackageBuildInfo(buildParams.OutputPath, platform);
        }

        #region asset缓存、辅助等

        /// <summary>
        /// 资源导入缓存
        /// </summary>
        static private Dictionary<string, AssetImporter> AssetImpoterCacheMap = new Dictionary<string, AssetImporter>();

        /// <summary>
        /// 获取assetimpoter
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        static private AssetImporter GetAssetImporter(string path)
        {
            AssetImporter ai = null;
            if (!AssetImpoterCacheMap.TryGetValue(path, out ai))
            {
                ai                         = AssetImporter.GetAtPath(path);
                AssetImpoterCacheMap[path] = ai;
                if (!ai)
                {
                    Debug.LogError("【打包】获取资源失败:" + path);
                }
            }

            return ai;
        }

        /// <summary>
        /// 移除无效资源
        /// </summary>
        public static void RemoveAllAssetbundleName()
        {
            EditorUtility.DisplayProgressBar("资源清理", "清理中...", 1);

            foreach (var ai in AssetImpoterCacheMap)
            {
                if (ai.Value != null)
                {
                    if (!string.IsNullOrEmpty(ai.Value.assetBundleVariant))
                    {
                        ai.Value.assetBundleVariant = null;
                    }

                    ai.Value.assetBundleName = null;
                }
            }

            EditorUtility.ClearProgressBar();
        }

        #endregion
    }
}