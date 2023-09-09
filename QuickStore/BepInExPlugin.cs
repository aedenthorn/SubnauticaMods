using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace QuickStore
{
    [BepInPlugin("aedenthorn.QuickStore", "QuickStore", "0.2.2")]
    public partial class BepInExPlugin : BaseUnityPlugin
    {
        private static BepInExPlugin context;

        public static ConfigEntry<bool> modEnabled;
        public static ConfigEntry<bool> isDebug;

        public static ConfigEntry<float> range;
        public static ConfigEntry<KeyCode> hotKey;
        public static ConfigEntry<KeyCode> modHotKey;
        public static ConfigEntry<KeyCode> hotKeyStorage;
        public static ConfigEntry<KeyCode> hotKeyInventory;
        public static ConfigEntry<string> storedMessage;

        public static string[] allowedTypes = new string[0];
        public static string[] forbiddenTypes = new string[0];
        private static string allowedFile = "allowed_types.txt";
        private static string forbiddenFile = "forbidden_types.txt";

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
            hotKey = Config.Bind<KeyCode>("Options", "HotKeyStore", KeyCode.Semicolon, "Key to press to store items.");
            modHotKey = Config.Bind<KeyCode>("Options", "HotKeyMod", KeyCode.LeftShift, "Key to hold when storage open to not check whether items exist in destination.");
            hotKeyStorage = Config.Bind<KeyCode>("Options", "HotKeyStorage", KeyCode.RightArrow, "Key to press when storage open to transfer all items from inventory into storage.");
            hotKeyInventory = Config.Bind<KeyCode>("Options", "HotKeyInventory", KeyCode.LeftArrow, "Key to press when storage open to transfer all items from storage into inventory.");
            range = Config.Bind<float>("Options", "Range", 100f, "Range (m)");
            storedMessage = Config.Bind<string>("Options", "StoredMessage", "Stored {0} items.", "Message to show when items are stored.");

            modEnabled.SettingChanged += ModEnabled_SettingChanged;

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), Info.Metadata.GUID);
            Dbgl("Plugin awake");

            ReloadTypes();
        }

        private void Update()
        {
            if (!modEnabled.Value || Player.main is null)
                return;

            if (Input.GetKeyDown(hotKey.Value))
            {
                Dbgl("Pressed quick store key");
                StartCoroutine(StoreItemsRoutine());
            }
        }

        private IEnumerator StoreItemsRoutine()
        {

            int count = 0;
            var array = ((Component[])FindObjectsOfType<StorageContainer>()).Concat(FindObjectsOfType<SeamothStorageContainer>());
            for (int i = 0; i < array.Count(); i++)
            {
                if (Vector3.Distance(Player.main.transform.position, array.ElementAt(i).transform.position) > range.Value)
                    continue;
                ItemsContainer ic = (ItemsContainer)AccessTools.Property(array.ElementAt(i).GetType(), "container").GetValue(array.ElementAt(i));
                count += TransferItems(Inventory.main.container, ic, !Input.GetKey(modHotKey.Value));
                yield return null;
            }

            if (!string.IsNullOrEmpty(storedMessage.Value))
                ErrorMessage.AddWarning(string.Format(storedMessage.Value, count));

            yield break;
        }

        private static int TransferItems(ItemsContainer source, ItemsContainer dest, bool require)
        {
            int count = 0;
            foreach(var item in source.ToArray())
            {
                if (!IsAllowed(item.techType))
                    continue;
                //Dbgl($"Trying to store {item.techType.AsString(false)}");
                if (StoreInContainer(dest, item, require))
                {
                    count++;
                    Dbgl($"Stored {item.techType.AsString(false)}");
                }
            }
            return count;
        }

        private static bool StoreInContainer(ItemsContainer dest, InventoryItem item, bool require)
        {
            return dest is null || (require && dest.GetItems(item.techType)?.Any() != true) ? false : Inventory.AddOrSwap(item, dest);
        }

        private void ModEnabled_SettingChanged(object sender, EventArgs e)
        {
            ReloadTypes();
        }

        private void ReloadTypes()
        {
            if (!modEnabled.Value)
                return;
            forbiddenTypes = new string[0];
            allowedTypes = new string[0];
            string folder = AedenthornUtils.GetAssetPath(context, false);
            string f = Path.Combine(folder, forbiddenFile);
            string a = Path.Combine(folder, allowedFile);
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
                allowedTypes = Enum.GetNames(typeof(TechType));
                File.Create(f);
                File.WriteAllLines(a, allowedTypes);
            }
            else
            {
                if (File.Exists(f))
                    forbiddenTypes = File.ReadAllLines(f);
                if (File.Exists(a))
                    allowedTypes = File.ReadAllLines(a);
            }
        }
        private static bool IsAllowed(TechType type)
        {
            string ts = type.ToString();
            if (forbiddenTypes.Length > 0 && Array.IndexOf(forbiddenTypes, ts) >= 0)
            {
                return false;
            }
            return allowedTypes.Length == 0 || Array.IndexOf(allowedTypes, ts) >= 0;
        }

        [HarmonyPatch(typeof(uGUI_ItemsContainer), nameof(uGUI_ItemsContainer.DoUpdate))]
        private static class uGUI_ItemsContainer_DoUpdate_Patch
        {
            static void Postfix(uGUI_ItemsContainer __instance, ItemsContainer ___container)
            {

                if (!modEnabled.Value || __instance != __instance.inventory.storage)
                    return;
                if (Input.GetKeyDown(hotKeyInventory.Value))
                {
                    TransferItems(___container, Inventory.main.container, !Input.GetKey(modHotKey.Value));
                }
                else if (Input.GetKeyDown(hotKeyStorage.Value))
                {
                    TransferItems(Inventory.main.container, ___container, !Input.GetKey(modHotKey.Value));
                }
            }
        }
    }
}
