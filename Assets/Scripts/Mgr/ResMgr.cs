using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 加载优先级
/// </summary>
public enum LoadResPriority
{
    RES_HIGHT = 0,//最高优先级
    RES_MIDDLE,//一般优先级
    RES_SLOW,//低优先级
    RES_NUM,
}

public class AsyncCallBack
{
    /// <summary>
    /// 加载完成的回调
    /// </summary>
    public OnAsyncObjFinish dealFinish = null;
    /// <summary>
    /// 回调参数
    /// </summary>
    public object param1 = null, param2 = null, param3 = null;
    public void Reset()
    {
        dealFinish = null;
        param1 = null;
        param2 = null;
        param3 = null;
    }
}

public class AsyncLoadResParam
{
    public List<AsyncCallBack> callBackList = new List<AsyncCallBack>();
    public uint crc;
    public string path;
    /// <summary>
    /// 是否是一张图片
    /// </summary>
    public bool isSprite = false;
    public LoadResPriority priority = LoadResPriority.RES_SLOW;

    public void Reset()
    {
        crc = 0;
        path = "";
        isSprite = false;
        priority = LoadResPriority.RES_SLOW;
        callBackList.Clear();
    }
}

public delegate void OnAsyncObjFinish(string path, Object obj, object param1 = null, object param2 = null, object param3 = null);

public class ResMgr : Singleton<ResMgr>
{
    public bool _loadFromAssetBundle = false;
    /// <summary>
    /// 缓存使用的资源列表
    /// </summary>
    public Dictionary<uint, ResourceItem> assetDic { get; set; } = new Dictionary<uint, ResourceItem>();
    /// <summary>
    /// 缓存引用计数为零的资源列表，达到缓存最大的时候释放这个列表里面最早没用的资源
    /// </summary>
    protected CMapList<ResourceItem> _noRefrenceAssetMapList = new CMapList<ResourceItem>();
    /// <summary>
    /// 中间类，回调类的类对象池
    /// </summary>
    protected ClassObjPool<AsyncLoadResParam> _asyncLoadResParamPool = new ClassObjPool<AsyncLoadResParam>(50);
    protected ClassObjPool<AsyncCallBack> _asyncCallBackPool = new ClassObjPool<AsyncCallBack>(100);

    //Mono脚本
    protected MonoBehaviour _startMono;
    //正在异步加载的资源列表
    protected List<AsyncLoadResParam>[] _loadingAssetList = new List<AsyncLoadResParam>[(int)LoadResPriority.RES_NUM];
    //正在异步加载的Dic
    protected Dictionary<uint, AsyncLoadResParam> _loadingAssetDic = new Dictionary<uint, AsyncLoadResParam>();
    /// <summary>
    /// 最長连续卡着加载资源的时间，单位微秒
    /// </summary>
    private const long MAX_LOAD_RES_TIME = 200000;


    public void Init(MonoBehaviour mono)
    {
        for (int i = 0; i < (int)LoadResPriority.RES_NUM; i++)
        {
            _loadingAssetList[i] = new List<AsyncLoadResParam>();
        }
        _startMono = mono;
        //启动协程
        _startMono.StartCoroutine(AsyncLoader());
    }

    /// <summary>
    /// 同步资源加载，外部直接调用，仅加载不需要实例化的资源，例如:Texture，音频等等
    /// </summary>
    public T LoadResource<T>(string path) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(path)) return null;
        uint crc = CRC32.GetCRC32(path);
        ResourceItem item = GetCancheResourceItem(crc);
        if (item != null) return item._obj as T;

        T obj = null;
#if UNITY_EDITOR
        if (!_loadFromAssetBundle)
        {
            item = ABMgr.Ins.FingResourceItem(crc);
            if (item._obj != null)
            {
                obj = item._obj as T;
            }
            else
            {
                obj = LoadAssetByEditor<T>(path);
            }
        }
