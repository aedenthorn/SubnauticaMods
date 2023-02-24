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

namespace CyclopsTopographicMap
{
    [BepInPlugin("aedenthorn.CyclopsTopographicMap", "Cyclops Topographic Map", "0.1.0")]
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
            mapPosition = Config.Bind<Vector3>("Options", "MapPosition", new Vector3(0, -0.3f, 0.65f), "Map position");

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
            Dbgl("Plugin awake");

        }


        [HarmonyPatch(typeof(PilotingChair), nameof(PilotingChair.Start))]
        private static class PilotingChair_Start_Patch
        {
            static void Postfix(PilotingChair __instance)
            {
                if (!modEnabled.Value)
                    return;

                __instance.StartCoroutine(CreateMap(__instance));


            }
        }

        private static IEnumerator CreateMap(PilotingChair __instance)
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

            Dbgl("Adding map to Cyclops");
            GameObject go = Instantiate(seaglide.transform.Find("MapHolder").gameObject, __instance.sittingPosition.transform);
            go.name = "MapHolder";
            go.transform.localPosition = mapPosition.Value;
            CyclopsInterface_Map map = __instance.gameObject.AddComponent<CyclopsInterface_Map>();
            map.mapHolder = go;
            map.chair = __instance;
            var vim = seaglide.GetComponent<VehicleInterface_MapController>();
            map.interfacePrefab = vim.interfacePrefab;
            map.playerDot = go.transform.Find("PlayerPing").gameObject;
            map.lightVfx = go.transform.Find("HoloFX").gameObject;
            map.mapSpawnPos = go.transform.Find("PlayerPing/Ping");
        }
    }

}
