using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Xml.Serialization;

[System.Serializable]
public class AssetBundleConfig
{
    [XmlElement("ABList")]
    public List<ABBase> ABList { get; set; }
}

[System.Serializable]
public class ABBase
{
    //路径
    [XmlAttribute("Path")]
    public string Path { get; set; }
    //类似MD5，唯一标识
    [XmlAttribute("Crc")]
    public uint Crc { get; set; }
    //包名
    [XmlAttribute("ABName")]
    public string ABName { get; set; }
    //资源名
    [XmlAttribute("AssetName")]
    public string AssetName { get; set; }
    //AB包的依赖项
    [XmlElement("ABDependce")]
    public List<string> ABDependce { get; set; }
}
