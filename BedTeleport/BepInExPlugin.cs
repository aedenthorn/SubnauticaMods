using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using FMOD.Studio;
using HarmonyLib;
using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UWE;
using static HandReticle;

namespace BedTeleport
{
    [BepInPlugin("aedenthorn.BedTeleport", "BedTeleport", "0.2.0")]
    public partial class BepInExPlugin : BaseUnityPlugin
    {
        private static BepInExPlugin context;

        public static ConfigEntry<bool> modEnabled;
        public static ConfigEntry<bool> isDebug;

        public static ConfigEntry<bool> immediate;
        public static ConfigEntry<bool> playSound;
        public static ConfigEntry<bool> allowLabels;
        public static ConfigEntry<float> range;
        public static ConfigEntry<KeyCode> modHotKey;
        
        public static ConfigEntry<string> menuHeader;
        public static ConfigEntry<string> handText;
        public static ConfigEntry<string> labelTitle;
        public static ConfigEntry<string> labelEdit;
        public static Dictionary<string, string> bedLabels = new Dictionary<string, string>();
        
        private static GameObject menuGO;

        private static GameObject labelPrefab;
        private string labelsPath;

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
            immediate = Config.Bind<bool>("Options", "Immediate", false, "Skip the movement visual");
            playSound = Config.Bind<bool>("Options", "PlaySound", true, "Play teleport sound");
            allowLabels = Config.Bind<bool>("Options", "AllowLabels", true, "Allow labelling beds.");
            modHotKey = Config.Bind<KeyCode>("Options", "HotKeyMod", KeyCode.LeftShift, "Key to hold to allow teleportation.");
            range = Config.Bind<float>("Options", "Range", -1f, "Range (m)");
            menuHeader = Config.Bind<string>("Text", "MenuHeader", "Teleport", "Menu header.");
            handText = Config.Bind<string>("Text", "HandText", "Teleport", "Hover message.");
            labelTitle = Config.Bind<string>("Text", "LabelTitle", "Bed Name", "Title for label dialogue.");
            labelEdit = Config.Bind<string>("Text", "LabelEdit", "Edit Bed Name", "Title for label hover indicator.");

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), Info.Metadata.GUID);
            Dbgl("Plugin awake");

            labelsPath = Path.Combine(AedenthornUtils.GetAssetPath(context, true), "labels.json");
            ReloadLabels();
        }

        private void ReloadLabels()
        {
            if (!allowLabels.Value)
                return;
            bedLabels.Clear();
            if (File.Exists(labelsPath))
            {
                try
                {
                    bedLabels = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(labelsPath));
                }
                catch { }
            }
        }

        public static string GetLabel(string id)
        {
            return bedLabels.TryGetValue(id, out string label) ? label : null;
        }
        public static void SetLabel(string id, string label)
        {
            bedLabels[id] = label;
            File.WriteAllText(Path.Combine(AedenthornUtils.GetAssetPath(context, true), "labels.json"), JsonConvert.SerializeObject(bedLabels, Formatting.Indented));
        }

        [HarmonyPatch(typeof(Bed), nameof(Bed.Start))]
        private static class Bed_Start_Patch
        {

            static void Prefix(Bed __instance)
            {

                if (!modEnabled.Value || !allowLabels.Value)
                    return;
                context.StartCoroutine(ApplyLabel(__instance));
            }
        }
        private static IEnumerator ApplyLabel(Bed bed)
        {
            if(labelPrefab == null)
            {
                CoroutineTask<GameObject> request = CraftData.GetPrefabForTechTypeAsync(TechType.Beacon, false);
                yield return request;
                labelPrefab = request.GetResult()?.GetComponentInChildren<BeaconLabel>().gameObject;
            }
            if (labelPrefab == null)
                yield break;
            var label = Instantiate(labelPrefab, bed.transform);
            label.transform.localPosition = new Vector3(0, 1, 0);
            DestroyImmediate(label.GetComponent<BeaconLabel>());
            label.AddComponent<BedLabel>();
            Dbgl("Added label to bed");
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

        [HarmonyPatch(typeof(Bed), nameof(Bed.OnHandHover))]
        private static class Bed_OnHandHover_Patch
        {
            static bool Prefix(Bed __instance, GUIHand hand)
            {

                if (!modEnabled.Value || (modHotKey.Value != KeyCode.None && !Input.GetKey(modHotKey.Value)))
                    return true;
                if (hand.IsFreeToInteract())
                {
                    main.SetText(TextType.Hand, handText.Value , true, GameInput.Button.LeftHand);
                    main.SetText(TextType.HandSubscript, string.Empty, false, GameInput.Button.None);
                    main.SetIcon(IconType.Hand, 1f);
                }
                return false;
            }
        }
        private static void ShowTeleportMenu(Bed source)
        {
            var template = IngameMenu.main?.GetComponentInChildren<IngameMenuTopLevel>();
            if (template is null)
            {
                ErrorMessage.AddWarning("Menu template not found!");
                return;
            }
            menuGO = Instantiate(template.gameObject, uGUI.main.hud.transform);
            menuGO.name = "TeleportMenu";
            var menu = menuGO.AddComponent<TeleportMenu>();
            menu.Select();
            
            Dbgl("Created menu");

            var buttons = menuGO.GetComponentsInChildren<Button>(true);
            Button templateButton = null;
            bool first = true;
            foreach(var button in buttons)
            {
                if (first)
                {
                    templateButton = button;
                    first = false;
                }
                else Destroy(button.gameObject);
            }
            var beds = new List<Bed>();
            foreach(var bed in FindObjectsOfType<Bed>())
            {
                if(range.Value < 0 || Vector3.Distance(bed.transform.position, Player.main.transform.position) <= range.Value)
                    beds.Add(bed);
            }

            Dbgl($"Found {beds.Count} beds");
            if(beds.Count < 2)
            {

                ErrorMessage.AddWarning("No beds found!");
                return;
            }
            var headerText = menuGO.transform.Find("Header").GetComponent<TextMeshProUGUI>();
            headerText.text = menuHeader.Value;

            first = true;
            foreach(var bed in beds)
            {
                if (bed == source)
                    continue;
                if (first)
                {
                    SetupButton(templateButton.gameObject, source, bed);
                    first = false;
                }
                else
                {
                    GameObject b = Instantiate(templateButton.gameObject, templateButton.transform.parent);
                    SetupButton(b, source, bed);
                }
            }
            uGUI_INavigableIconGrid grid = menuGO.GetComponentInChildren<uGUI_INavigableIconGrid>();
            if(grid is null)
                grid = menuGO.GetComponent<uGUI_INavigableIconGrid>();
            if(grid != null)
                GamepadInputModule.current.SetCurrentGrid(grid);
        }

        private static void SetupButton(GameObject gameObject, Bed source, Bed dest)
        {
            gameObject.name = "Bed";
            var button = gameObject.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(delegate ()
            {
                context.StartCoroutine(GotoLocation(source, dest, immediate.Value));
                Destroy(menuGO);
            });
            var text = gameObject.transform.GetComponentInChildren<TextMeshProUGUI>();
            var label = dest.GetComponentInChildren<BedLabel>()?.GetLabel();
            if (!string.IsNullOrEmpty(label) && !label.EndsWith("(Clone)"))
            {
                text.text = label;
            }
            else
            {
                text.text = GetBedText(source.transform.position, dest.transform.position);
            }
            Dbgl($"Setup button {text.text}");

        }

        private static string GetBedText(Vector3 source, Vector3 dest)
        {
            int distance = (int)Mathf.Round(Vector3.Distance(dest, source));
            Vector3 direction = dest - source;
            string dir;
            var longitude = Mathf.Abs(direction.x);
            var latitude = Mathf.Abs(direction.z);
            if (direction.x < 0)
            {
                if(direction.z < 0) 
                { 
                    if(longitude > latitude * 4)
                    {
                        dir = "W";
                    }
                    else if(latitude > longitude * 4)
                    {
                        dir = "S";
                    }
                    else if(longitude > latitude * 2)
                    {
                        dir = "WSW";
                    }
                    else if(latitude > longitude * 2)
                    {
                        dir = "SSW";
                    }
                    else
                    {
                        dir = "SW";
                    }
                }
                else
                {
                    if (longitude > latitude * 4)
                    {
                        dir = "W";
                    }
                    else if (latitude > longitude * 4)
                    {
                        dir = "N";
                    }
                    else if (longitude > latitude * 2)
                    {
                        dir = "WNW";
                    }
                    else if(latitude > longitude * 2)
                    {
                        dir = "NNW";
                    }
                    else
                    {
                        dir = "NW";
                    }
                }
            }
            else
            {
                if(direction.z < 0) 
                {
                    if (longitude > latitude * 4)
                    {
                        dir = "E";
                    }
                    else if (latitude > longitude * 4)
                    {
                        dir = "S";
                    }
                    else if (longitude > latitude * 2)
                    {
                        dir = "ESE";
                    }
                    else if(latitude > longitude * 2)
                    {
                        dir = "SSE";
                    }
                    else
                    {
                        dir = "SE";
                    }
                }
                else
                {
                    if (longitude > latitude * 4)
                    {
                        dir = "E";
                    }
                    else if (latitude > longitude * 4)
                    {
                        dir = "N";
                    }
                    else if (longitude > latitude * 2)
                    {
                        dir = "ENE";
                    }
                    else if(latitude > longitude * 2)
                    {
                        dir = "NNE";
                    }
                    else
                    {
                        dir = "NE";
                    }
                }
            }
            int h = (int)Mathf.Abs(Mathf.Round(direction.y));
            var height = h > 0 ? $" ({h}m " + (direction.y > 0 ? "up" : "down") + ")" : "";
            return $"Bed {distance}m {dir}{height}";
        }


        public static IEnumerator GotoLocation(Bed sourceBed, Bed destBed, bool gotoImmediate)
        {
            Vector3 position = destBed.transform.position + new Vector3(0, 2.5f, 0);
            Vector3 dest = position;
            Vector3 vector = dest - Player.main.transform.position;
            Vector3 direction = vector.normalized;
            float magnitude = vector.magnitude;
            if (gotoImmediate)
            {
                Player.main.SetPosition(dest);
            }
            else
            {
                if(playSound.Value)
                    Player.main.teleportingLoopSound.Play();

                float num = 2.5f;
                float travelSpeed = 250f;
                if (magnitude / travelSpeed > num)
                {
                    travelSpeed = magnitude / num;
                }
                Player.main.playerController.SetEnabled(false);
                for (; ; )
                {
                    Vector3 position2 = Player.main.transform.position;
                    float magnitude2 = (dest - position2).magnitude;
                    float num2 = travelSpeed * Time.deltaTime;
                    if (magnitude2 < num2)
                    {
                        break;
                    }
                    Vector3 position3 = position2 + direction * num2;
                    Player.main.SetPosition(position3);
                    yield return CoroutineUtils.waitForNextFrame;
                }
                if (playSound.Value)
                    Player.main.teleportingLoopSound.Stop(STOP_MODE.ALLOWFADEOUT);
                Player.main.SetPosition(dest);
            }
            if (position.y > 0f)
            {
                float travelSpeed = 15f;
                new Bounds(position, Vector3.zero);
                while (!LargeWorldStreamer.main.IsWorldSettled())
                {
                    travelSpeed -= Time.deltaTime;
                    if (travelSpeed < 0f)
                    {
                        break;
                    }
                    yield return CoroutineUtils.waitForNextFrame;
                }
            }
            Player.main.OnPlayerPositionCheat();
            Player.main.SetCurrentSub(destBed.GetComponentInParent<SubRoot>(), true);
            Player.main.playerController.SetEnabled(true);
            yield break;
        }
    }
}
