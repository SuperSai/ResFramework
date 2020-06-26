using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjMgr : Singleton<ObjMgr>
{
    #region 类对象池的使用
    /// <summary>
    /// 类对象池字典    key:类型    value:类对象
    /// </summary>
    protected Dictionary<Type, object> _classPoolDic = new Dictionary<Type, object>();

    /// <summary>
    /// 创建类对象池
    /// </summary>
    public ClassObjPool<T> GetOrCreateClassPool<T>(int maxCount) where T : class, new()
    {
        Type type = typeof(T);
        object outObj = null;
        //不存在类对象池字典中的操作
        if (!_classPoolDic.TryGetValue(type, out outObj) || outObj == null)
        {
            ClassObjPool<T> newPool = new ClassObjPool<T>(maxCount);
            _classPoolDic.Add(type, newPool);
            return newPool;
        }
        return outObj as ClassObjPool<T>;
    }

    #endregion
}
