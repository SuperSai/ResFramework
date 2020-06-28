using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ResMgr : Singleton<ResMgr>
{

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