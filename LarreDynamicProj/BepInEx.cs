using HarmonyLib;
using System;
using UnityEngine;

namespace LarreDynamic
{
    #region BepInEx
    [BepInEx.BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    public class LarreDynamicPlugin : BepInEx.BaseUnityPlugin
    {
        public const string pluginGuid = "com.larre.larredynamic";
        public const string pluginName = "LarreDynamic";
        public const string pluginVersion = "1.0.0";
        public static void Log(string line)
        {
            Debug.Log("[" + pluginName + "]: " + line);
        }
        void Awake()
        {
            try
            {
                var harmony = new Harmony(pluginGuid);
                harmony.PatchAll();
                Log("Patch succeeded");

            }
            catch (Exception e)
            {

                Log("Patch Failed");
                Log(e.ToString());
            }
        }
    }
    #endregion
}
