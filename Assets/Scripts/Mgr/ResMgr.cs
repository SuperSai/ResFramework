using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ResMgr : Singleton<ResMgr>
{

    /// <summary>
    /// 缓存使用的资源列表
    /// </summary>
    public Dictionary<uint, ResourceItem> assetDic { get; set; } = new Dictionary<uint, ResourceItem>();
    /// <summary>
    /// 缓存引用计数为零的资源列表，达到缓存最大的时候释放这个列表里面最早没用的资源
    /// </summary>
    protected CMapList<ResourceItem> _noRefrenceAssetMapList = new CMapList<ResourceItem>();

    public bool _loadFromAssetBundle = false;

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

        if (item._obj != null) item._obj = null;
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