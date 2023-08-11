using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace DeveloperMenu
{
    [BepInPlugin("aedenthorn.DeveloperMenu", "DeveloperMenu", "0.4.0")]
    public partial class BepInExPlugin : BaseUnityPlugin
    {
        private static BepInExPlugin context;

        public static ConfigEntry<bool> modEnabled;
        public static ConfigEntry<bool> isDebug;

        public static ConfigEntry<bool> developerModeEnabled;
        public static ConfigEntry<float> range;
        public static ConfigEntry<string> spawnTabLabel;
        public static ConfigEntry<KeyboardShortcut> hotKey;
        public static ConfigEntry<KeyCode> ingredientsModKey;

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
            developerModeEnabled = Config.Bind<bool>("Options", "DeveloperModeEnabled", false, "Whether developer mode is enabled");
            hotKey = Config.Bind<KeyboardShortcut>("Options", "HotKey", new KeyboardShortcut(KeyCode.Backslash), "Key to press to toggle developer menu.");
            ingredientsModKey = Config.Bind<KeyCode>("Options", "IngredientsModKey", KeyCode.LeftShift, "Key to hold when pressing give button to give ingredients instead.");
            spawnTabLabel = Config.Bind<string>("Options", "SpawnTabLabel", "Spawn", "Spawn tab label.");

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), Info.Metadata.GUID);
            Dbgl("Plugin awake");

        }
        public void Update()
        {
            if (modEnabled.Value && hotKey.Value.IsDown())
            {
                developerModeEnabled.Value = !developerModeEnabled.Value;
                Dbgl($"Developer mode enabled: {developerModeEnabled.Value}");
            }
        }
        [HarmonyPatch(typeof(GameInput), nameof(GameInput.GetEnableDeveloperMode))]
        private static class GameInput_GetEnableDeveloperMode_Patch
        {
            static void Postfix(GameInput __instance, ref bool __result)
            {
                if (modEnabled.Value)
                    __result = developerModeEnabled.Value;
            }
        }
        [HarmonyPatch(typeof(IngameMenu), "Update")]
        private static class IngameMenu_Update_Patch
        {
            static void Prefix(IngameMenu __instance, ref bool ___developerMode)
            {
                if (!modEnabled.Value)
                    return;
                ___developerMode = developerModeEnabled.Value;
                __instance.developerButton.gameObject.SetActive(___developerMode);
            }
        }
        [HarmonyPatch(typeof(PlatformUtils), nameof(PlatformUtils.GetDevToolsEnabled))]
        private static class PlatformUtils_GetDevToolsEnabled_Patch
        {
            static bool Prefix(ref bool __result)
            {
                if (!modEnabled.Value)
                    return true;
                __result = true;
                return false;
            }
        }
        [HarmonyPatch(typeof(InventoryConsoleCommands), "OnConsoleCommand_item")]
        private static class InventoryConsoleCommands_OnConsoleCommand_item_Patch
        {
            static bool Prefix(InventoryConsoleCommands __instance, NotificationCenter.Notification n)
            {
                if (!modEnabled.Value || !Input.GetKey(ingredientsModKey.Value))
                    return true;

                if (n != null && n.data != null && n.data.Count > 0)
                {
                    string text = (string)n.data[0];
                    TechType techType;
                    if (!UWE.Utils.TryParseEnum<TechType>(text, out techType))
                        return true;
                    ITechData techData = CraftData.Get(techType, true);
                    if (techData == null)
                        return true;
                    Dbgl($"Spawning {techData.ingredientCount} ingredients for {techType}");

                    for (int i = 0; i < techData.ingredientCount; i++)
                    {
                        var ing = techData.GetIngredient(i);
                        if (CraftData.IsAllowed(ing.techType))
                        {
                            int number = ing.amount;
                            Dbgl($"Spawning {ing.amount}x {ing.techType}");
                            __instance.StartCoroutine(ItemCmdSpawnAsync(number, ing.techType));
                        }
                    }
                }
                return false;
            }
        }

        private static IEnumerator ItemCmdSpawnAsync(int number, TechType techType)
        {
            TaskResult<GameObject> result = new TaskResult<GameObject>();
            int num;
            for (int i = 0; i < number; i = num + 1)
            {
                yield return CraftData.InstantiateFromPrefabAsync(techType, result, false);
                GameObject gameObject = result.Get();
                if (gameObject != null)
                {
                    gameObject.transform.position = MainCamera.camera.transform.position + MainCamera.camera.transform.forward * 3f;
                    CrafterLogic.NotifyCraftEnd(gameObject, techType);
                    Pickupable component = gameObject.GetComponent<Pickupable>();
                    if (component != null && !Inventory.main.Pickup(component, false))
                    {
                        ErrorMessage.AddError(Language.main.Get("InventoryFull"));
                    }
                }
                num = i;
            }
            yield break;
        }


        [HarmonyPatch(typeof(uGUI_DeveloperPanel), "AddTabs")]
        private static class uGUI_DeveloperPanel_AddTabs_Patch
        {
            static void Postfix(uGUI_DeveloperPanel __instance)
            {
                if (!modEnabled.Value)
                    return;
                int tabIndex = __instance.AddTab(spawnTabLabel.Value);

                List<CommandData> giveList = new List<CommandData>();

                foreach(var tech in Enum.GetNames(typeof(TechType)))
                {
                    if (tech == "None")
                        continue;
                    giveList.Add(new CommandData($"item {tech}", tech, false));
                }

                giveList.Sort(delegate (CommandData a, CommandData b) {
                    return Language.main.Get(a.label).CompareTo(Language.main.Get(b.label));
                });
                var mi = AccessTools.Method(typeof(uGUI_DeveloperPanel), "AddConsoleCommandButton");
                foreach (var give in giveList)
                {
                    mi.Invoke(__instance, new object[] { tabIndex, give.command, give.label, give.close });
                }

                AccessTools.Method(typeof(uGUI_DeveloperPanel), "AddGraphicsTab").Invoke(__instance, new object[0]);
                //AccessTools.Method(typeof(uGUI_DeveloperPanel), "AddTestingTab").Invoke(__instance, new object[0]);
            }
        }
        [HarmonyPatch(typeof(uGUI_DeveloperPanel), "AddCommandsTab")]
        private static class uGUI_DeveloperPanel_AddCommandsTab_Patch
        {
            static bool Prefix(uGUI_DeveloperPanel __instance)
            {
                if (!modEnabled.Value)
                    return true;
                int tabIndex = __instance.AddTab("Commands");

                List<CommandData> commandList = new List<CommandData>()
                {
                    new CommandData("explodeship", null, false),
                    new CommandData("kill", null, false),
                    new CommandData("bobthebuilder", null, false),
                    new CommandData("nocost", null, false),
                    new CommandData("nodamage", null, false),
                    new CommandData("oxygen", null, false),
                    new CommandData("fastbuild", null, false),
                    new CommandData("fastscan", null, false),
                    new CommandData("fasthatch", null, false),
                    new CommandData("fastgrow", null, false),
                    new CommandData("unlockdoors", null, false),
                    new CommandData("precursorkeys", null, false),
                    new CommandData("resetmotormode", null, false),
                    new CommandData("pdalog all", null, false),
                    new CommandData("ency all", null, false),
                    new CommandData("unlock all", null, false),
                    new CommandData("infect 50", null, false),
                    
                    new CommandData("damagesub", null, false),
                    new CommandData("day", null, false),
                    new CommandData("destroycyclops", null, false),
                    new CommandData("filterfast", null, false),
                    new CommandData("flood", null, false),
                    new CommandData("fly", null, false),
                    new CommandData("fog", null, false),
                    new CommandData("fps", null, false),
                    new CommandData("freecam", null, false),
                    new CommandData("ghost", null, false),
                    new CommandData("instagib", null, false),
                    new CommandData("invisible", null, false),
                    new CommandData("night", null, false),
                    new CommandData("noenergy", null, false),
                    new CommandData("noshadows", null, false),
                    new CommandData("nosurvival", null, false),
                    new CommandData("nitrogen", null, false),
                    new CommandData("radiation", null, false),
                    new CommandData("restorecyclops", null, false),
                    new CommandData("schedule", null, false)
                };

                commandList.Sort(delegate (CommandData a, CommandData b) {
                    return a.command.CompareTo(b.command);
                });
                var mi = AccessTools.Method(typeof(uGUI_DeveloperPanel), "AddConsoleCommandButton");
                foreach (var give in commandList)
                {
                    mi.Invoke(__instance, new object[] { tabIndex, give.command, give.label, give.close });
                }
                return false;
            }
        }
    }
}
