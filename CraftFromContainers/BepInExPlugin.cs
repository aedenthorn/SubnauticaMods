using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace CraftFromContainers
{
    [BepInPlugin("aedenthorn.CraftFromContainers", "Craft From Containers", "0.2.0")]
    public partial class BepInExPlugin : BaseUnityPlugin
    {
        private static BepInExPlugin context;

        public static ConfigEntry<bool> modEnabled;
        public static ConfigEntry<bool> isDebug;
        public static ConfigEntry<float> range;
        private static Dictionary<Transform, ItemsContainer> cachedContainers = new Dictionary<Transform, ItemsContainer>();

        private static float timeElapsed;

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
                    if (Vector3.Distance(Player.main.transform.position, key.position) <= range.Value)
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
