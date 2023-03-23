using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace TrackEverything
{
    [BepInPlugin("aedenthorn.TrackEverything", "Track Everything", "0.1.0")]
    public partial class BepInExPlugin : BaseUnityPlugin
    {
        private static BepInExPlugin context;

        public static ConfigEntry<bool> modEnabled;
        public static ConfigEntry<bool> isDebug;

        public static ConfigEntry<bool> allowCreature;
        public static ConfigEntry<bool> allowEdible;
        public static ConfigEntry<bool> allowPlants;

        public static string[] allowedTypes = new string[0];
        public static string[] forbiddenTypes = new string[0];
        public static string allowedFile = "allowed_types.txt";
        public static string forbiddenFile = "forbidden_types.txt";

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

            allowCreature = Config.Bind<bool>("Options", "AllowCreature", true, "Allow creatures (i.e. fish) - respects forbid list");
            allowEdible = Config.Bind<bool>("Options", "AllowEdible", true, "Allow edibles (i.e. fish and plants) - respects forbid list");
            allowPlants = Config.Bind<bool>("Options", "AllowPlants", true, "Allow plants - respects forbid list");

            ReloadTypes();

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), Info.Metadata.GUID);

            Dbgl("Plugin awake");
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
                File.WriteAllLines(a, allowedTypes);
                File.WriteAllLines(f, forbiddenTypes);
            }
            else
            {
                if (File.Exists(f))
                    forbiddenTypes = File.ReadAllLines(f);
                if (File.Exists(a))
                    allowedTypes = File.ReadAllLines(a);
            }
        }

        private static bool IsAllowed(GameObject go)
        {
            if (!allowCreature.Value && go.GetComponent<Creature>())
                return false;
            if (!allowEdible.Value && go.GetComponent<Eatable>())
                return false;
            if (!allowPlants.Value && go.GetComponent<PlantBehaviour>())
                return false;
            TechType type = CraftData.GetTechType(go);
            if (type == TechType.None)
                return false;

            string ts = type.ToString();
            if (forbiddenTypes.Length > 0 && Array.IndexOf(forbiddenTypes, ts) >= 0)
            {
                return false;
            }
            if (allowedTypes.Length > 0 && Array.IndexOf(allowedTypes, ts) < 0)
            {
                return false;
            }
            return true;
        }

        [HarmonyPatch(typeof(ResourceTrackerDatabase), "Start")]
        private static class ResourceTrackerDatabase_Start_Patch
        {
            static void Postfix(ResourceTrackerDatabase __instance, HashSet<TechType> ___undetectableTechTypes)
            {
                if (!modEnabled.Value)
                    return;
                ___undetectableTechTypes.Clear();
            }
        }

        [HarmonyPatch(typeof(LiveMixin), "Awake")]
        private static class LiveMixin_Awake_Patch
        {
            static void Prefix(LiveMixin __instance)
            {
                if (!modEnabled.Value)
                    return;
                AddTracker(__instance.gameObject);
            }

        }

        //[HarmonyPatch(typeof(Pickupable), "Awake")]
        private static class Pickupable_Awake_Patch
        {
            static void Prefix(Pickupable __instance, InventoryItem ___inventoryItem)
            {
                if (!modEnabled.Value)
                    return;
                AddTracker(__instance.gameObject);

            }
        }

        private static void AddTracker(GameObject go)
        {
            if (go.GetComponent<ResourceTracker>() || !go.GetComponent<PrefabIdentifier>() || !IsAllowed(go))
                return;
            var rt = go.AddComponent<ResourceTracker>();
            rt.pickupable = go.GetComponent<Pickupable>();
            rt.prefabIdentifier = go.GetComponent<PrefabIdentifier>();
            rt.rb = go.GetComponent<Rigidbody>();
            ResourceTrackerDatabase.Register(rt.prefabIdentifier.Id, go.transform.position, CraftData.GetTechType(go));
            if (rt.rb && !go.GetComponent<ResourceTrackerUpdater>())
            {
                go.AddComponent<ResourceTrackerUpdater>();
            }
        }
    }
}
