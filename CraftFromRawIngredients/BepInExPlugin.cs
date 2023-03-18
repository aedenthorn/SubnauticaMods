using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Newtonsoft.Json;
using Oculus.Platform.Models;
using SMLHelper.V2.Crafting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace CraftFromRawIngredients
{
    [BepInPlugin("aedenthorn.CraftFromRawIngredients", "Craft From Raw Ingredients", "0.1.0")]
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

        [HarmonyPatch(typeof(CraftData), nameof(CraftData.Get))]
        private static class CraftData_Get_Patch
        {
            static void Postfix(ref ITechData __result)
            {
                if (!modEnabled.Value || (!modKey.Value.IsPressed() && !modKeyIgnore.Value.IsPressed()) || __result is null || __result.ingredientCount == 0)
                    return;
                MyTechData data = new MyTechData(__result);
                data._ingredients = GetIngredients(__result, 1);
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
                        ingredients.AddRange(GetIngredients(product, ing.amount));
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
