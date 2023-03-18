using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace CraftFromRawMaterials
{
    [BepInPlugin("aedenthorn.CraftFromRawMaterials", "Craft From Raw Materials", "0.1.0")]
    public partial class BepInExPlugin : BaseUnityPlugin
    {
        private static BepInExPlugin context;

        public static ConfigEntry<bool> modEnabled;
        public static ConfigEntry<bool> isDebug;
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
            modKey = Config.Bind<KeyboardShortcut>("Options", "ModKey", new KeyboardShortcut(KeyCode.LeftShift), "Key to hold to enable mod");
            modKeyIgnore = Config.Bind<KeyboardShortcut>("Options", "ModKeyIgnore", new KeyboardShortcut(KeyCode.LeftControl), "Key to hold to ignore products you have in inventory");

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
            Dbgl("Plugin awake");

        }

        [HarmonyPatch(typeof(uGUI_CraftingMenu), "UpdateVisuals")]
        private static class uGUI_CraftingMenu_UpdateVisuals_Patch
        {
            static void Prefix(uGUI_CraftingMenu __instance, ref bool ___resync, ref bool ___isDirty)
            {
                if(!modEnabled.Value || (!modKey.Value.IsDown() && !modKeyIgnore.Value.IsDown() && !modKey.Value.IsUp() && !modKeyIgnore.Value.IsUp()))
                    return;
                ___resync = true;
            }
        }

        [HarmonyPatch(typeof(CraftData), nameof(CraftData.Get))]
        private static class CraftData_Get_Patch
        {
            static void Postfix(ref ITechData __result)
            {
                if (!modEnabled.Value || (!modKey.Value.IsPressed() && !modKeyIgnore.Value.IsPressed()) || __result is null || __result.ingredientCount == 0)
                    return;
                MyTechData data = new MyTechData(__result);
                var ingList = GetIngredients(__result, 1);
                List<MyIngredient> ingredients = new List<MyIngredient>();
                foreach (var ing in ingList)
                {
                    for(int i = 0; i < ingredients.Count; i++)
                    {
                        if (ingredients[i].techType == ing.techType)
                        {
                            ingredients[i]._amount += ing.amount;
                            goto cont;
                        }
                    }
                    ingredients.Add(new MyIngredient(ing.techType, ing.amount));
                cont:
                    continue;
                }
                data._ingredients.Clear();
                data._ingredients.AddRange(ingredients);
                __result = data;
            }

            private static List<IIngredient> GetIngredients(ITechData __result, int mult)
            {
                List<IIngredient> ingredients = new List<IIngredient>();
                for (int i = 0; i < __result.ingredientCount; i++)
                {
                    var ing = new MyIngredient( __result.GetIngredient(i).techType,  __result.GetIngredient(i).amount);
                    if (ing.techType == TechType.None)
                        continue;
                    if (!modKeyIgnore.Value.IsPressed())
                    {
                        if(Inventory.main.GetPickupCount(ing.techType) >= ing.amount * mult)
                        {
                            ing._amount *= mult;
                            ingredients.Add(ing);
                            continue;
                        }
                    }
                    var product = CraftData.Get(ing.techType, true);
                    if (product != null && product.ingredientCount > 0)
                    {
                        ingredients.AddRange(GetIngredients(product, Mathf.CeilToInt(ing.amount / (float)product.craftAmount)));
                    }
                    else
                    {
                        ing._amount *= mult;
                        ingredients.Add(ing);
                    }
                }
                return ingredients;
            }
        }
    }
}