#endif
        if (obj == null)
        {
            item = ABMgr.Ins.LoadResAssetBundle(crc);
            if (item != null && item._assetBundle != null)
            {
                if (item._obj != null)
                {
                    obj = item._obj as T;
                }
                else
                {
                    obj = item._assetBundle.LoadAsset<T>(item._assetName);
                }
            }
        }

        CacheResource(path, ref item, crc, obj);
        return obj;
    }

    /// <summary>
    /// 不需要实例化的资源卸载
    /// </summary>
    /// <returns>bool</returns>
    public bool ReleaseResouce(Object obj, bool destoryObj = false)
    {
        if (obj == null) return false;
        ResourceItem item = null;
        foreach (ResourceItem res in assetDic.Values)
        {
            if (res._guid == obj.GetInstanceID())
            {
                item = res;
            }
        }

        if (item == null)
        {
            Debug.LogError("assetDic里不存在该资源：" + obj.name + " 可能释放了多次！");
            return false;
        }
        item.RefCount--;
        DestoryResourceItem(item, destoryObj);
        return true;
    }

    /// <summary>
    /// 缓存加载的资源
    /// </summary>
    private void CacheResource(string path, ref ResourceItem item, uint crc, Object obj, int addRefCount = 1)
    {
        //缓存太多，清除最早没有使用的资源
        WashOut();

        if (item == null)
        {
            Debug.LogError("ResourceItem is null, path:" + path);
        }

        if (obj == null)
        {
            Debug.LogError("ResourceLoad Fail :" + path);
        }
        item._obj = obj;
        item._guid = obj.GetInstanceID();
        item._lastUseTime = Time.realtimeSinceStartup;
        item.RefCount += addRefCount;
        ResourceItem oldItem = null;
        if (assetDic.TryGetValue(crc, out oldItem))
        {
            assetDic[item._crc] = item;
        }
        else
        {
            assetDic.Add(item._crc, item);
        }
    }

    /// <summary>
    /// 当当前内存使用大于80%的时候，进行清除最早没用的资源
    /// </summary>
    protected void WashOut()
    {
        // {
        //     if (_noRefrenceAssetMapList.Size() <= 0)
        //         break;
        //     ResourceItem item = _noRefrenceAssetMapList.BackNode();
        //     DestoryResourceItem(item, true);
        //     _noRefrenceAssetMapList.Pop();
        // }
    }

    /// <summary>
    /// 回收一个资源
    /// </summary>
    protected void DestoryResourceItem(ResourceItem item, bool destroyCache = false)
    {
        if (item == null || item.RefCount > 0) return;

        if (!assetDic.Remove(item._crc)) return;

        if (!destroyCache)
        {
            _noRefrenceAssetMapList.InsertToHead(item);
            return;
        }
        //释放AB引用
        ABMgr.Ins.ReleaseAsset(item);

        if (item._obj != null)
        {
            item._obj = null;
#if UNITY_EDITOR
            Resources.UnloadUnusedAssets();
#endif
        }

    }

#if UNITY_EDITOR
    protected T LoadAssetByEditor<T>(string path) where T : UnityEngine.Object
    {
        return UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
    }
