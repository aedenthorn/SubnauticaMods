using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UWE;

namespace AutoHarvest
{
    [BepInPlugin("aedenthorn.AutoHarvest", "AutoHarvest", "0.3.4")]
    public partial class BepInExPlugin : BaseUnityPlugin
    {
        private static BepInExPlugin context;

        public static ConfigEntry<bool> modEnabled;
        public static ConfigEntry<bool> isDebug;

        public static ConfigEntry<bool> breakBreakables;
        public static ConfigEntry<bool> cutCuttables;
        public static ConfigEntry<bool> autoPickupBreakables;
        public static ConfigEntry<bool> autoPickupCuttables;
        public static ConfigEntry<bool> pickupables;
        public static ConfigEntry<bool> allowPickupCreature;
        public static ConfigEntry<bool> allowPickupEdible;
        public static ConfigEntry<bool> allowPickupEgg;
        public static ConfigEntry<bool> preventPickingUpDropped;
        public static ConfigEntry<float> range;
        public static ConfigEntry<float> interval;
        public static ConfigEntry<KeyCode> toggleKey;
        public static ConfigEntry<string> disabledMessage;
        public static ConfigEntry<string> enabledMessage;

        public static string[] allowedTypes = new string[0];
        public static string[] forbiddenTypes = new string[0];
        private static string allowedFile = "allowed_types.txt";
        private static string forbiddenFile = "forbidden_types.txt";
        
        private static FieldInfo inventoryItemField = AccessTools.Field(typeof(Pickupable), "inventoryItem");

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
            toggleKey = Config.Bind<KeyCode>("General", "ToggleKey", KeyCode.End, "Key to press to toggle mod.");
            preventPickingUpDropped = Config.Bind<bool>("Options", "PreventPickingUpDropped", true, "Prevent auto pickup of items dropped by the player");
            breakBreakables = Config.Bind<bool>("Options", "BreakBreakables", true, "Break breakables");
            cutCuttables = Config.Bind<bool>("Options", "CutCuttables", true, "Cut cuttables");
            autoPickupBreakables = Config.Bind<bool>("Options", "AutoPickupBreakables", true, "Auto-pickup breakables");
            autoPickupCuttables = Config.Bind<bool>("Options", "AutoPickupCuttables", true, "Auto-pickup cuttables");
            pickupables = Config.Bind<bool>("Options", "Pickupables", true, "Pickup pickupables");
            allowPickupCreature = Config.Bind<bool>("Options", "AllowPickupCreature", true, "Allow pickup creature pickupables (i.e. fish) - respects forbid list");
            allowPickupEdible = Config.Bind<bool>("Options", "AllowPickupEdible", true, "Allow pickup edibles (i.e. fish and plants) - respects forbid list");
            allowPickupEgg = Config.Bind<bool>("Options", "AllowPickupEgg", true, "Allow pickup eggs - respects forbid list");
            range = Config.Bind<float>("Options", "Range", 3f, "Range (m)");
            interval = Config.Bind<float>("Options", "Interval", 1f, "Harvest interval in seconds");
            disabledMessage = Config.Bind<string>("Text", "DisabledMessage", "Auto harvest disabled.", "Message to show when disabling mod.");
            enabledMessage = Config.Bind<string>("Text", "EnabledMessage", "Auto harvest enabled.", "Message to show when enabling mod.");

            modEnabled.SettingChanged += ModEnabled_SettingChanged;

            interval.SettingChanged += Interval_SettingChanged;
            
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
            Dbgl("Plugin awake");
            InvokeRepeating("CheckAutoHarvest", 0, interval.Value);

            ReloadTypes();
        }

