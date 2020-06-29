using System.Runtime.Serialization.Formatters.Binary;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class ABMgr : Singleton<ABMgr>
{

    /// <summary>
    /// 资源关系依赖配置表，可以根据crc来找到对应资源块
    /// </summary>
    protected Dictionary<uint, ResourceItem> _resItemDic = new Dictionary<uint, ResourceItem>();
    /// <summary>
    /// 储存已加载的AB包    key:crc     value:AssetBundleItem
    /// </summary>
    protected Dictionary<uint, AssetBundleItem> _assetBundleItemDic = new Dictionary<uint, AssetBundleItem>();
    /// <summary>
    /// AssetBundleItem类对象池
    /// </summary>
    protected ClassObjPool<AssetBundleItem> _assetBundleItemPool = ObjMgr.Ins.GetOrCreateClassPool<AssetBundleItem>(500);

    /// <summary>
    /// 加载AB配置表
    /// </summary>
    /// <returns>是否加载成功</returns>
    public bool LoadAssetBundleConfig()
    {
        _resItemDic.Clear();
        string configPath = Application.streamingAssetsPath + "/assetbundleconfig";
        AssetBundle configAB = AssetBundle.LoadFromFile(configPath);
        TextAsset textAsset = configAB.LoadAsset<TextAsset>("assetbundleconfig");
        if (textAsset == null)
        {
            Debug.LogError("AssetBundleConfig is noe exist!");
            return false;
        }
        //反序列化
        MemoryStream stream = new MemoryStream(textAsset.bytes);
        BinaryFormatter bf = new BinaryFormatter();
        AssetBundleConfig config = bf.Deserialize(stream) as AssetBundleConfig;
        stream.Close();

        for (int i = 0; i < config.ABList.Count; i++)
        {
            ABBase abBase = config.ABList[i];
            ResourceItem item = new ResourceItem();
            item._crc = abBase.Crc;
            item._assetName = abBase.AssetName;
            item._abName = abBase.ABName;
            item._dependAssetBundle = abBase.ABDependce;
            if (_resItemDic.ContainsKey(item._crc))
            {
                Debug.LogError("重复的CRC 资源名：" + item._assetName + " AB包名：" + item._abName);
            }
            else
            {
                _resItemDic.Add(item._crc, item);
            }
        }

        return true;
    }

    /// <summary>
    /// 根据路径的crc加载中间类ResourceItem
    /// </summary>
    /// <returns>ResourceItem</returns>
    public ResourceItem LoadResAssetBundle(uint crc)
    {
        ResourceItem item = null;
        if (!_resItemDic.TryGetValue(crc, out item) || item == null)
        {
            Debug.LogError(string.Format("LoadResAssetBundle error：can not fing crc {0} in AssetBundleConfig", crc.ToString()));
            return item;
        }
        if (item._assetBundle != null) return item;
        item._assetBundle = LoadAssetBundle(item._abName);

        if (item._dependAssetBundle != null)
        {
            for (int i = 0; i < item._dependAssetBundle.Count; i++)
            {
                LoadAssetBundle(item._dependAssetBundle[i]);
            }
        }
        return item;
    }

    /// <summary>
    /// 加载单个assetbundle根据名字
    /// </summary>
    /// <returns>AssetBundle</returns>
    private AssetBundle LoadAssetBundle(string name)
    {
        AssetBundleItem item = null;
        uint crc = CRC32.GetCRC32(name);

        if (!_assetBundleItemDic.TryGetValue(crc, out item))
        {
            AssetBundle assetBundle = null;
            string fullPath = Application.streamingAssetsPath + "/" + name;
            if (File.Exists(fullPath))
            {
                assetBundle = AssetBundle.LoadFromFile(fullPath);
            }

            if (assetBundle == null)
            {
                Debug.LogError("Load AssetBundle Error Path:" + fullPath);
            }

            item = _assetBundleItemPool.Spawn(true);
            item.assetbundle = assetBundle;
            item.refCount++;
            _assetBundleItemDic.Add(crc, item);
        }
        else
        {
            item.refCount++;
        }
        return item.assetbundle;
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void ReleaseAsset(ResourceItem item)
    {
        if (item == null) return;
        if (item._dependAssetBundle != null && item._dependAssetBundle.Count > 0)
        {
            for (int i = 0; i < item._dependAssetBundle.Count; i++)
            {
                UnLoadAssetBundle(item._dependAssetBundle[i]);
            }
        }
        UnLoadAssetBundle(item._abName);
    }

    /// <summary>
    /// 卸载AssetBundle
    /// </summary>
    private void UnLoadAssetBundle(string name)
    {
        AssetBundleItem item = null;
        uint crc = CRC32.GetCRC32(name);
        if (_assetBundleItemDic.TryGetValue(crc, out item) && item != null)
        {
            item.refCount--;
            if (item.refCount <= 0 && item.assetbundle != null)
            {
                item.assetbundle.Unload(true);
                item.Reset();
                _assetBundleItemPool.Recycle(item);
                _assetBundleItemDic.Remove(crc);
            }
        }
    }

    /// <summary>
    /// 根据crc查找ResourceItem
    /// </summary>
    /// <returns>ResourceItem</returns>
    public ResourceItem FingResourceItem(uint crc)
    {
        return _resItemDic[crc];
    }
}

public class AssetBundleItem
{
    public AssetBundle assetbundle = null;
    /// <summary>
    /// 引用计数
    /// </summary>
    public int refCount;

    /// <summary>
    /// 重置
    /// </summary>
    public void Reset()
    {
        assetbundle = null;
        refCount = 0;
    }
}

public class ResourceItem
{
    /// <summary>
    /// 资源路径的CRC
    /// </summary>
    public uint _crc = 0;
    /// <summary>
    /// 资源文件名
    /// </summary>
    public string _assetName = string.Empty;
    /// <summary>
    /// 资源所在的AssetBundle名字
    /// </summary>
    public string _abName = string.Empty;
    /// <summary>
    /// 资源所依赖的AssetBundle
    /// </summary>
    public List<string> _dependAssetBundle = null;
    /// <summary>
    /// 资源加载完的AB包
    /// </summary>
    public AssetBundle _assetBundle = null;
    //******************************* 下面是资源相关 ***********************************
    /// <summary>
    /// 资源对象
    /// </summary>
    public Object _obj = null;
    /// <summary>
    /// 资源唯一标识
    /// </summary>
    public int _guid = 0;
    /// <summary>
    /// 资源最后使用的时间
    /// </summary>
    public float _lastUseTime = 0.0f;
    /// <summary>
    /// 引用计数
    /// </summary>
    protected int _refCount = 0;
    /// <summary>
    /// 引用计数
    /// </summary>
    public int RefCount
    {
        get { return _refCount; }
        set
        {
            _refCount = value;
            if (_refCount < 0)
            {
                Debug.LogError("ReCount < 0 :" + _refCount + " , " + (_obj != null ? _obj.name : "name is null"));
            }
        }
    }
}
