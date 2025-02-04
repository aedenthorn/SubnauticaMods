using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using CyclopsSolarCharger.Items.Equipment;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace CyclopsSolarCharger
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("com.snmodding.nautilus")]
    public class BepInExPlugin : BaseUnityPlugin
    {

        private static Assembly Assembly { get; } = Assembly.GetExecutingAssembly();
        private static BepInExPlugin context;

        public static ConfigEntry<bool> modEnabled;
        public static ConfigEntry<bool> isDebug;

        public static ConfigEntry<string> nameString;
        public static ConfigEntry<string> descriptionString;

        public static ConfigEntry<string> ingredients;
        public static ConfigEntry<CraftTree.Type> fabricatorType;
        public static readonly string idString = "CyclopsSolarCharger";

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

            ingredients = Config.Bind<string>("Options", "Ingredients", "AdvancedWiringKit:1,EnameledGlass:2", "Required ingredients, comma separated TechType:Amount pairs");
            fabricatorType = Config.Bind<CraftTree.Type>("Options", "FabricatorType", CraftTree.Type.CyclopsFabricator, "Fabricator to use to craft the chip.");

            nameString = Config.Bind<string>("Text", "NameString", "Cyclops Solar Charger", "Display name");
            descriptionString = Config.Bind<string>("Text", "DescriptionString", "Enables recharging the Cyclops' batteries while in the sun.", "Display description");
            // set project-scoped logger instance

            // Initialize custom prefabs
            InitializePrefabs();

            // register harmony patches, if there are any
            Harmony.CreateAndPatchAll(Assembly, $"{PluginInfo.PLUGIN_GUID}");
        }

        private void InitializePrefabs()
        {
            ChargerPrefab.Register();
        }

        [HarmonyPatch(typeof(SubRoot), nameof(SubRoot.UpdateThermalReactorCharge))]
        private static class SubRoot_UpdateThermalReactorCharge_Patch
        {
            public static void Postfix(SubRoot __instance)
            {
                if(!modEnabled.Value || __instance.upgradeConsole == null || !__instance.live.IsAlive())
                    return;

                CyclopsSolarChargerComponent component = __instance.GetComponent<CyclopsSolarChargerComponent>();
                if (component == null)
                {
                    component = __instance.gameObject.AddComponent<CyclopsSolarChargerComponent>();
                }
                component.upgradeModules = 0;
                Equipment modules = __instance.upgradeConsole.modules;
                for (int i = 0; i < 6; i++)
                {
                    string text = SubRoot.slotNames[i];
                    TechType techTypeInSlot = modules.GetTechTypeInSlot(text);
                    if (techTypeInSlot == ChargerPrefab.Info.TechType)
                    {
                        component.upgradeModules++;
                    }
                }
            }
        }
    }
}