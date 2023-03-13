using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using SMLHelper.V2.Crafting;
using SMLHelper.V2.Handlers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;

namespace SimplePosters
{
    [BepInPlugin("aedenthorn.SimplePosters", "Simple Posters", "0.2.0")]
    public partial class BepInExPlugin : BaseUnityPlugin
    {
        private static BepInExPlugin context;

        public static ConfigEntry<bool> modEnabled;
        public static ConfigEntry<bool> isDebug;
        public static ConfigEntry<TechType> iconTechType;
        public static ConfigEntry<string> posterDescription;
        public static ConfigEntry<string> ingredients;
        public static ConfigEntry<float> craftTimeMult;

        public static bool skip;

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
            iconTechType = Config.Bind<TechType>("Options", "IconTechType", TechType.PosterExoSuit1, "Icon to use for the crafter category");
            posterDescription = Config.Bind<string>("Options", "PosterDescription", "A custom poster", "Generic poster description");
            ingredients = Config.Bind<string>("Options", "Ingredients", "Titanium:1", "Required ingredients, comma separated TechType:Amount pairs");
            craftTimeMult = Config.Bind<float>("Options", "CraftTimeMult", 1f, "Craft time multiplier.");

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), Info.Metadata.GUID);
            Dbgl("Plugin awake");
            StartCoroutine(LoadPosters());
        }

        private static IEnumerator LoadPosters()
        {
            Dbgl($"Adding posters");

            CraftTreeHandler.AddTabNode(CraftTree.Type.Fabricator, "SimplePosters", "Posters", SpriteManager.Get(iconTechType.Value));

            PosterItem.ingredientList = new List<Ingredient>();

            foreach (var str in ingredients.Value.Split(','))
            {
                if (!str.Contains(':'))
                    continue;
                var split = str.Split(':');
                if (!int.TryParse(split[1], out var amount))
                    continue;
                if (!Enum.TryParse<TechType>(split[0], out var tech))
                    continue;
                PosterItem.ingredientList.Add(new Ingredient(tech, amount));
            }

            CoroutineTask<GameObject> request = CraftData.GetPrefabForTechTypeAsync(TechType.PosterAurora, false);
            yield return request;
            PosterItem.prefab = request.GetResult();

            List<string> uniques = new List<string>();
            foreach (string path in Directory.GetFiles(AedenthornUtils.GetAssetPath(context, true), "*.*", SearchOption.AllDirectories))
            {
                if (!path.EndsWith(".png") && !path.EndsWith(".jpg") && !path.EndsWith(".gif") && !path.EndsWith(".jpeg"))
                    continue;
                var name = Path.GetFileNameWithoutExtension(path);
                if (uniques.Contains(name))
                    continue;
                CoroutineTask<Texture2D> r = GetImageAsync(path, name);
                yield return r;
                var tex = r.GetResult();
                if (tex == null)
                    continue;
                float scale = GetScaleFromPath(path);
                var poster = new PosterItem(tex, scale, $"SimplePosters{name.Replace(" ", "")}", name, posterDescription.Value); // Create an instance of your class
                poster.Patch(); // Call the Patch method
                Dbgl($"Added poster {name}");
            }
            yield break;
        }

        private static float GetScaleFromPath(string path)
        {
            var folder = Path.GetFileName(Path.GetDirectoryName(path));
            if (!folder.EndsWith("x"))
                return 1;
            return float.TryParse(folder.Remove(folder.Length - 1), NumberStyles.Any, CultureInfo.InvariantCulture, out float scale) ? scale : 1;
        }

        public static CoroutineTask<Texture2D> GetImageAsync(string path, string name)
        {
            TaskResult<Texture2D> result = new TaskResult<Texture2D>();
            return new CoroutineTask<Texture2D>(GetImageAsync(path, name, result), result);
        }

        public static IEnumerator GetImageAsync(string path, string name, IOut<Texture2D> result)
        {
            Uri uri = new Uri(path);
            using (UnityWebRequest uwr = UnityWebRequestTexture.GetTexture(uri))
            {
                yield return uwr.SendWebRequest();

                DownloadHandlerTexture.GetContent(uwr);
                if (!string.IsNullOrEmpty(uwr.error))
                {
                    Debug.LogError(uwr.error);
                    result.Set(null);
                }
                else
                {
                    var tex = DownloadHandlerTexture.GetContent(uwr);
                    tex.name = name;
                    result.Set(tex);
                }
            }
            yield break;
        }

    }
}
