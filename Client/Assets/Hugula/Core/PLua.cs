// Copyright (c) 2015 hugula
// direct https://github.com/tenvick/hugula
//
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Text.RegularExpressions;
using System;
using System.Text;

using SLua;
using Lua = SLua.LuaSvr;
using Hugula.Utils;
using Hugula.Cryptograph;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hugula
{
    /// <summary>
    /// ¼ÓÔØºÍÔËÐÐlua
    /// </summary>
    [CustomLuaClass]
    public class PLua : MonoBehaviour
    {

        public static string enterLua = "main";
        public LuaFunction onDestroyFn;
        public LuaFunction onAppPauseFn;
        public LuaFunction onAppQuitFn;
        public LuaFunction onAppFocusFn;

#if UNITY_EDITOR
        const string KeyDebugString = "_Plua_Debug_string";
        [SLua.DoNotToLua]
        public static bool isDebug
        {
            get
            {
                bool _debug = EditorPrefs.GetBool(KeyDebugString, true);
                return _debug;
            }
            set
            {
                EditorPrefs.SetBool(KeyDebugString, value);
            }
        }
#endif

        public Lua lua;
        private static Dictionary<string, byte[]> luacache;

        #region priveta
        private string luaMain = "";

        private LuaFunction _updateFn;

        #endregion

        #region mono

        public LuaFunction updateFn
        {
            get { return _updateFn; }
            set
            {
                _updateFn = value;
            }
        }

        void Awake()
        {
            DontDestroyOnLoad(this.gameObject);
            luacache = new Dictionary<string, byte[]>();
            lua = new Lua();
            _instance = this;
            LoadScript();
        }

        void Start()
        {
            lua.init(null, () =>
                    { LoadBundle(true); });
        }

	    void Update()
	    {
	        if (_updateFn != null) _updateFn.call();
	    }

        void OnDestroy()
        {
            if (onDestroyFn != null) onDestroyFn.call();
            updateFn = null;
            lua = null;
            _instance = null;
            luacache = null;
        }

        void OnApplicationFocus(bool focusStatus)
        {
            if (onAppFocusFn != null) onAppFocusFn.call(this, focusStatus);
        }

        void OnApplicationPause(bool pauseStatus)
        {
            if (onAppPauseFn != null) onAppPauseFn.call(this, pauseStatus);
        }

        void OnApplicationQuit()
        {
            if (onAppQuitFn != null) onAppQuitFn.call(this, true);
        }
        #endregion

        private void SetLuaPath()
        {
            this.luaMain = "return require(\"" + enterLua + "\") \n";
            //        Debug.Log(luaMain);
        }

        private void LoadScript()
        {
            SetLuaPath();

            RegisterFunc();
#if UNITY_EDITOR
            Debug.LogFormat("<color=green>running {0} mode </color> <color=#8cacbc> change( menu Hugula->Debug Lua)</color>", isDebug ? "debug" : "release");
#endif
        }

        /// <summary>
        /// lua bundle
        /// </summary>
        /// <returns></returns>
        private IEnumerator loadLuaBundle(bool domain, LuaFunction onLoadedFn)
        {
            string keyName = "";
            string luaP = CUtils.GetAssetFullPath(Common.LUA_ASSETBUNDLE_FILENAME);
            WWW luaLoader = new WWW(luaP);
            yield return luaLoader;
            if (luaLoader.error == null)
            {
                byte[] byts = CryptographHelper.Decrypt(luaLoader.bytes, DESHelper.instance.Key, DESHelper.instance.IV);
#if UNITY_5_0 || UNITY_5_1 || UNITY_5_2
                AssetBundle item = AssetBundle.CreateFromMemoryImmediate(byts);
#else
				AssetBundle item = AssetBundle.LoadFromMemory(byts);
#endif

                TextAsset[] all = item.LoadAllAssets<TextAsset>();
                foreach (var ass in all)
                {
                    keyName = ass.name;
                    SetRequire(keyName, ass);
                }

                item.Unload(true);
                luaLoader.Dispose();
            }

            if (domain)
                DoMain();

            if (onLoadedFn != null) onLoadedFn.call();
        }

    #region public

        public void SetRequire(string key, Array bytes)
        {
            var bytearr = (byte[])bytes;
            luacache[key] = bytearr;
        }

        public void SetRequire(string key, TextAsset textAsset)
        {
            luacache[key] = textAsset.bytes;
        }

        /// <summary>
        /// lua begin
        /// </summary>
        public void DoMain()
        {
            lua.luaState.doString(this.luaMain);
        }

        /// <summary>
        /// load assetbundle
        /// </summary>
        public void LoadBundle(bool domain)
        {
#if UNITY_EDITOR
            if (!isDebug)
            {
                StopCoroutine(loadLuaBundle(domain, null));
                StartCoroutine(loadLuaBundle(domain, null));
            }
            else
            {
                if (domain)
                    DoMain();
            }
#else
			StopCoroutine(loadLuaBundle(domain, onLoadedFn));
			StartCoroutine(loadLuaBundle(domain, onLoadedFn));
#endif
        }

        /// <summary>
        /// load assetbundle
        /// </summary>
        public void LoadBundle(LuaFunction onLoadedFn)
        {
            StopCoroutine(loadLuaBundle(false, onLoadedFn));
            StartCoroutine(loadLuaBundle(false, onLoadedFn));
        }

        #endregion

        #region toolMethod

        public void RegisterFunc()
        {
            LuaState.loaderDelegate = Loader;
        }

        /// <summary>
        ///  loader
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static byte[] Loader(string name)
        {
            byte[] str = null;
#if UNITY_EDITOR

            if (isDebug)
            {
                string name1 = name.Replace('.', '/');
                string path = Application.dataPath + "/Lua/" + name1 + ".lua";
                if (!File.Exists(path))
                    path = Application.dataPath + "/Config/" + name1 + ".lua";

                if (File.Exists(path))
                {
                    str = File.ReadAllBytes(path);
                }
                else
                {
                    name = name.Replace('.', '_').Replace('/', '_');
                    if (luacache.ContainsKey(name))
                    {
                        var bytes = luacache[name];
                        str = bytes;
                    }
                }
            }
            else
            {
                name = name.Replace('.', '_').Replace('/', '_');
                if (luacache.ContainsKey(name))
                {
                    var bytes = luacache[name];
                    str = bytes;
                }
            }
#elif UNITY_STANDALONE

        name = name.Replace('.', '/');
        string path = Application.dataPath + "/config_data/" + name + ".lua"; //ÓÅÏÈ¶ÁÈ¡ÅäÖÃÎÄ¼þ
        if (File.Exists(path))
        {
            str = File.ReadAllBytes(path);
        }
        else
        {
            name = name.Replace('.', '_').Replace('/', '_');
            if (luacache.ContainsKey(name))
            {
                var bytes = luacache[name];
                str = bytes;
            }
        }

#else
		name = name.Replace('.','_').Replace('/','_'); 
        if(luacache.ContainsKey(name))
        {
	        var bytes = luacache[name];
	        str = bytes;
        }
#endif
            return str;
        }


        public static Coroutine Delay(LuaFunction luafun, float time, params object[] args)
        {
            return _instance.StartCoroutine(DelayDo(luafun, time, args));
        }

        public static void StopDelay(Coroutine coroutine)
        {
            if (coroutine != null)
                _instance.StopCoroutine(coroutine);
        }

        private static IEnumerator DelayDo(LuaFunction luafun, float time, params object[] args)
        {
            yield return new WaitForSeconds(time);
            luafun.call(args);
        }

        #endregion

        #region static
        private static PLua _instance;

        public static PLua instance
        {
            get
            {
                return _instance;
            }
        }
        #endregion

    }
}