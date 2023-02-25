using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using UnityEngine.UI;

namespace InventorySize
{
    [BepInPlugin("aedenthorn.InventorySize", "Inventory Size", "0.1.0")]
    public partial class BepInExPlugin : BaseUnityPlugin
    {
        private static BepInExPlugin context;

        public static ConfigEntry<bool> modEnabled;
        public static ConfigEntry<bool> isDebug;
        public static ConfigEntry<int> inventoryWidth;
        public static ConfigEntry<int> inventoryHeight;
        private static bool skip;

        private static RectTransform rts;
        private static RectTransform rtm;
        private static ScrollRect sr;

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
            inventoryWidth = Config.Bind<int>("Options", "InventoryWidth", 6, "Inventory width");
            inventoryHeight = Config.Bind<int>("Options", "InventoryHeight", 8, "Inventory width");

            inventoryWidth.SettingChanged += SettingChanged;
            inventoryHeight.SettingChanged += SettingChanged;

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
            Dbgl("Plugin awake");
        }

        private void SettingChanged(object sender, EventArgs e)
        {
            if (!modEnabled.Value)
                return;

            foreach (var i in FindObjectsOfType<Inventory>())
            {
                i.container.Resize(inventoryWidth.Value, inventoryHeight.Value);
            }
            foreach(var i in FindObjectsOfType<uGUI_InventoryTab>())
            {
                i.inventory.Init(Inventory.main.container);
            }
        }

        [HarmonyPatch(typeof(Inventory), "Awake")]
        private static class Inventory_Awake_Patch
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                Dbgl("transpiling Inventory.Awake");

                var codes = new List<CodeInstruction>(instructions);
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Ldc_I4_6 && codes[i + 1].opcode == OpCodes.Ldc_I4_8)
                    {
                        Dbgl($"Found dimensions, setting size to {inventoryWidth.Value}x{inventoryHeight.Value}");
                        codes[i].opcode = OpCodes.Call;
                        codes[i].operand = AccessTools.Method(typeof(BepInExPlugin), nameof(BepInExPlugin.GetInventoryWidth));
                        codes[i + 1].opcode = OpCodes.Call;
                        codes[i + 1].operand = AccessTools.Method(typeof(BepInExPlugin), nameof(BepInExPlugin.GetInventoryHeight));
                        break;
                    }
                }
                return codes.AsEnumerable();
            }
        }
        [HarmonyPatch(typeof(uGUI_InventoryTab), nameof(uGUI_InventoryTab.OnOpenPDA))]
        private static class uGUI_InventoryTab_OnOpenPDA_Patch
        {
            public static void Postfix(uGUI_InventoryTab __instance)
            {
                if (!modEnabled.Value || sr is null)
                    return;
                sr.verticalNormalizedPosition = 1;
                sr.horizontalNormalizedPosition = 0;
            }
        }
        [HarmonyPatch(typeof(uGUI_ItemsContainer), nameof(uGUI_ItemsContainer.Init))]
        private static class uGUI_ItemsContainer_Init_Patch
        {

            public static void Postfix(uGUI_ItemsContainer __instance, ItemsContainer ___container)
            {
                if (!modEnabled.Value || __instance != __instance.inventory.inventory)
                    return;
                RectTransform rtg = __instance.rectTransform;
                var cellSize = 71;
                var columns = Math.Min(___container.sizeX, 9);
                var containerSize = new Vector2(columns * cellSize, 10 * cellSize);
                var gridSize = new Vector2(___container.sizeX * cellSize, ___container.sizeY * cellSize);

                if (containerSize.y < gridSize.y && __instance.transform.parent.name != "Mask")
                {
                    Dbgl($"Adding scroll view");

                    GameObject scrollObject = new GameObject() { name = "ScrollView" };
                    scrollObject.transform.SetParent(__instance.transform.parent);
                    rts = scrollObject.AddComponent<RectTransform>();

                    GameObject mask = new GameObject { name = "Mask" };
                    mask.transform.SetParent(scrollObject.transform);
                    rtm = mask.AddComponent<RectTransform>();

                    __instance.transform.SetParent(mask.transform);

                    Texture2D tex = new Texture2D((int)Mathf.Ceil(rtm.rect.width), (int)Mathf.Ceil(rtm.rect.height));
                    Image image = mask.AddComponent<Image>();
                    image.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.zero);
                    Mask m = mask.AddComponent<Mask>();
                    m.showMaskGraphic = false;

                    sr = scrollObject.AddComponent<ScrollRect>();
                    sr.movementType = ScrollRect.MovementType.Clamped;
                    sr.horizontal = true;
                    sr.viewport = mask.GetComponent<RectTransform>();
                    sr.content = rtg;
                    sr.scrollSensitivity = 10;

                    Dbgl("Added scroll view");
                }

                rts.sizeDelta = containerSize;
                rts.localScale = new Vector3(1, 1, 1);
                if(columns > 7)
                {
                    rts.anchorMax = new Vector2(0.5f, 0.5f);
                    rts.anchorMin = new Vector2(0.5f, 0.5f);
                    rts.anchoredPosition = new Vector2(-cellSize * (columns / 2f - 1), -cellSize);
                }
                else
                {
                    rts.anchorMax = new Vector2(0.25f, 0.5f);
                    rts.anchorMin = new Vector2(0.25f, 0.5f);
                    rts.anchoredPosition = new Vector2(cellSize / 2f, -cellSize);
                }

                sr.verticalNormalizedPosition = 1;
                sr.horizontalNormalizedPosition = 0;

                rtm.anchoredPosition = Vector2.zero;
                rtm.sizeDelta = containerSize;
                rtm.localScale = new Vector3(1, 1, 1);

                rtg.anchorMax = new Vector2(0.5f, 0.5f);
                rtg.anchorMin = new Vector2(0.5f, 0.5f);
                rtg.sizeDelta = gridSize;
                rtg.localScale = new Vector3(1, 1, 1);
            }
        }
        private static int GetInventoryWidth()
        {
            if (!modEnabled.Value)
                return 6;

            return inventoryWidth.Value;
        }
        private static int GetInventoryHeight()
        {
            if (!modEnabled.Value)
                return 8;
            return inventoryHeight.Value;
        }

    }

}
