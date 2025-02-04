using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Image = UnityEngine.UI.Image;

namespace MobileResourceScanner
{
    [BepInPlugin("aedenthorn.MobileResourceScanner", "Mobile Resource Scanner", "0.2.3")]
    public partial class BepInExPlugin : BaseUnityPlugin
    {
        private static BepInExPlugin context;

        public static ConfigEntry<bool> modEnabled;
        public static ConfigEntry<bool> isDebug;

        public static ConfigEntry<bool> requireScanned;
        public static ConfigEntry<float> range;
        public static ConfigEntry<float> interval;
        public static ConfigEntry<int> menuButton;
        public static ConfigEntry<string> currentResource;
        public static ConfigEntry<string> nameString;
        public static ConfigEntry<string> descriptionString;
        public static ConfigEntry<string> menuHeader;
        public static ConfigEntry<string> ingredients;
        public static ConfigEntry<string> openMenuString;
        public static ConfigEntry<CraftTree.Type> fabricatorType;
        public static ConfigEntry<KeyboardShortcut> menuHotkey;
        
        public static bool intervalChanged = true;

        private static TechType currentTechType = TechType.None;
        private static string currentTechName = "None";

        public static GameObject menuGO;
        public static TechType chipTechType;
        public static readonly string idString = "MobileResourceScanner";
        
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
            
            requireScanned = Config.Bind<bool>("Options", "RequireScanned", false, "Only show resources that have been scanned by the player");
            range = Config.Bind<float>("Options", "Range", 500f, "Range (m)");
            menuButton = Config.Bind<int>("Options", "Button", 1, "Which button to use to open the menu.");
            interval = Config.Bind<float>("Options", "Interval", 10f, "Interval (s)");
            currentResource = Config.Bind<string>("Options", "CurrentResource", "None", "Current resource type to scan for");
            ingredients = Config.Bind<string>("Options", "Ingredients", "ComputerChip:1,Magnetite:1", "Required ingredients, comma separated TechType:Amount pairs");
            fabricatorType = Config.Bind<CraftTree.Type>("Options", "FabricatorType", CraftTree.Type.MapRoom, "Fabricator to use to craft the chip.");
            menuHotkey = Config.Bind<KeyboardShortcut>("Options", "MenuHotkey", new KeyboardShortcut(KeyCode.L, new KeyCode[] { KeyCode.LeftShift}), "Key shortcut used to open the menu.");

