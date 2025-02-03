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
    [BepInPlugin("aedenthorn.SimplePosters", "Simple Posters", "0.3.1")]
    public partial class BepInExPlugin : BaseUnityPlugin
    {
        private static BepInExPlugin context;

        public static ConfigEntry<bool> modEnabled;
        public static ConfigEntry<bool> isDebug;
        public static ConfigEntry<TechType> iconTechType;
        public static ConfigEntry<string> posterDescription;
        public static ConfigEntry<string> ingredients;
        public static ConfigEntry<float> craftTimeMult;
        public static ConfigEntry<int> postersPerPage;

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
            postersPerPage = Config.Bind<int>("Options", "PostersPerPage", 100, "Posters per page.");

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), Info.Metadata.GUID);
            Dbgl("Plugin awake");
            StartCoroutine(LoadPosters());
        }

        private static IEnumerator LoadPosters()
        {
            Dbgl($"Adding posters");

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

            CraftTreeHandler.AddTabNode(CraftTree.Type.Fabricator, "SimplePosters", "Posters", SpriteManager.Get(iconTechType.Value));
            
            List<string> uniques = new List<string>();
            string root = AedenthornUtils.GetAssetPath(context, true);
            int rootPaths = root.Split(Path.DirectorySeparatorChar).Length;
            var all = Directory.GetFiles(root, "*.*", SearchOption.AllDirectories);
            var unsorted = new List<string>();
            var sorted = new Dictionary<string, List<string>>();
            foreach(var s in all)
            {
                var split = s.Split(Path.DirectorySeparatorChar).Skip(rootPaths);
                if (split.Count() > 1 && (!split.First().EndsWith("x") || !float.TryParse(split.First().Substring(0, split.First().Length - 1), NumberStyles.Any, CultureInfo.InvariantCulture, out float f)))
                {
                    List<string> list;
                    if(!sorted.TryGetValue(split.First(), out list))
                    {
                        list = new List<string>();
                        sorted.Add(split.First(), list);
                    }
                    list.Add(s);
                }
                else
                {
                    unsorted.Add(s);
                }
            }
            Dbgl($"{sorted.Count} sorted");

            foreach (var kvp in sorted)
            {
                CraftTreeHandler.AddTabNode(CraftTree.Type.Fabricator, kvp.Key, kvp.Key, SpriteManager.Get(iconTechType.Value), new string[] { "SimplePosters", kvp.Key });
                foreach(var path in kvp.Value)
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
                    try
                    {
                        var poster = new PosterItem(tex, scale, $"SimplePosters{name.Replace(" ", "")}", name, posterDescription.Value, new string[] { "SimplePosters", kvp.Key }); // Create an instance of your class
                        poster.Patch(); // Call the Patch method
                        Dbgl($"Added poster {name}");
                    }
                    catch
                    {

                    }
                }
            }
            if (unsorted.Count() > postersPerPage.Value)
            {
                int pages = (int)Mathf.Ceil(unsorted.Count() / (float)postersPerPage.Value);
                for (int i = 1; i <= pages; i++)
                {
                    CraftTreeHandler.AddTabNode(CraftTree.Type.Fabricator, i + "", $"Page {i}", SpriteManager.Get(iconTechType.Value), new string[] { "SimplePosters" });
                }
            }

            for (int i = 0; i < unsorted.Count(); i++)
            {
                var path = unsorted[i];
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
                int page = (i / postersPerPage.Value) + 1;
                var strings = unsorted.Count() <= postersPerPage.Value ? new string[] { "SimplePosters" } : new string[] { "SimplePosters", page + "" };
                try
                {
                    var poster = new PosterItem(tex, scale, $"SimplePosters{name.Replace(" ", "")}", name, posterDescription.Value, strings); // Create an instance of your class
                    poster.Patch(); // Call the Patch method
                    Dbgl($"Added poster {name}");
                }
                catch
                {

                }
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
