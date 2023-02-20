using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace StorageSizeMod
{
    [BepInPlugin("aedenthorn.StorageSizeMod", "Storage Size Mod", "0.1.0")]
    public partial class BepInExPlugin : BaseUnityPlugin
    {
        private static BepInExPlugin context;

        public static ConfigEntry<bool> modEnabled;
        public static ConfigEntry<bool> isDebug;
        public static ConfigEntry<float> range;
        private static Dictionary<string, XY> containerTypes = new Dictionary<string, XY>();
        private static string fileName = "container_types.json";

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
            
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
            Dbgl("Plugin awake");

            containerTypes = GetContainerTypes();

        }

        private static Dictionary<string, XY> GetContainerTypes()
        {
            string path = Path.Combine(AedenthornUtils.GetAssetPath(context, true), fileName);
            if (!File.Exists(path))
            {
                return new Dictionary<string, XY>();
            }
            return JsonConvert.DeserializeObject<Dictionary<string, XY>>(File.ReadAllText(path));
        }
        
        private static void AddContainerType(string name, int x, int y)
        {
            string path = Path.Combine(AedenthornUtils.GetAssetPath(context, true), fileName);

            var dict = new Dictionary<string, XY>();
            if (File.Exists(path))
            {
                dict = JsonConvert.DeserializeObject<Dictionary<string, XY>>(File.ReadAllText(path));
            }
            if (!dict.ContainsKey(name))
            {
                dict.Add(name, new XY(x, y));
                File.WriteAllText(path, JsonConvert.SerializeObject(dict, Formatting.Indented));
                Dbgl($"Added type {name} ({x}x{y})");
            }
        }

        private static string GetStorageName(Component c)
        {
            Transform t = c.transform;
            while(t.parent != null && (t.name == "StorageRoot" || t.name == "StorageContainer" || t.name == "storage"))
            {
                t = t.parent;
            }
            var name = (t is null ? c.transform : t).name.Replace("(Clone)", "").Replace("StorageRoot", "");
            Dbgl($"Got storage name {name} for {c.gameObject.GetFullHierarchyPath()}");
            return name;
        }

        [HarmonyPatch(typeof(SeamothStorageContainer), nameof(SeamothStorageContainer.container))]
        [HarmonyPatch(MethodType.Setter)]
        private static class SeamothStorageContainer_container_Patch
        {
            static void Postfix(SeamothStorageContainer __instance, ItemsContainer value)
            {
                var name = GetStorageName(__instance.storageRoot);
                containerTypes = GetContainerTypes();
                if (containerTypes.TryGetValue(name, out var xy) && xy.custom)
                {
                    Dbgl($"setting size for {name} ({xy.width}x{xy.height})");
                    __instance.container.Resize(xy.width, xy.height);
                }
                else
                {
                    AddContainerType(GetStorageName(__instance.storageRoot), value.sizeX, value.sizeY);

                }
            }
        }

        [HarmonyPatch(typeof(StorageContainer), nameof(StorageContainer.container))]
        [HarmonyPatch(MethodType.Setter)]
        private static class StorageContainer_container_Patch
        {
            static void Postfix(StorageContainer __instance, ItemsContainer value)
            {
                var name = GetStorageName(__instance.storageRoot);
                containerTypes = GetContainerTypes();
                if (containerTypes.TryGetValue(name, out var xy) && xy.custom)
                {
                    Dbgl($"setting size for {name} ({xy.width}x{xy.height})");
                    __instance.container.Resize(xy.width, xy.height);
                }
                else
                {
                    AddContainerType(GetStorageName(__instance.storageRoot), value.sizeX, value.sizeY);

                }
            }
        }
        [HarmonyPatch(typeof(StorageContainer), nameof(StorageContainer.Resize))]
        private static class StorageContainer_Resize_Patch
        {
            static void Prefix(StorageContainer __instance, ref int width, ref int height)
            {
                var name = GetStorageName(__instance.storageRoot);
                containerTypes = GetContainerTypes();
                if (containerTypes.TryGetValue(name, out var xy) && xy.custom)
                {
                    width = xy.width;
                    height = xy.height;
                }
            }
        }
    }

}
