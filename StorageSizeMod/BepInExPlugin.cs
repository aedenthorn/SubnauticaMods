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
using UnityEngine.UI;

namespace StorageSizeMod
{
    [BepInPlugin("aedenthorn.StorageSizeMod", "Storage Size Mod", "0.3.0")]
    public partial class BepInExPlugin : BaseUnityPlugin
    {
        private static BepInExPlugin context;

        public static ConfigEntry<bool> modEnabled;
        public static ConfigEntry<bool> isDebug;
        public static ConfigEntry<float> overflowOffset;

        private static Dictionary<string, XY> containerTypes = new Dictionary<string, XY>();
        private static string fileName = "container_types.json";
        private static bool skip;

        private static RectTransform rts;
        private static RectTransform rtm;
        private static ScrollRect sr;

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
            overflowOffset = Config.Bind<float>("Options", "OverflowOffset", 20f, "Overflow offset to show part of the offscreen storage grid in UI");
            
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
            Dbgl("Plugin awake");

            containerTypes = GetContainerTypes();

        }

        private static Dictionary<string, XY> GetContainerTypes()
        {
            if (!modEnabled.Value)
                return new Dictionary<string, XY>();
            string path = Path.Combine(AedenthornUtils.GetAssetPath(context, true), fileName);
            if (!File.Exists(path))
            {
                return new Dictionary<string, XY>();
            }
            return JsonConvert.DeserializeObject<Dictionary<string, XY>>(File.ReadAllText(path));
        }
        
        private static void AddContainerType(string name, int x, int y)
        {
            if (!modEnabled.Value)
                return;

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
                if (!modEnabled.Value)
                    return;
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
                if (!modEnabled.Value)
                    return;

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
                if (!modEnabled.Value || skip)
                    return;

                var name = GetStorageName(__instance.storageRoot);
                containerTypes = GetContainerTypes();
                if (containerTypes.TryGetValue(name, out var xy) && xy.custom)
                {
                    width = xy.width;
                    height = xy.height;
                }
            }
        }
        [HarmonyPatch(typeof(Exosuit), "UpdateStorageSize")]
        private static class Exosuit_UpdateStorageSize_Patch
        {
            static bool Prefix(Exosuit __instance)
            {
                if (!modEnabled.Value)
                    return true;

                var name = GetStorageName(__instance.storageContainer.storageRoot);
                containerTypes = GetContainerTypes();
                if (containerTypes.TryGetValue(name, out var xy) && xy.custom)
                {
                    int upgrades = __instance.modules.GetCount(TechType.VehicleStorageModule);
                    skip = true;
                    __instance.storageContainer.Resize(xy.width, xy.height + upgrades);
                    skip = false;
                    return false;
                }
                return true;
            }
        }
        [HarmonyPatch(typeof(uGUI_ItemsContainer), nameof(uGUI_ItemsContainer.Init))]
        private static class uGUI_ItemsContainer_Init_Patch
        {
            public static void Postfix(uGUI_ItemsContainer __instance, ItemsContainer ___container)
            {
                if (!modEnabled.Value || __instance != __instance.inventory.storage)
                    return;
                RectTransform rtg = __instance.rectTransform;
                var cellSize = 71;
                var columns = Math.Min(___container.sizeX, 7);
                var containerSize = new Vector2(columns * cellSize + overflowOffset.Value, 10 * cellSize + overflowOffset.Value);
                var gridSize = new Vector2(___container.sizeX * cellSize, ___container.sizeY * cellSize);

                if ((containerSize.x < gridSize.x || containerSize.y < gridSize.y) && __instance.transform.parent.name != "Mask")
                {
                    Dbgl($"Adding scroll view");

                    GameObject scrollObject = new GameObject() { name = "StorageScrollView" };
                    scrollObject.transform.SetParent(__instance.transform.parent);
                    rts = scrollObject.AddComponent<RectTransform>();

                    GameObject mask = new GameObject { name = "Mask" };
                    mask.transform.SetParent(scrollObject.transform);
                    rtm = mask.AddComponent<RectTransform>();

                    __instance.transform.SetParent(mask.transform);

                    Texture2D tex = new Texture2D((int)Mathf.Ceil(rtm.rect.width), (int)Mathf.Ceil(rtm.rect.height));
                    Image image = mask.AddComponent<Image>();
                    image.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.zero);
                    Mask m = mask.AddComponent<Mask>();
                    m.showMaskGraphic = false;

                    sr = scrollObject.AddComponent<ScrollRect>();
                    sr.movementType = ScrollRect.MovementType.Clamped;
                    sr.horizontal = true;
                    sr.viewport = mask.GetComponent<RectTransform>();
                    sr.content = rtg;
                    sr.scrollSensitivity = 10;

                    Dbgl("Added scroll view");
                }
                rts.sizeDelta = containerSize;
                rts.localScale = new Vector3(1f, 1f, 1f);
                rts.localEulerAngles = Vector3.zero;

                rts.anchorMax = new Vector2(0.75f, 0.5f);
                rts.anchorMin = new Vector2(0.75f, 0.5f);
                rts.anchoredPosition3D = new Vector3(-cellSize / 4f, -cellSize, 0);

                rtm.anchoredPosition3D = Vector3.zero;
                rtm.sizeDelta = containerSize;
                rtm.localScale = new Vector3(1, 1, 1);
                rtm.localEulerAngles = Vector3.zero;

                rtg.anchoredPosition3D = Vector3.zero;
                rtg.anchorMax = new Vector2(0.5f, 0.5f);
                rtg.anchorMin = new Vector2(0.5f, 0.5f);
                rtg.sizeDelta = gridSize;
                rtg.localScale = new Vector3(1, 1, 1);
                rtg.localEulerAngles = Vector3.zero;
                sr.verticalNormalizedPosition = 1;
                sr.horizontalNormalizedPosition = 0;
            }
        }
    }

}
