using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using UnityEngine;

namespace CraftFromRawMaterials
{
    [BepInPlugin("aedenthorn.CraftFromRawMaterials", "Craft From Raw Materials", "0.3.1")]
    public partial class BepInExPlugin : BaseUnityPlugin
    {
        private static BepInExPlugin context;

        public static ConfigEntry<bool> modEnabled;
        public static ConfigEntry<bool> isDebug;
        public static ConfigEntry<bool> rawByDefault;
        public static ConfigEntry<KeyboardShortcut> modKey;
        public static ConfigEntry<KeyboardShortcut> modKeyIgnore;

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
            rawByDefault = Config.Bind<bool>("Options", "RawByDefault", false, "Reverse ModKey behaviour");
            modKey = Config.Bind<KeyboardShortcut>("Options", "ModKey", new KeyboardShortcut(KeyCode.LeftShift), "Key to hold to enable mod");
            modKeyIgnore = Config.Bind<KeyboardShortcut>("Options", "ModKeyIgnore", new KeyboardShortcut(KeyCode.LeftControl), "Key to hold to ignore products you have in inventory");

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
            Dbgl("Plugin awake");

        }

        [HarmonyPatch(typeof(uGUI_CraftingMenu), "UpdateVisuals")]
        private static class uGUI_CraftingMenu_UpdateVisuals_Patch
        {
            static void Prefix(uGUI_CraftingMenu __instance, ref bool ___isDirty)
            {
                if(!modEnabled.Value || (!modKey.Value.IsDown() && !modKeyIgnore.Value.IsDown() && !modKey.Value.IsUp() && !modKeyIgnore.Value.IsUp()))
                    return;
                ___isDirty = true;
            }
        }
        [HarmonyPatch(typeof(BuilderTool), "OnHover", new Type[] { typeof(Constructable) })]
        private static class BuilderTool_OnHover_Patch
        {
            static void Prefix(Constructable constructable)
            {
                if (!modEnabled.Value || (!modKey.Value.IsPressed() && !modKey.Value.IsUp()))
                    return;
                AccessTools.FieldRefAccess<Constructable, List<TechType>>(constructable, "resourceMap") = null;
            }
        }

        [HarmonyPatch(typeof(TechData), nameof(TechData.GetIngredients))]
        private static class TechData_GetIngredients_Patch
        {
            static void Postfix(ref ReadOnlyCollection<Ingredient> __result)
            {
                if (!modEnabled.Value || (modKey.Value.IsPressed() == rawByDefault.Value && !modKeyIgnore.Value.IsPressed()) || __result is null || __result.Count == 0)
                    return;
                var ingList = GetIngredients(__result, 1);
                List<Ingredient> ingredients = new List<Ingredient>();
                foreach (var ing in ingList)
                {
                    for(int i = 0; i < ingredients.Count; i++)
                    {
                        if (ingredients[i].techType == ing.techType)
                        {
                            ingredients[i] = new Ingredient(ingredients[i].techType, ingredients[i].amount + ing.amount);

                            goto cont;
                        }
                    }
                    ingredients.Add(new Ingredient(ing.techType, ing.amount));
                cont:
                    continue;
                }
                __result = new ReadOnlyCollection<Ingredient>(ingredients);
            }

        }

        private static List<Ingredient> GetIngredients(ReadOnlyCollection<Ingredient> __result, int mult)
        {
            List<Ingredient> ingredients = new List<Ingredient>();
            for (int i = 0; i < __result.Count; i++)
            {
                var ing = new Ingredient(__result[i].techType, __result[i].amount);
                if (ing.techType == TechType.None)
                    continue;
                if (!modKeyIgnore.Value.IsPressed())
                {
                    if (Inventory.main.GetPickupCount(ing.techType) >= ing.amount * mult)
                    {
                        ing = new Ingredient(ing.techType, ing.amount * mult);
                        ingredients.Add(ing);
                        continue;
                    }
                }
                var inging = TechData.GetIngredients(ing.techType);
                if (inging != null && inging.Count > 0)
                {
                    ingredients.AddRange(GetIngredients(inging, Mathf.CeilToInt(ing.amount / (float)TechData.GetCraftAmount(ing.techType))));
                }
                else
                {
                    ing = new Ingredient(ing.techType, ing.amount * mult);
                    ingredients.Add(ing);
                }
            }
            return ingredients;
        }
    }
}
