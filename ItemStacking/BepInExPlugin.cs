using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UIElements;
using static HandReticle;
using static VFXParticlesPool;

namespace ItemStacking
{
    [BepInPlugin("aedenthorn.ItemStacking", "ItemStacking", "0.1.0")]
    public partial class BepInExPlugin : BaseUnityPlugin
    {
        private static BepInExPlugin context;

        public static ConfigEntry<bool> modEnabled;
        public static ConfigEntry<bool> isDebug;

        public static ConfigEntry<KeyCode> splitStackKey;
        public static ConfigEntry<KeyCode> takeOneKey;
        public static ConfigEntry<int> labelSize;
        public static ConfigEntry<int> maxStack;

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
            splitStackKey = Config.Bind<KeyCode>("Options", "HotKeyStore", KeyCode.Mouse1, "Key to press to take half a stack.");
            takeOneKey = Config.Bind<KeyCode>("Options", "HotKeyMod", KeyCode.Mouse2, "Key to press to take one from stack.");
            labelSize = Config.Bind<int>("Options", "LabelSize", 20, "Width and height of count label in pixels.");
            maxStack = Config.Bind<int>("Options", "MaxStack", 99, "Max stack amount.");


            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), Info.Metadata.GUID);
            Dbgl("Plugin awake");

        }

        private void Update()
        {
            if (!modEnabled.Value || Player.main is null)
                return;
        }


        [HarmonyPatch(typeof(uGUI_ItemsContainer), "OnAddItem")]
        private static class uGUI_ItemsContainer_OnAddItem_Patch
        {
            static void Postfix(uGUI_ItemsContainer __instance, InventoryItem item)
            {
                OnAddItem(__instance, item);

            }

            private static void OnAddItem(uGUI_IIconManager __instance, InventoryItem item)
            {

                if (!modEnabled.Value)
                    return;
                var icons = (Dictionary<uGUI_ItemIcon, InventoryItem>)AccessTools.Field(__instance.GetType(), "icons").GetValue(__instance);
                var matches = icons.Where(kvp => kvp.Value == item);
                if (matches?.Count() <= 0 || !(item is InventoryStack) || matches.ElementAt(0).Key.transform.parent == null)
                {
                    return;
                }

                var labelGO = matches.ElementAt(0).Key.transform.Find("ItemCount")?.gameObject;
                TextMeshPro tmp;
                if (labelGO == null)
                {
                    labelGO = new GameObject("ItemCount");
                    labelGO.transform.SetParent(matches.ElementAt(0).Key.transform, false);
                    var rt = labelGO.AddComponent<RectTransform>();
                    rt.sizeDelta = new Vector2(labelSize.Value, labelSize.Value);
                    tmp = labelGO.AddComponent<TextMeshPro>();
                }
                else
                {
                    tmp = labelGO.GetComponent<TextMeshPro>();
                    tmp.fontSize = labelSize.Value;
                    tmp.color = Color.white;
                }
                tmp.text = (item as InventoryStack).Count.ToString();
                Dbgl($"added label to uGUI_ItemIcon of {tmp.text} {item.techType}");
            }
        }
        [HarmonyPatch(typeof(ItemsContainer), nameof(ItemsContainer.UnsafeAdd))]
        private static class ItemsContainer_UnsafeAdd_Patch
        {
            static void Prefix(ItemsContainer __instance, ref InventoryItem item)
            {

                if (!modEnabled.Value || CraftData.GetEquipmentType(item.techType) != EquipmentType.None)
                    return;
                int count = item is InventoryStack ? (item as InventoryStack).Count : 1;

                var itemGroups = AccessTools.Field(typeof(ItemsContainer), "_items").GetValue(__instance) as ICollection;

                foreach (var group in itemGroups) 
                {
                    var key = (TechType)AccessTools.Property(group.GetType(), "Key").GetValue(group);
                    var value = AccessTools.Property(group.GetType(), "Value").GetValue(group);
                    if (key == item.techType){
                        var items = (List<InventoryItem>)AccessTools.Field(value.GetType(), "items").GetValue(value);
                        if (items?.Count > 0)
                        {

                            for (int i = items.Count - 1; i >= 0; i--)
                            {
                                if (items[i] == null)
                                    continue;

                                int totalCount = count;
                                totalCount += (items[i] is InventoryStack) ? (items[i] as InventoryStack).Count : 1;
                                if (totalCount <= 1 || totalCount > maxStack.Value)
                                    continue;
                                InventoryStack newStack = new InventoryStack(item, totalCount, item.width, item.height);
                                item = newStack;
                                items.RemoveAt(i);
                                Dbgl($"combined new item stack of {totalCount} {newStack.techType}");
                                return;
                            }
                        }
                    }
                }
            }
        }
    }
}
