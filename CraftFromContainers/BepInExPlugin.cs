using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace CraftFromContainers
{
    [BepInPlugin("aedenthorn.CraftFromContainers", "Craft From Containers", "0.3.2")]
    public partial class BepInExPlugin : BaseUnityPlugin
    {
        private static BepInExPlugin context;

        public static ConfigEntry<bool> modEnabled;
        public static ConfigEntry<bool> isDebug;
        public static ConfigEntry<float> range;

        private static float timeElapsed;

        private static Dictionary<Component, ItemsContainer> cachedContainers = new Dictionary<Component, ItemsContainer>();

        private static Dictionary<string, bool> containerTypes;
        private static string containerFile = "container_types_allowed.json";
        private static string containerPath;

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

            containerPath = Path.Combine(AedenthornUtils.GetAssetPath(this, true), containerFile);
            containerTypes = File.Exists(containerPath) ? JsonConvert.DeserializeObject<Dictionary<string, bool>>(File.ReadAllText(containerPath)) : new Dictionary<string, bool>();

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
            Dbgl("Plugin awake");

        }
        private void Update()
        {
            if(!modEnabled.Value || Inventory.main is null || uGUI.main?.pinnedRecipes is null)
                return;
            timeElapsed += Time.deltaTime;
            if(timeElapsed > 1)
            {
                timeElapsed = 0;
                AccessTools.FieldRefAccess<uGUI_PinnedRecipes, bool>(uGUI.main.pinnedRecipes, "ingredientsDirty") = true;
            }
        }

        [HarmonyPatch(typeof(SeamothStorageContainer), "Awake")]
        private static class SeamothStorageContainer_Awake_Patch
        {
            static void Postfix(SeamothStorageContainer __instance)
            {
                if (!modEnabled.Value)
                    return;
                CheckStorageType(__instance);
                cachedContainers[__instance.storageRoot] = __instance.container;
            }

        }

        [HarmonyPatch(typeof(StorageContainer), "Awake")]
        private static class StorageContainer_Awake_Patch
        {
            static void Postfix(StorageContainer __instance)
            {
                if (!modEnabled.Value)
                    return;
                CheckStorageType(__instance);
                cachedContainers[__instance.storageRoot] = __instance.container;
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
                        if (CheckStorageType(key) && Vector3.Distance(Player.main.transform.position, key.transform.position) <= range.Value)
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
        
        [HarmonyPatch(typeof(uGUI_RecipeEntry), nameof(uGUI_RecipeEntry.UpdateIngredients))]
        private static class uGUI_RecipeEntry_UpdateIngredients_Patch
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                Dbgl("transpiling uGUI_RecipeEntry.UpdateIngredients");

                var codes = new List<CodeInstruction>(instructions);
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Callvirt && codes[i].operand is MethodInfo && (MethodInfo)codes[i].operand == AccessTools.Method(typeof(ItemsContainer), nameof(ItemsContainer.GetCount)))
                    {
                        Dbgl("Found method");
                        codes[i].opcode = OpCodes.Call;
                        codes[i].operand = AccessTools.Method(typeof(BepInExPlugin), nameof(BepInExPlugin.GetCount));
                        break;
                    }
                }

                return codes.AsEnumerable();
            }
        }

        public static int GetCount(ItemsContainer container, TechType techType)
        {

            int total = container.GetCount(techType);
            if (!modEnabled.Value)
                return total;

            foreach (var key in cachedContainers.Keys.ToArray())
            {
                try
                {
                    if (CheckStorageType(key) && Vector3.Distance(Player.main.transform.position, key.transform.position) <= range.Value)
                    {
                        total += cachedContainers[key].GetCount(techType);
                    }
                }
                catch
                {
                    cachedContainers.Remove(key);
                }
            }
            return total;
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
                        if (CheckStorageType(key) && Vector3.Distance(Player.main.transform.position, key.transform.position) <= range.Value)
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
        private static string GetStorageName(Component c)
        {
            Transform t = c.transform;
            while (t.parent != null && (t.name == "StorageRoot" || t.name == "StorageContainer" || t.name == "storage"))
            {
                t = t.parent;
            }
            var name = (t is null ? c.transform : t).name.Replace("(Clone)", "").Replace("StorageRoot", "");
            return name;
        }
        private static bool CheckStorageType(Component c)
        {
            string name = GetStorageName(c);
            if (!containerTypes.TryGetValue(name, out bool allowed))
            {
                containerTypes[name] = true;
                File.WriteAllText(containerPath, JsonConvert.SerializeObject(containerTypes, Formatting.Indented));
                return true;
            }
            else return allowed;
        }
    }
}