            nameString = Config.Bind<string>("Text", "NameString", "Mobile Resource Scanner", "Display name");
            descriptionString = Config.Bind<string>("Text", "DescriptionString", "Equip to enable mobile resource scanning", "Display description");
            menuHeader = Config.Bind<string>("Text", "MenuHeader", "Select Resource", "Menu header.");
            openMenuString = Config.Bind<string>("Text", "OpenMenuString", "Switch Resource ({0})", "Tooltip text.");

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), Info.Metadata.GUID);

            interval.SettingChanged += Interval_SettingChanged;

            Enum.TryParse(currentResource.Value, false, out currentTechType);
            currentTechName = Language.main.Get(currentTechType);

            InitializePrefabs();

            Dbgl("Plugin awake");
        }
        private void InitializePrefabs()
        {
            ScannerItem.Register();
        }
        public void Update()
        {
            if (modEnabled.Value && menuHotkey.Value.IsDown())
            {
                ShowMenu();
            }
        }

        private void Interval_SettingChanged(object sender, System.EventArgs e)
        {
            intervalChanged = true;
        }


        [HarmonyPatch(typeof(uGUI_ResourceTracker), "IsVisibleNow")]
        private static class uGUI_ResourceTracker_IsVisibleNow_Patch
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                Dbgl("transpiling uGUI_ResourceTracker.IsVisibleNow");

                var codes = new List<CodeInstruction>(instructions);
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Callvirt && codes[i].operand is MethodInfo && (MethodInfo)codes[i].operand == AccessTools.Method(typeof(Equipment), nameof(Equipment.GetCount)))
                    {
                        Dbgl("Found method");
                        codes.Insert(i + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(BepInExPlugin), nameof(BepInExPlugin.GetCount))));
                        break;
                    }
                }

                return codes.AsEnumerable();
            }
        }

        private static int GetCount(int count)
        {
            if (!modEnabled.Value || count > 0)
                return count;
            return Inventory.main.equipment.GetCount(chipTechType);
        }

        [HarmonyPatch(typeof(uGUI_ResourceTracker), "GatherNodes")]
        private static class uGUI_ResourceTracker_GatherNodes_Patch
        {
            static bool Prefix(uGUI_ResourceTracker __instance, HashSet<ResourceTrackerDatabase.ResourceInfo> ___nodes, List<TechType> ___techTypes)
            {
                if (!modEnabled.Value || currentTechType == TechType.None || Inventory.main?.equipment?.GetCount(chipTechType) == 0)
                    return true;

                Camera camera = MainCamera.camera;
                ___nodes.Clear();
                ___techTypes.Clear();
                ResourceTrackerDatabase.GetTechTypesInRange(camera.transform.position, range.Value, new List<TechType>() { currentTechType });
                ResourceTrackerDatabase.GetNodes(camera.transform.position, range.Value, currentTechType, ___nodes);

                if (intervalChanged)
                {
                    __instance.CancelInvoke("GatherNodes");
                    __instance.InvokeRepeating("GatherNodes", interval.Value, interval.Value);
                    intervalChanged = false;
                }
                return false;
            }
        }
        [HarmonyPatch(typeof(uGUI_Equipment), nameof(uGUI_Equipment.OnPointerClick))]
        private static class uGUI_Equipment_OnPointerClick_Patch
        {
            static bool Prefix(uGUI_Equipment __instance, uGUI_EquipmentSlot instance, int button, Dictionary<uGUI_EquipmentSlot, InventoryItem> ___slots)
            {
                if (!modEnabled.Value)
                    return true;
                if (!___slots.TryGetValue(instance, out var item) || item.techType != chipTechType || Inventory.main.GetItemAction(item, button) > ItemAction.None || button != menuButton.Value)
                {
                    return true;
                }
                Dbgl("Clicked on chip, showing menu");
                Player.main.GetPDA().Close();
                ShowMenu();
                return false;
            }
        }
        [HarmonyPatch(typeof(TooltipFactory), "ItemActions")]
        private static class TooltipFactory_ItemActions_Patch
        {
            static void Postfix(StringBuilder sb, InventoryItem item)
            {
                if (!modEnabled.Value || item.techType != chipTechType)
                    return;
                AccessTools.Method(typeof(TooltipFactory), "WriteAction").Invoke(null, new object[] { sb, AccessTools.Field(typeof(TooltipFactory), $"stringButton{menuButton.Value}").GetValue(null), string.Format(openMenuString.Value, currentTechName) });
            }
        }
        private static void ShowMenu()
        {
            var template = IngameMenu.main?.GetComponentInChildren<IngameMenuTopLevel>();
            if (template is null)
            {
                ErrorMessage.AddWarning("Menu template not found!");
                return;
            }
            var rtt = IngameMenu.main.GetComponent<RectTransform>();
            menuGO = new GameObject("ResourceMenu");
            menuGO.transform.SetParent(uGUI.main.hud.transform);
            var rtb = menuGO.AddComponent<RectTransform>();
            rtb.sizeDelta = new Vector2(545, 700);
            //rtb.localPosition = rtt.localPosition;
            //rtb.pivot = rtt.pivot;
            //rtb.anchorMax = rtt.anchorMax;
            //rtb.anchorMin = rtt.anchorMin;

            Dbgl($"Adding scroll view");

            GameObject scrollObject = new GameObject() { name = "ScrollView" };
            scrollObject.transform.SetParent(menuGO.transform);
            var rts = scrollObject.AddComponent<RectTransform>();
            rts.sizeDelta = rtb.sizeDelta;

            GameObject mask = new GameObject { name = "Mask" };
            mask.transform.SetParent(scrollObject.transform);
            var rtm = mask.AddComponent<RectTransform>();
            rtm.sizeDelta = rtb.sizeDelta;

            Texture2D tex = new Texture2D((int)Mathf.Ceil(rtm.rect.width), (int)Mathf.Ceil(rtm.rect.height));
            Image image = mask.AddComponent<Image>();
            image.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.zero);
            Mask m = mask.AddComponent<Mask>();
            m.showMaskGraphic = false;

            var sr = scrollObject.AddComponent<ScrollRect>();

            Dbgl("Added scroll view");

            var gridGO = Instantiate(template.gameObject, mask.transform);
            var header = gridGO.transform.Find("Header");
            var rth = header.GetComponent<RectTransform>();

            var rtg = gridGO.GetComponent<RectTransform>();
            sr.content = rtg;

            var menu = gridGO.AddComponent<ResourceMenu>();
            menu.Select();

            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.horizontal = false;
            sr.viewport = mask.GetComponent<RectTransform>();
            sr.scrollSensitivity = 50;

            Dbgl("Created menu");

            var buttons = gridGO.GetComponentsInChildren<Button>(true);
            Button templateButton = null;
            bool first = true;
            foreach(var button in buttons)
            {
                if (first)
                {
                    templateButton = button;
                    Destroy(templateButton.gameObject.GetComponent<EventTrigger>());
                    first = false;
                }
                else Destroy(button.gameObject);
            }
            var techs = new List<TechType>();
            foreach(var t in ResourceTrackerDatabase.GetTechTypes())
            {
                if (!requireScanned.Value || PDAScanner.ContainsCompleteEntry(t))
                    techs.Add(t);
            }
            
            techs.Sort(delegate (TechType a, TechType b)
            {
                return Language.main.Get(a).CompareTo(Language.main.Get(b));
            });

            Dbgl($"Found {techs.Count} techs");
            if(techs.Count < 1)
            {
                ErrorMessage.AddWarning("No techs found!");
                return;
            }

            techs.Insert(0, TechType.None);

            rtb.localScale = Vector3.one;
            rtb.anchoredPosition3D = Vector3.zero;


            header.SetParent(menuGO.transform);
            rth.anchoredPosition = new Vector2(272.5f, 700);
            rth.sizeDelta = new Vector2(545f, 100);
            var headerText = header.GetComponent<TextMeshProUGUI>();
            headerText.text = menuHeader.Value;
            Dbgl($"Header size: {rth.sizeDelta}");

            foreach (var t in techs)
            {
                GameObject b = Instantiate(templateButton.gameObject, gridGO.transform);
                SetupButton(b, t);
            }
            Destroy(templateButton.gameObject);

            sr.verticalNormalizedPosition = 1;
            sr.horizontalNormalizedPosition = 0;

            uGUI_INavigableIconGrid grid = gridGO.GetComponentInChildren<uGUI_INavigableIconGrid>();
            if(grid is null)
                grid = gridGO.GetComponent<uGUI_INavigableIconGrid>();
            if(grid != null)
                GamepadInputModule.current.SetCurrentGrid(grid);
        }

        private static void SetupButton(GameObject gameObject, TechType t)
        {
            gameObject.name = $"Button{t}";
            var button = gameObject.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(delegate ()
            {
                currentTechType = t;
                currentResource.Value = t.ToString();
                currentTechName = Language.main.Get(t);
                ErrorMessage.AddWarning($"Mobile scanner tech type set to {currentTechName}");
                Destroy(menuGO);
            });
            var text = gameObject.transform.GetComponentInChildren<TextMeshProUGUI>();
            text.text = Language.main.Get(t);
        }
    }
}
