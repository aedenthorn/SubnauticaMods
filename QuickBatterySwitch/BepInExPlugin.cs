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
using UnityEngine;

namespace QuickBatterySwitch
{
    [BepInPlugin("aedenthorn.QuickBatterySwitch", "QuickBatterySwitch", "0.1.0")]
    public partial class BepInExPlugin : BaseUnityPlugin
    {
        private static BepInExPlugin context;

        public static ConfigEntry<bool> modEnabled;
        public static ConfigEntry<bool> isDebug;

        public static ConfigEntry<float> range;
        public static ConfigEntry<KeyCode> modKeySwitch;
        public static ConfigEntry<KeyCode> modKeyRemove;
        public static ConfigEntry<string> textSwitch;
        public static ConfigEntry<string> textRemove;

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
            modKeySwitch = Config.Bind<KeyCode>("Options", "ModKeySwitch", KeyCode.LeftShift, "Key to hold to perform quick swap.");
            modKeyRemove = Config.Bind<KeyCode>("Options", "ModKeyRemove", KeyCode.LeftControl, "Key to hold to remove battery.");
            textSwitch = Config.Bind<string>("Options", "TextSwitch", "[Quick] {0}", "Text change when holding down switch mod key.");
            textRemove = Config.Bind<string>("Options", "TextRemove", "[Remove] {0}", "Text change when holding down remove mod key.");

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), Info.Metadata.GUID);
            Dbgl("Plugin awake");
            
        }

        [HarmonyPatch(typeof(HandReticle), nameof(HandReticle.SetTextRaw))]
        private static class HandReticle_SetTextRaw_Patch
        {
            static void Prefix(HandReticle __instance, HandReticle.TextType type, ref string text)
            {
                if (!modEnabled.Value || type != HandReticle.TextType.UseSubscript || (!Input.GetKey(modKeySwitch.Value) && !Input.GetKey(modKeyRemove.Value)) || text != (string)AccessTools.Field(typeof(GUIHand), "cachedEnergyHudText").GetValue(Player.main.guiHand))
                    return;
                if (Input.GetKey(modKeySwitch.Value))
                    text = string.Format(textSwitch.Value, text);
                else
                    text = string.Format(textRemove.Value, text);
            }
        }

        [HarmonyPatch(typeof(PlayerTool), nameof(PlayerTool.OnReloadDown))]
        private static class PlayerTool_OnReloadDown_Patch
        {
            static bool Prefix(PlayerTool __instance, ref EnergyMixin ___energyMixin, ref bool __result)
            {
                
                if (!modEnabled.Value || (!Input.GetKey(modKeySwitch.Value) && !Input.GetKey(modKeyRemove.Value)))
                    return true;
                if (___energyMixin == null)
                {
                    ___energyMixin = __instance.GetComponent<EnergyMixin>();
                }
                if (___energyMixin != null)
                {
                    if (___energyMixin.allowBatteryReplacement)
                    {
                        var batterySlot = AccessTools.FieldRefAccess<EnergyMixin, StorageSlot>(___energyMixin, "batterySlot");
                        InventoryItem storedItem = batterySlot.storedItem;
                        if (Input.GetKey(modKeyRemove.Value))
                        {
                            if (storedItem != null)
                            {
                                batterySlot.RemoveItem();
                                Inventory.main.ForcePickup(storedItem.item);
                            }
                        }
                        else
                        {
                            List<InventoryItem> items = new List<InventoryItem>();
                            foreach (InventoryItem item in Inventory.main.container)
                            {
                                if (___energyMixin.Filter(item))
                                {
                                    items.Add(item);
                                }
                            }
                            if (items.Count > 0)
                            {
                                items.Sort(delegate (InventoryItem a, InventoryItem b) {
                                    var ac = a?.item?.GetComponent<IBattery>()?.charge;
                                    var bc = b?.item?.GetComponent<IBattery>()?.charge;

                                    if (ac == null && bc == null)
                                        return 0;
                                    if (ac == null)
                                        return 1;
                                    if (bc == null)
                                        return -1;
                                    return b.item.GetComponent<IBattery>().charge.CompareTo(a.item.GetComponent<IBattery>().charge);
                                });
                                foreach (var i in items)
                                {
                                    Dbgl($"{i.item?.name}: {i.item?.GetComponent<IBattery>()?.charge}");
                                }
                                var swap = items[0];
                                if (swap.item?.GetComponent<IBattery>()?.charge != null)
                                {
                                    ___energyMixin.Select(swap);
                                }
                            }
                        }

                    }
                    __result = true;
                }
                return false;
            }
        }
    }
}
