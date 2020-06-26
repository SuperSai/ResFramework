using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 单例基类
/// </summary>
public class Singleton<T> where T : new()
{
    private static T _ins;
    public static T Ins
    {
        get
        {
            if (_ins == null)
            {
                _ins = new T();
            }
            return _ins;
        }
    }
}
