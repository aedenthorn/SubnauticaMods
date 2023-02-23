using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using static HandReticle;

namespace SeamothTopographicMap
{
    [BepInPlugin("aedenthorn.SeamothTopographicMap", "Seamoth Topographic Map", "0.1.1")]
    public partial class BepInExPlugin : BaseUnityPlugin
    {
        private static BepInExPlugin context;

        public static ConfigEntry<bool> modEnabled;
        public static ConfigEntry<bool> isDebug;
        public static ConfigEntry<Vector3> mapPosition;

        public static void Dbgl(string str = "", LogLevel logLevel = LogLevel.Debug)
        {
            if (isDebug.Value)
                context.Logger.Log(logLevel, str);
        }
        private void Awake()
        {

            context = this;
            modEnabled = Config.Bind<bool>("General", "Enabled", true, "Enable this mod");
            isDebug = Config.Bind<bool>("General", "IsDebug", true, "Enable debug logs");
            mapPosition = Config.Bind<Vector3>("Options", "MapPosition", new Vector3(0, -0.4f, 1), "Map position");
            
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
            Dbgl("Plugin awake");

        }


        [HarmonyPatch(typeof(SeaMoth), nameof(SeaMoth.Awake))]
        private static class SeaMoth_Awake_Patch
        {
            static void Postfix(SeaMoth __instance)
            {
                if (!modEnabled.Value)
                    return;

                __instance.StartCoroutine(CreateMap(__instance));


            }
        }

        private static IEnumerator CreateMap(SeaMoth __instance)
        {
            TaskResult<GameObject> result = new TaskResult<GameObject>();
            CoroutineTask<GameObject> request = CraftData.GetPrefabForTechTypeAsync(TechType.Seaglide);
            yield return request;
            GameObject seaglide = request.GetResult();
            if (seaglide is null)
            {
                Dbgl("Couldn't get seaglide prefab");
                yield break;
            }

            Dbgl("Adding map to seamoth");
            GameObject go = Instantiate(seaglide.transform.Find("MapHolder").gameObject, __instance.playerPosition.transform);
            go.name = "MapHolder";
            SeamothInterface_Map map = __instance.gameObject.AddComponent<SeamothInterface_Map>();
            map.mapHolder = go;
            map.seamoth = __instance;
            var vim = seaglide.GetComponent<VehicleInterface_MapController>();
            map.interfacePrefab = vim.interfacePrefab;
            map.playerDot = go.transform.Find("PlayerPing").gameObject;
            map.lightVfx = go.transform.Find("HoloFX").gameObject;
            map.mapSpawnPos = go.transform.Find("PlayerPing/Ping");
        }
    }

}