#endif

    private ResourceItem GetCancheResourceItem(uint crc, int addRefCount = 1)
    {
        ResourceItem item = null;
        if (assetDic.TryGetValue(crc, out item) && item != null)
        {
            item.RefCount += addRefCount;
            item._lastUseTime = Time.realtimeSinceStartup;
            //容错处理，按道理是不会进来的
            if (item.RefCount <= 1)
            {
                _noRefrenceAssetMapList.RemoveNode(item);
            }
        }
        return item;
    }

    /// <summary>
    /// 异步加载资源,仅仅是不需要实例化的资源，例如音频，图片等等
    /// </summary>
    public void AsyncLoadResource(string path, OnAsyncObjFinish dealFinish, LoadResPriority priority, uint crc = 0, object param1 = null, object param2 = null, object param3 = null)
    {
        if (crc == 0)
        {
            crc = CRC32.GetCRC32(path);
        }
        ResourceItem item = GetCancheResourceItem(crc);
        if (item != null)
        {
            if (dealFinish != null)
            {
                dealFinish(path, item._obj, param1, param2, param3);
            }
            return;
        }
        //判断是否在加载中
        AsyncLoadResParam param = null;
        if (!_loadingAssetDic.TryGetValue(crc, out param) || param == null)
        {
            param = _asyncLoadResParamPool.Spawn(true);
            param.crc = crc;
            param.path = path;
            param.priority = priority;
            _loadingAssetDic.Add(crc, param);
            _loadingAssetList[(int)priority].Add(param);
        }

        //往回调列表里面加回调
        AsyncCallBack callBack = _asyncCallBackPool.Spawn(true);
        callBack.dealFinish = dealFinish;
        callBack.param1 = param1;
        callBack.param2 = param2;
        callBack.param3 = param3;
        param.callBackList.Add(callBack);
    }

    /// <summary>
    /// 异步加载
    /// </summary>
    IEnumerator AsyncLoader()
    {
        List<AsyncCallBack> callBackList = null;
        //上一次yield的时间
        long lastYiledTime = System.DateTime.Now.Ticks;
        while (true)
        {
            bool haveYield = false;
            for (int i = 0; i < (int)LoadResPriority.RES_NUM; i++)
            {
                List<AsyncLoadResParam> loadingList = _loadingAssetList[i];
                if (loadingList.Count <= 0) continue;
                AsyncLoadResParam loadingItem = loadingList[0];
                loadingList.RemoveAt(0);
                callBackList = loadingItem.callBackList;

                Object obj = null;
                ResourceItem item = null;
#if UNITY_EDITOR
                if (!_loadFromAssetBundle)
                {
                    obj = LoadAssetByEditor<Object>(loadingItem.path);
                    //模拟异步加载
                    yield return new WaitForSeconds(0.5f);

                    item = ABMgr.Ins.FingResourceItem(loadingItem.crc);
                }
#endif
                if (obj == null)
                {
                    item = ABMgr.Ins.LoadResAssetBundle(loadingItem.crc);
                    if (item != null && item._assetBundle != null)
                    {
                        AssetBundleRequest abRequest = null;
                        if (loadingItem.isSprite)
                        {
                            abRequest = item._assetBundle.LoadAssetAsync<Sprite>(item._assetName);
                        }
                        else
                        {
                            abRequest = item._assetBundle.LoadAssetAsync(item._assetName);
                        }
                        yield return abRequest;
                        if (abRequest.isDone)
                        {
                            obj = abRequest.asset;
                        }
                        lastYiledTime = System.DateTime.Now.Ticks;
                    }
                }

                CacheResource(loadingItem.path, ref item, loadingItem.crc, obj, callBackList.Count);

                for (int j = 0; j < callBackList.Count; j++)
                {
                    AsyncCallBack callBack = callBackList[i];
                    if (callBack != null && callBack.dealFinish != null)
                    {
                        callBack.dealFinish(loadingItem.path, obj, callBack.param1, callBack.param2, callBack.param3);
                        callBack.dealFinish = null;
                    }
                    callBack.Reset();
                    _asyncCallBackPool.Recycle(callBack);
                }

                obj = null;
                callBackList.Clear();
                _loadingAssetDic.Remove(loadingItem.crc);
                loadingItem.Reset();
                _asyncLoadResParamPool.Recycle(loadingItem);

                if (System.DateTime.Now.Ticks - lastYiledTime > MAX_LOAD_RES_TIME)
                {
                    yield return null;
                    lastYiledTime = System.DateTime.Now.Ticks;
                    haveYield = true;
                }
            }
            if (!haveYield || System.DateTime.Now.Ticks - lastYiledTime > MAX_LOAD_RES_TIME)
            {
                lastYiledTime = System.DateTime.Now.Ticks;
                yield return null;
            }
        }
    }

}

/// <summary>
/// 双向链表结构节点
/// </summary>
public class DoubleLinkedListNode<T> where T : class, new()
{
    /// <summary>
    /// 前一个节点
    /// </summary>
    public DoubleLinkedListNode<T> prev = null;
    /// <summary>
    /// 后一个节点
    /// </summary>
    public DoubleLinkedListNode<T> next = null;
    /// <summary>
    /// 当前节点
    /// </summary>
    public T curr = null;
}

/// <summary>
/// 双向链表结构列表
/// </summary>
public class DoubleLinkedList<T> where T : class, new()
{
    /// <summary>
    /// 表头
    /// </summary>
    public DoubleLinkedListNode<T> head = null;
    /// <summary>
    /// 表尾
    /// </summary>
    public DoubleLinkedListNode<T> tail = null;
    /// <summary>
    /// 双向链表结构类对象池
    /// </summary>
    /// <returns></returns>
    protected ClassObjPool<DoubleLinkedListNode<T>> _doubleLinkNodePool = ObjMgr.Ins.GetOrCreateClassPool<DoubleLinkedListNode<T>>(500);
    protected int _count = 0;
    /// <summary>
    /// 个数
    /// </summary>
    public int Count { get { return _count; } }

    /// <summary>
    /// 添加一个节点到头部
    /// </summary>
    /// <returns>DoubleLinkedListNode</returns>
    public DoubleLinkedListNode<T> AddToHeader(T t)
    {
        DoubleLinkedListNode<T> node = _doubleLinkNodePool.Spawn(true);
        node.prev = node.next = null;
        node.curr = t;
        return AddToHeader(node);
    }

    /// <summary>
    /// 添加一个节点到头部
    /// </summary>
    /// <returns>DoubleLinkedListNode</returns>
    public DoubleLinkedListNode<T> AddToHeader(DoubleLinkedListNode<T> node)
    {
        if (node == null) return null;
        node.prev = null;
        if (head == null)
        {
            head = tail = node;
        }
        else
        {
            node.next = head;
            head.prev = node;
            head = node;
        }
        _count++;
        return head;
    }

