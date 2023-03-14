using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using FMOD.Studio;
using HarmonyLib;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UWE;
using static HandReticle;

namespace CyclopsAdditions
{
    [BepInPlugin("aedenthorn.CyclopsAdditions", "CyclopsAdditions", "0.1.0")]
    public partial class BepInExPlugin : BaseUnityPlugin
    {
        private static BepInExPlugin context;

        public static ConfigEntry<bool> modEnabled;
        public static ConfigEntry<bool> isDebug;

        public static ConfigEntry<KeyCode> modHotKey;
        
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
            modHotKey = Config.Bind<KeyCode>("Options", "HotKeyMod", KeyCode.X, "Key to press to add base.");

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), Info.Metadata.GUID);
            Dbgl("Plugin awake");

        }

        private void Update()
        {
            if (!modEnabled.Value || Player.main?.currentSub == null)
                return;
            if (Input.GetKeyDown(modHotKey.Value))
            {
                StartCoroutine(StartBuild());
            }
        }

        private static IEnumerator StartBuild()
        {
            CoroutineTask<GameObject> request = CraftData.GetPrefabForTechTypeAsync(TechType.BaseRoom, true);
            yield return request;

            var prefab = request.GetResult();

            var ghost = Instantiate(prefab).GetComponent<ConstructableBase>().model;
            ConstructableBase cbase = ghost.GetComponentInParent<ConstructableBase>();

            BaseGhost component = ghost.GetComponent<BaseGhost>();
            component.TargetBase = 
            component.Place();
            if (component.TargetBase != null)
            {
                componentInParent.transform.SetParent(component.TargetBase.transform, true);
            }
            componentInParent.SetState(false, true);

            var subBase = Player.main.currentSub.GetModulesRoot().parent;

            yield break;
        }

        [HarmonyPatch(typeof(Bed), nameof(Bed.OnHandClick))]
        private static class Bed_OnHandClick_Patch
        {
            static bool Prefix(Bed __instance, GUIHand hand)
            {

                if (!modEnabled.Value || (modHotKey.Value != KeyCode.None && !Input.GetKey(modHotKey.Value)))
                    return true;
                ShowTeleportMenu(__instance);
                return false;
            }
        }
    }
}
