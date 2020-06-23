using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ABConfig", menuName = "CreateABConfig", order = 0)]
public class ABConfig : ScriptableObject
{
    //单个文件所在文件夹路径，会遍历文件夹下面所有Prefab，所有的Prefab的名字不能重复
    public List<string> _allPrefabPath = new List<string>();
    public List<FileDirABName> _allFileDirAB = new List<FileDirABName>();

    [System.Serializable]
    public struct FileDirABName
    {
        public string ABName;
        public string Path;
    }
}

