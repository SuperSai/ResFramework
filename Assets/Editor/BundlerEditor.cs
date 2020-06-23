using System.ComponentModel.DataAnnotations.Schema;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class BundlerEditor
{

    //asset序列地址
    public static string ABCONIFG_PATH = "Assets/Editor/ABConfig.asset";
    //key:AB包名字  value:路径
    public static Dictionary<string, string> _allFileDir = new Dictionary<string, string>();
    //过滤
    public static List<string> _allFileAB = new List<string>();
    //单个Prefab的AB包
    public static Dictionary<string, List<string>> _allPrefabDir = new Dictionary<string, List<string>>();

    [MenuItem("Tool/打包")]
    public static void Build()
    {
        _allFileAB.Clear();
        _allFileDir.Clear();
        ABConfig config = AssetDatabase.LoadAssetAtPath<ABConfig>(ABCONIFG_PATH);
        foreach (ABConfig.FileDirABName fileDir in config._allFileDirAB)
        {
            if (_allFileDir.ContainsKey(fileDir.ABName))
            {
                Debug.LogError("AB包配置名字重复，请检查！");
                continue;
            }
            _allFileDir.Add(fileDir.ABName, fileDir.Path);
            _allFileAB.Add(fileDir.Path);
        }

        //获取路径下所有Prefab的GUID
        string[] allPath = AssetDatabase.FindAssets("t:Prefab", config._allPrefabPath.ToArray());
        for (int i = 0; i < allPath.Length; i++)
        {
            //找到对应Prefab的全地址
            string path = AssetDatabase.GUIDToAssetPath(allPath[i]);
            //显示进度条
            EditorUtility.DisplayProgressBar("查找Prefab", "Prefab:" + path, i * 1.0f / allPath.Length);
            if (!ContainAllFileAB(path))
            {
                GameObject obj = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                //获取所有的依赖项
                string[] allDepend = AssetDatabase.GetDependencies(path);
                List<string> allDependPath = new List<string>();
                for (int j = 0; j < allDepend.Length; j++)
                {
                    //过滤已经存在别的AB包中的依赖项和剔除cs代码
                    if (!ContainAllFileAB(allDepend[j]) && !allDepend[j].EndsWith(".cs"))
                    {
                        _allFileAB.Add(allDepend[j]);
                        allDependPath.Add(allDepend[j]);
                    }
                }
                if (_allPrefabDir.ContainsKey(obj.name))
                    Debug.LogError("存在相同名字的Prefab!   名字：" + obj.name);
                else
                    _allPrefabDir.Add(obj.name, allDependPath);
            }
        }

        foreach (string name in _allFileDir.Keys)
        {
            SetABName(name, _allFileDir[name]);
        }

        foreach (string name in _allPrefabDir.Keys)
        {
            SetABName(name, _allPrefabDir[name]);
        }

        BunildAssetBundle();

        //清除AB包名字，防止meta文件老是冲突
        string[] oldABName = AssetDatabase.GetAllAssetBundleNames();
        for (int i = 0; i < oldABName.Length; i++)
        {
            AssetDatabase.RemoveAssetBundleName(oldABName[i], true);
            EditorUtility.DisplayProgressBar("清除旧的AB包名", "AB包名字:" + oldABName[i], i * 1.0f / oldABName.Length);
        }

        AssetDatabase.Refresh();
        EditorUtility.ClearProgressBar();
        // BuildPipeline.BuildAssetBundles(Application.streamingAssetsPath, BuildAssetBundleOptions.ChunkBasedCompression, EditorUserBuildSettings.activeBuildTarget);
        // AssetDatabase.SaveAssets();
    }

    //打包
    private static void BunildAssetBundle()
    {
        //所有AB包名字
        string[] allBundles = AssetDatabase.GetAllAssetBundleNames();
        //key:全路径    value:包名
        Dictionary<string, string> resPathDic = new Dictionary<string, string>();
        for (int i = 0; i < allBundles.Length; i++)
        {
            //通过名字去获取路径
            string[] allBundlePath = AssetDatabase.GetAssetPathsFromAssetBundle(allBundles[i]);
            for (int j = 0; j < allBundlePath.Length; j++)
            {
                if (allBundlePath[j].EndsWith(".cs"))
                    continue;
                resPathDic.Add(allBundlePath[j], allBundles[i]);
            }
        }

        //生成AB包配置表

        BuildPipeline.BuildAssetBundles(Application.streamingAssetsPath, BuildAssetBundleOptions.ChunkBasedCompression, EditorUserBuildSettings.activeBuildTarget);
    }

    //设置AB包名字
    private static void SetABName(string name, string path)
    {
        AssetImporter assetImporter = AssetImporter.GetAtPath(path);
        if (assetImporter == null)
            Debug.LogError("不存在此路径文件：" + path);
        else
            assetImporter.assetBundleName = name;
    }

    //设置AB包名字
    private static void SetABName(string name, List<string> paths)
    {
        for (int i = 0; i < paths.Count; i++)
        {
            SetABName(name, paths[i]);
        }
    }

    //所有的Prefab路径是否已经在文件夹中包含了
    private static bool ContainAllFileAB(string path)
    {
        for (int i = 0; i < _allFileAB.Count; i++)
        {
            if (path == _allFileAB[i] || path.Contains(_allFileAB[i]))
                return true;
        }
        return false;
    }
}