    /// <summary>
    /// 添加一个节点到尾部
    /// </summary>
    /// <returns>DoubleLinkedListNode</returns>
    public DoubleLinkedListNode<T> AddToTail(T t)
    {
        DoubleLinkedListNode<T> node = _doubleLinkNodePool.Spawn(true);
        node.prev = node.next = null;
        node.curr = t;
        return AddToTail(node);
    }

    /// <summary>
    /// 添加一个节点到尾部
    /// </summary>
    /// <returns>DoubleLinkedListNode</returns>
    public DoubleLinkedListNode<T> AddToTail(DoubleLinkedListNode<T> node)
    {
        if (node == null) return null;
        node.next = null;
        if (tail == null)
        {
            head = tail = node;
        }
        else
        {
            node.prev = tail;
            tail.next = node;
            tail = node;
        }
        _count++;
        return tail;
    }

    /// <summary>
    /// 把某个节点移动到头部
    /// </summary>
    public void MoveToHead(DoubleLinkedListNode<T> node)
    {
        if (node == null || node == head) return;
        if (node.prev == null && node.next == null) return;
        if (node == tail) tail = node.prev;
        if (node.prev != null) node.prev.next = node.next;
        if (node.next != null) node.next.prev = node.prev;
        node.prev = null;
        node.next = head;
        head.prev = node;
        head = node;
        if (tail == null) tail = head;
    }

    /// <summary>
    /// 移除节点
    /// </summary>
    public void RemoveNode(DoubleLinkedListNode<T> node)
    {
        if (node == null) return;
        if (node == head) head = node.next;
        if (node == tail) tail = node.prev;
        if (node.prev != null) node.prev.next = node.next;
        if (node.next != null) node.next.prev = node.prev;
        node.next = node.prev = null;
        node.curr = null;
        _doubleLinkNodePool.Recycle(node);
        _count--;
    }
}

public class CMapList<T> where T : class, new()
{
    DoubleLinkedList<T> _dlink = new DoubleLinkedList<T>();
    Dictionary<T, DoubleLinkedListNode<T>> _findMap = new Dictionary<T, DoubleLinkedListNode<T>>();

    //虚构函数
    ~CMapList()
    {
        Clear();
    }

    /// <summary>
    /// 清空列表
    /// </summary>    
    public void Clear()
    {
        while (_dlink.tail != null)
        {
            RemoveNode(_dlink.tail.curr);
        }
    }


    /// <summary>
    /// 插入一个节点到表头
    /// </summary>
    public void InsertToHead(T t)
    {
        DoubleLinkedListNode<T> node = null;
        if (_findMap.TryGetValue(t, out node) && node != null)
        {
            _dlink.AddToHeader(node);
            return;
        }
        _dlink.AddToHeader(t);
        _findMap.Add(t, _dlink.head);
    }

    /// <summary>
    /// 从表尾弹出一个节点
    /// </summary>
    public void Pop()
    {
        if (_dlink.tail != null)
        {
            RemoveNode(_dlink.tail.curr);
        }
    }

    /// <summary>
    /// 删除某个节点
    /// </summary>
    public void RemoveNode(T t)
    {
        DoubleLinkedListNode<T> node = null;
        if (!_findMap.TryGetValue(t, out node) || node == null) return;
        _dlink.RemoveNode(node);
        _findMap.Remove(t);
    }

    /// <summary>
    /// 获取尾部节点
    /// </summary>
    /// <returns></returns>
    public T BackNode()
    {
        return _dlink.tail == null ? null : _dlink.tail.curr;
    }

    /// <summary>
    /// 返回节点个数
    /// </summary>
    /// <returns></returns>
    public int Size()
    {
        return _findMap.Count;
    }

    /// <summary>
    /// 查找是否存在该节点
    /// </summary>
    /// <returns>bool</returns>
    public bool FindNode(T t)
    {
        DoubleLinkedListNode<T> node = null;
        if (!_findMap.TryGetValue(t, out node) || node == null) return false;
        return true;
    }

    /// <summary>
    /// 刷新某个节点，把节点移动到头部
    /// </summary>
    /// <returns>bool</returns>
    public bool Refresh(T t)
    {
        DoubleLinkedListNode<T> node = null;
        if (!_findMap.TryGetValue(t, out node) || node == null) return false;

        _dlink.MoveToHead(node);
        return true;
    }
}