        private void Interval_SettingChanged(object sender, EventArgs e)
        {
            CancelInvoke("CheckAutoHarvest");
            InvokeRepeating("CheckAutoHarvest", 0, interval.Value);
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey.Value))
            {
                modEnabled.Value = !modEnabled.Value;
                ErrorMessage.AddWarning(modEnabled.Value ? enabledMessage.Value : disabledMessage.Value);
            }
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

        private void CheckAutoHarvest()
        {
            if(!modEnabled.Value || Player.main == null || Player.main.transform == null)
                return;
            Collider[] colliders = Physics.OverlapSphere(Player.main.transform.position, range.Value);
            //Collider[] colliders = new Collider[maxHarvest.Value];
            //Physics.OverlapSphereNonAlloc(Player.main.transform.position, 100, colliders);

            //Dbgl($"Found {colliders.Length} colliders on layer 0");
            bool reset = false;
            foreach (var c in colliders)
            {
                if (breakBreakables.Value)
                {
                    BreakableResource r = c.GetComponentInParent<BreakableResource>();
                    if (!r)
                    {
                        r = c.GetComponent<BreakableResource>();
                    }
                    if (!r)
                    {
                        r = c.GetComponentInChildren<BreakableResource>();
                    }
                    if (r && IsAllowed(r.gameObject))
                    {
                        Dbgl($"Breaking {r.name} ({c.gameObject.layer})");
                        r.BreakIntoResources();
                        if (autoPickupBreakables.Value)
                        {
                            reset = true;
                        }
                        continue;
                    }
                }
                if (cutCuttables.Value)
                {
                    SpawnOnKill s = c.GetComponentInParent<SpawnOnKill>();
                    if (!s)
                    {
                        s = c.GetComponent<SpawnOnKill>();
                    }
                    if (!s)
                    {
                        s = c.GetComponentInChildren<SpawnOnKill>();
                    }
                    if (s && s.GetComponent<LiveMixin>() && IsAllowed(s.prefabToSpawn))
                    {
                        Dbgl($"Cutting {s.name} ({c.gameObject.layer})");
                        s.GetComponent<LiveMixin>().Kill();
                        if (autoPickupCuttables.Value)
                        {
                            reset = true;
                        }
                        continue;
                    }
                }
                if (pickupables.Value)
                {
                    Pickupable p = c.GetComponentInParent<Pickupable>();
                    if (!p)
                    {
                        p = c.GetComponent<Pickupable>();
                    }
                    if (!p)
                    {
                        p = c.GetComponentInChildren<Pickupable>();
                    }
                    if (p)
                    {
                        //Dbgl($"Picking up {p.name} ({c.gameObject.layer})");
                        Pickup(p);
                        continue;
                    }
                }
            }
            if (reset)
            {
                CancelInvoke("CheckAutoHarvest");
                InvokeRepeating("CheckAutoHarvest", 0.1f, interval.Value);
            }
        }

        private static void Pickup(Pickupable pickupable)
        {
            var ii = (InventoryItem)inventoryItemField.GetValue(pickupable);
            if ((ii == null || ii.container == null) && pickupable.isPickupable && (!preventPickingUpDropped.Value || AccessTools.FieldRefAccess<Pickupable, float>(pickupable, "timeDropped") == 0) && Player.main.HasInventoryRoom(pickupable) && IsAllowed(pickupable.gameObject))
            {
                //Debug.Log("Picking up " + pickupable.GetTechName());

                if (!Inventory.Get().Pickup(pickupable, false))
                {
                    ErrorMessage.AddWarning(Language.main.Get("InventoryFull"));
                    return;
                }
                Debug.Log("Picked up " + pickupable.GetTechName());
                WaterParkItem component = pickupable.GetComponent<WaterParkItem>();
                if (component != null)
                {
                    component.SetWaterPark(null);
                }
            }
        }

        private static bool IsAllowed(GameObject go)
        {
            if (!allowPickupCreature.Value && go.GetComponent<Creature>())
                return false;
            if (!allowPickupEdible.Value && go.GetComponent<Eatable>())
                return false;
            if (!allowPickupEgg.Value && (go.GetComponent<CreatureEgg>() || go.GetComponent<IncubatorEgg>()))
                return false;
            TechType type = CraftData.GetTechType(go);
            if (type == TechType.None)
                return false;

            string ts = type.ToString();
            if (forbiddenTypes.Length > 0 && Array.IndexOf(forbiddenTypes, ts) >= 0)
            {
                return false;
            }
            return allowedTypes.Length > 0 && Array.IndexOf(allowedTypes, ts) >= 0;
        }


        [HarmonyPatch(typeof(Pickupable), nameof(Pickupable.Drop), new Type[] {typeof(Vector3 ), typeof(Vector3), typeof(bool) })]
        private static class Pickupable_Drop_Patch
        {
            static void Postfix(Pickupable __instance, ref float ___timeDropped)
            {
                if (!modEnabled.Value)
                    return;

                ___timeDropped = Time.time;
            }
        }
    }
}
