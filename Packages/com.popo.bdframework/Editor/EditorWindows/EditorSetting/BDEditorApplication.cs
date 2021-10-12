using System.IO;
using System.Net.Mime;
using BDFramework.Core.Tools;
using JetBrains.Annotations;
using LitJson;
using UnityEditor;
using UnityEngine;

namespace BDFramework.Editor
{
    /// <summary>
    /// 编辑器下application的帮助
    /// </summary>
    static public class BDEditorApplication
    {
        /// <summary>
        /// 编辑器设置
        /// </summary>
        static public BDEditorSetting BdFrameEditorSetting { get; private set; }

        /// <summary>
        /// Runtime的config
        /// </summary>
        static public BDFrameConfig BDFrameConfig { get; private set; }

        /// <summary>
        /// 初始化
        /// </summary>
        static public void Init()
        {
            BdFrameEditorSetting = BDEditorSetting.Load();
            BDFrameConfig = BDFrameConfig.Load();
        }


       
        /// <summary>
        /// 获取最近修改的热更代码
        /// </summary>
        static public string[] GetLeastHotfixCodes()
        {
            return BDAssetImporter.CacheData?.HotfixList.ToArray();
        }
        
        /// <summary>
        /// 平台资源的父路径
        /// </summary>
        public static string GetPlatformPath(BuildTarget buildTarget)
        {
            switch (buildTarget)
            {
                case BuildTarget.Android:
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    return "Android";
                case BuildTarget.iOS:
                case BuildTarget.StandaloneOSX:
                    return "iOS";
            }

            return "";
        }
    }
}