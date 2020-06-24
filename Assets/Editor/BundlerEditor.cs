using System.Runtime.Serialization.Formatters.Binary;
using System.Xml.Serialization;
using System.Reflection;
using System.IO;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class BundlerEditor
{

    private static string _bundleTargetPath = Application.streamingAssetsPath;
    //asset序列地址
    private static string ABCONIFG_PATH = "Assets/Editor/ABConfig.asset";
    //key:AB包名字  value:路径
    private static Dictionary<string, string> _allFileDir = new Dictionary<string, string>();
    //过滤
    private static List<string> _allFileAB = new List<string>();
    //单个Prefab的AB包
    private static Dictionary<string, List<string>> _allPrefabDir = new Dictionary<string, List<string>>();
    //存储所有有效路径
    private static List<string> _configFil = new List<string>();


    [MenuItem("Tool/打包")]
    public static void Build()
    {
        _allFileAB.Clear();
        _allFileDir.Clear();
        _allPrefabDir.Clear();
        _configFil.Clear();
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
            _configFil.Add(fileDir.Path);
        }

        //获取路径下所有Prefab的GUID
        string[] allPath = AssetDatabase.FindAssets("t:Prefab", config._allPrefabPath.ToArray());
        for (int i = 0; i < allPath.Length; i++)
        {
            //找到对应Prefab的全地址
            string path = AssetDatabase.GUIDToAssetPath(allPath[i]);
            //显示进度条
            EditorUtility.DisplayProgressBar("查找Prefab", "Prefab:" + path, i * 1.0f / allPath.Length);
            _configFil.Add(path);
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

    /// <summary>
    /// 打包
    /// </summary>
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
                Debug.Log("此AB包：" + allBundles[i] + " 下面包含的资源文件路径：" + allBundlePath[j]);
                if (VaildPath(allBundlePath[j]))
                    resPathDic.Add(allBundlePath[j], allBundles[i]);
            }
        }

        DeleteAB();
        //生成AB包配置表
        WriteData(resPathDic);

        BuildPipeline.BuildAssetBundles(_bundleTargetPath, BuildAssetBundleOptions.ChunkBasedCompression, EditorUserBuildSettings.activeBuildTarget);
        Debug.Log("------------- 打包完成！");
    }

    /// <summary>
    /// 生成AB包配置表
    /// </summary>
    private static void WriteData(Dictionary<string, string> resPathDic)
    {
        AssetBundleConfig config = new AssetBundleConfig();
        config.ABList = new List<ABBase>();
        foreach (string path in resPathDic.Keys)
        {
            ABBase abBase = new ABBase();
            abBase.Path = path;
            abBase.Crc = CRC32.GetCRC32(path);
            abBase.ABName = resPathDic[path];
            abBase.AssetName = path.Remove(0, path.LastIndexOf("/") + 1);
            abBase.ABDependce = new List<string>();
            //获取当前AB包对应的依赖项
            string[] resDepende = AssetDatabase.GetDependencies(path);
            for (int i = 0; i < resDepende.Length; i++)
            {
                string tempPath = resDepende[i];
                if (tempPath == path || path.EndsWith(".cs"))
                    continue;
                string abName = "";
                if (resPathDic.TryGetValue(tempPath, out abName))
                {
                    if (abName == resPathDic[path])
                        continue;
                    if (!abBase.ABDependce.Contains(abName))
                        abBase.ABDependce.Add(abName);
                }
            }
            config.ABList.Add(abBase);
        }
        //写入XML
        string xmlPath = Application.dataPath + "/AssetBundleConfig.xml";
        if (File.Exists(xmlPath)) File.Delete(xmlPath);
        FileStream fileStream = new FileStream(xmlPath, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
        StreamWriter sw = new StreamWriter(fileStream, System.Text.Encoding.UTF8);
        XmlSerializer xs = new XmlSerializer(config.GetType());
        xs.Serialize(sw, config);
        sw.Close();
        fileStream.Close();
        //写入二进制
        foreach (ABBase abBase in config.ABList)
        {
            abBase.Path = "";
        }
        string bytePath = "Assets/GameData/Data/ABData/AssetBundleConfig.bytes";
        FileStream fs = new FileStream(bytePath, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
        BinaryFormatter bf = new BinaryFormatter();
        bf.Serialize(fs, config);
        fs.Close();
    }

    /// <summary>
    /// 删除多余的AB包
    /// </summary>
    private static void DeleteAB()
    {
        //获取所有AB包的名字
        string[] allBundlesName = AssetDatabase.GetAllAssetBundleNames();
        DirectoryInfo direction = new DirectoryInfo(_bundleTargetPath);
        FileInfo[] files = direction.GetFiles("*", SearchOption.AllDirectories);
        for (int i = 0; i < files.Length; i++)
        {
            //判断是否已经存在AB包中
            if (ContainABName(files[i].Name, allBundlesName) || files[i].Name.EndsWith(".meta"))
            {
                continue;
            }
            else
            {
                Debug.Log("此AB包已经被删或者改名了：" + files[i].Name);
                if (File.Exists(files[i].FullName))
                {
                    File.Delete(files[i].FullName);
                }
            }
        }
    }

    /// <summary>
    /// 遍历文件夹里的文件名与设置的所有的AB包进行检查判断
    /// </summary>
    private static bool ContainABName(string name, string[] strs)
    {
        for (int i = 0; i < strs.Length; i++)
        {
            if (name == strs[i])
                return true;
        }
        return false;
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

    /// <summary>
    /// 是否包含在已经有的AB包里，用来做冗余剔除
    /// </summary>
    private static bool ContainAllFileAB(string path)
    {
        for (int i = 0; i < _allFileAB.Count; i++)
        {
            if (path == _allFileAB[i] || (path.Contains(_allFileAB[i]) && (path.Replace(_allFileAB[i], "")[0] == '/')))
                return true;
        }
        return false;
    }

    /// <summary>
    /// 是否有效路径
    /// </summary>
    private static bool VaildPath(string path)
    {
        for (int i = 0; i < _configFil.Count; i++)
        {
            if (path.Contains(_configFil[i])) return true;
        }

        return false;
    }
}
