using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameStart : MonoBehaviour
{

    private void Awake()
    {
        ABMgr.Ins.LoadAssetBundleConfig();
    }

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    /// <summary>
    /// Callback sent to all game objects before the application is quit.
    /// </summary>
    void OnApplicationQuit()
    {
#if UNITY_EDITOR
        ResMgr.Ins.ClearCache();
        Resources.UnloadUnusedAssets();
#endif
    }
}
