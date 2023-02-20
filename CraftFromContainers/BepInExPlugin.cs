using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using static VFXParticlesPool;
using Component = UnityEngine.Component;

namespace CraftFromContainers
{
    [BepInPlugin("aedenthorn.CraftFromContainers", "Craft From Containers", "0.1.0")]
    public partial class BepInExPlugin : BaseUnityPlugin
    {
        private static BepInExPlugin context;

        public static ConfigEntry<bool> modEnabled;
        public static ConfigEntry<bool> isDebug;
        public static ConfigEntry<float> range;
        private static Dictionary<Transform, ItemsContainer> cachedContainers = new Dictionary<Transform, ItemsContainer>();

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
            range = Config.Bind<float>("Options", "Range", 100f, "Range (m)");

            
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
            Dbgl("Plugin awake");

        }
        [HarmonyPatch(typeof(SeamothStorageContainer), nameof(SeamothStorageContainer.container))]
        [HarmonyPatch(MethodType.Setter)]
        private static class SeamothStorageContainer_container_Patch
        {
            static void Postfix(SeamothStorageContainer __instance, ItemsContainer value)
            {
                if (!modEnabled.Value)
                    return;
                cachedContainers.Add(__instance.transform, value);
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
                cachedContainers.Add(__instance.transform, value);
            }
        }
        
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.GetPickupCount))]
        private static class Inventory_GetPickupCount_Patch
        {
            static void Postfix(Inventory __instance, TechType pickupType, ref int __result)
            {
                if (!modEnabled.Value || __instance != Inventory.main)
                    return;

                foreach (var key in cachedContainers.Keys.ToArray())
                {
                    try
                    {
                        if (Vector3.Distance(Player.main.transform.position, key.position) <= range.Value)
                        {
                            __result += cachedContainers[key].GetCount(pickupType);
                        }
                    }
                    catch
                    {
                        cachedContainers.Remove(key);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.DestroyItem))]
        private static class Inventory_DestroyItem_Patch
        {
            private static void Postfix(Inventory __instance, TechType destroyTechType, ref bool __result)
            {
                if (__result || __instance != Inventory.main)
                    return;
                foreach (var key in cachedContainers.Keys.ToArray())
                {
                    try
                    {
                        if (Vector3.Distance(Player.main.transform.position, key.position) <= range.Value)
                        {
                            if (cachedContainers[key].DestroyItem(destroyTechType))
                            {
                                __result = true;
                                return;
                            }
                        }
                    }
                    catch
                    {
                        cachedContainers.Remove(key);
                    }
                }
            }
        }
    }
}
