using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 类对象池
/// </summary>
public class ClassObjPool<T> where T : class, new()
{
    protected Stack<T> _pool = new Stack<T>();
    /// <summary>
    /// 最大对象个数，<=0 表示不限个数
    /// </summary>
    protected int _maxCount = 0;
    /// <summary>
    /// 没有回收的对象个数
    /// </summary>
    protected int _noRecycleCount = 0;

    public ClassObjPool(int maxCount)
    {
        _maxCount = maxCount;
        for (int i = 0; i < _maxCount; i++)
        {
            _pool.Push(new T());
        }
    }

    /// <summary>
    /// 从池里面取类对象
    /// </summary>
    /// <param name="createIfPoolEmpty">如果对象池是没有是否创建</param>
    /// <returns>T</returns>
    public T Spawn(bool createIfPoolEmpty = false)
    {
        if (_pool.Count > 0)
        {
            T c = _pool.Pop();
            if (c == null) if (createIfPoolEmpty) c = new T();
            _noRecycleCount++;
            return c;
        }
        else
        {
            if (createIfPoolEmpty)
            {
                T c = new T();
                _noRecycleCount++;
                return c;
            }
        }
        return null;
    }

    /// <summary>
    /// 回收类对象
    /// </summary>
    /// <param name="obj">需要被回收的类对象</param>
    /// <returns>是否回收成功</returns>
    public bool Recycle(T obj)
    {
        if (obj == null) return false;
        _noRecycleCount--;
        if (_pool.Count >= _maxCount && _maxCount > 0)
        {
            obj = null;
            return false;
        }
        _pool.Push(obj);
        return true;
    }
}
