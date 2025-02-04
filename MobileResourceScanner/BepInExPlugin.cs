using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using MobileResourceScanner.Items.Equipment;
using rail;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MobileResourceScanner
{
    [BepInPlugin("aedenthorn.MobileResourceScanner", "MobileResourceScanner", "1.0.1")]
    [BepInDependency("com.snmodding.nautilus")]
    public class BepInExPlugin : BaseUnityPlugin
    {

        private static Assembly Assembly { get; } = Assembly.GetExecutingAssembly();
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
            menuHotkey = Config.Bind<KeyboardShortcut>("Options", "MenuHotkey", new KeyboardShortcut(KeyCode.L, new KeyCode[] { KeyCode.LeftShift }), "Key shortcut used to open the menu.");

            nameString = Config.Bind<string>("Text", "NameString", "Mobile Resource Scanner", "Display name");
            descriptionString = Config.Bind<string>("Text", "DescriptionString", "Equip to enable mobile resource scanning", "Display description");
            menuHeader = Config.Bind<string>("Text", "MenuHeader", "Select Resource", "Menu header.");
            openMenuString = Config.Bind<string>("Text", "OpenMenuString", "Switch Resource ({0})", "Tooltip text.");
            // set project-scoped logger instance

            // Initialize custom prefabs
            InitializePrefabs();

            // register harmony patches, if there are any
            Harmony.CreateAndPatchAll(Assembly, $"{Info.Metadata.GUID}");
        }
        public void Update()
        {
            if (modEnabled.Value && menuHotkey.Value.IsDown())
            {
                ShowMenu();
            }
        }

        private void InitializePrefabs()
        {
            ScannerPrefab.Register();
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
        [HarmonyPatch(typeof(uGUI_ResourceTracker), "GatherScanned")]
        private static class uGUI_ResourceTracker_GatherScanned_Patch
        {
            static bool Prefix(uGUI_ResourceTracker __instance)
            {
                if (!modEnabled.Value || currentTechType == TechType.None || Inventory.main?.equipment?.GetCount(ScannerPrefab.Info.TechType) == 0)
                    return true;
                __instance.GatherNodes();
                return false;
            }
        }

        private static int GetCount(int count)
        {
            if (!modEnabled.Value || count > 0)
                return count;
            return Inventory.main.equipment.GetCount(ScannerPrefab.Info.TechType);
        }

        [HarmonyPatch(typeof(uGUI_ResourceTracker), "GatherNodes")]
        private static class uGUI_ResourceTracker_GatherNodes_Patch
        {
            static bool Prefix(uGUI_ResourceTracker __instance, HashSet<ResourceTrackerDatabase.ResourceInfo> ___nodes, List<TechType> ___techTypes)
            {
                if (!modEnabled.Value || currentTechType == TechType.None || Inventory.main?.equipment?.GetCount(ScannerPrefab.Info.TechType) == 0)
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
                if (!___slots.TryGetValue(instance, out var item) || item.techType != ScannerPrefab.Info.TechType || Inventory.main.GetItemAction(item, button) > ItemAction.None || button != menuButton.Value)
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
                if (!modEnabled.Value || item.techType != ScannerPrefab.Info.TechType)
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
            int width = 545;
            int height = 800;
            var rtt = IngameMenu.main.GetComponent<RectTransform>();
            menuGO = new GameObject("ResourceMenu");
            menuGO.transform.SetParent(uGUI.main.hud.transform);
            var rtb = menuGO.AddComponent<RectTransform>();
            rtb.sizeDelta = new Vector2(width, height);
            //rtb.localPosition = rtt.localPosition;
            //rtb.pivot = rtt.pivot;
            //rtb.anchorMax = rtt.anchorMax;
            //rtb.anchorMin = rtt.anchorMin;
            var menuContent = Instantiate(template.GetComponentInChildren<VerticalLayoutGroup>().gameObject, menuGO.transform);
            DestroyImmediate(menuContent.GetComponent<IngameMenuTopLevel>());
            DestroyImmediate(menuContent.GetComponent<ContentSizeFitter>());
            DestroyImmediate(menuContent.GetComponent<VerticalLayoutGroup>());
            DestroyImmediate(menuContent.transform.Find("Header").gameObject);
            menuContent.GetComponent<RectTransform>().sizeDelta = new Vector2(width, height);

            Dbgl($"Adding scroll view");

            GameObject scrollObject = new GameObject() { name = "ScrollView" };
            scrollObject.transform.SetParent(menuContent.transform);
            var rts = scrollObject.AddComponent<RectTransform>();
            rts.sizeDelta = new Vector2(width, height - 200); // leave room for button

            GameObject mask = new GameObject { name = "Mask" };
            mask.transform.SetParent(scrollObject.transform);
            var rtm = mask.AddComponent<RectTransform>();
            rtm.sizeDelta = new Vector2(rts.sizeDelta.x - 140, rts.sizeDelta.y);

            Texture2D tex = new Texture2D((int)Mathf.Ceil(rtm.rect.width), (int)Mathf.Ceil(rtm.rect.height));
            Image image = mask.AddComponent<Image>();
            image.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.zero);
            Mask m = mask.AddComponent<Mask>();
            m.showMaskGraphic = false;

            var gridContent = Instantiate(menuContent.transform.Find("ButtonLayout").gameObject, mask.transform);
            DestroyImmediate(menuContent.transform.Find("ButtonLayout").gameObject);

            var sr = scrollObject.AddComponent<ScrollRect>();

            Dbgl("Added scroll view");

            var header = Instantiate(template.transform.Find("Header").gameObject, menuGO.transform).transform;
            var rth = header.GetComponent<RectTransform>();

            var rtg = gridContent.GetComponent<RectTransform>();
            DestroyImmediate(gridContent.GetComponent<ContentSizeFitter>());
            sr.content = rtg;

            var menu = menuContent.gameObject.AddComponent<ResourceMenu>();
            menu.Select();

            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.horizontal = false;
            sr.viewport = mask.GetComponent<RectTransform>();
            sr.scrollSensitivity = 50;

            Dbgl("Created menu");

            var buttons = menuContent.GetComponentsInChildren<Button>(true);
            Button templateButton = null;
            bool first = true;
            foreach (var button in buttons)
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
            foreach (var t in ResourceTrackerDatabase.GetTechTypes())
            {
                if (!requireScanned.Value || PDAScanner.ContainsCompleteEntry(t))
                    techs.Add(t);
            }

            techs.Sort(delegate (TechType a, TechType b)
            {
                return Language.main.Get(a).CompareTo(Language.main.Get(b));
            });

            Dbgl($"Found {techs.Count} techs");
            if (techs.Count < 1)
            {
                ErrorMessage.AddWarning("No techs found!");
                return;
            }

            techs.Insert(0, TechType.None);
            rtg.sizeDelta = new Vector2(rtm.sizeDelta.x, techs.Count * 65);
            rtb.localScale = Vector3.one;
            rtb.anchoredPosition3D = Vector3.zero;


            header.SetParent(menuGO.transform);
            rth.localPosition = new Vector2(0, 332);
            rth.sizeDelta = new Vector2(545f, 100);
            var headerText = header.GetComponent<TextMeshProUGUI>();
            headerText.text = menuHeader.Value;
            Dbgl($"Header size: {rth.sizeDelta}");
            
            foreach (var t in techs)
            {
                GameObject b = Instantiate(templateButton.gameObject, gridContent.transform);
                SetupButton(b, t);
            }
            Destroy(templateButton.gameObject);

            sr.verticalNormalizedPosition = 1;
            sr.horizontalNormalizedPosition = 0;

            uGUI_INavigableIconGrid grid = menuContent.GetComponentInChildren<uGUI_INavigableIconGrid>();
            if (grid != null)
                GamepadInputModule.current.SetCurrentGrid(grid);

            GameObject sti = Instantiate(uGUI.main.userInput.inputField.gameObject, menuGO.transform);
            sti.name = "FilterInput";
            var tmpi = sti.GetComponentInChildren<TMP_InputField>();
            tmpi.text = "";
            tmpi.onValueChanged = new TMP_InputField.OnChangeEvent();
            tmpi.onValueChanged.AddListener(delegate (string value) { FilterMenuEntries(gridContent, value); });
            var rt = sti.GetComponent<RectTransform>();
            rt.localPosition = new Vector3(0, -332, 0);
        }

        private static void FilterMenuEntries(GameObject gridGO, string value)
        {
            var lower = value.ToLower();
            int count = 0;
            foreach (var b in gridGO.GetComponentsInChildren<Button>(true))
            {
                var tmp = b.GetComponentInChildren<TextMeshProUGUI>();
                bool active = value?.Length < 2 || tmp.text.ToLower().Contains(lower);
                b.transform.gameObject.SetActive(active);
                if (active)
                    count++;
            }
            gridGO.GetComponent<RectTransform>().sizeDelta = new Vector2(gridGO.GetComponent<RectTransform>().sizeDelta.x, 65 * count);
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