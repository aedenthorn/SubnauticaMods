using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Nautilus.Handlers;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System;
using System.Reflection;
using static CraftData;
using UnityEngine;
using System.Collections;
using UnityEngine.Networking;
using Nautilus.Assets;
using Valve.VR;
using Nautilus.Assets.PrefabTemplates;
using Nautilus.Crafting;
using Nautilus.Assets.Gadgets;

namespace SimplePosters
{
    [BepInPlugin("aedenthorn.SimplePosters", "Simple Posters", "1.1.0")]
    [BepInDependency("com.snmodding.nautilus")]
    public class BepInExPlugin : BaseUnityPlugin
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
        private static Assembly Assembly { get; } = Assembly.GetExecutingAssembly();

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

            // Initialize custom prefabs
            StartCoroutine(InitializePrefabs());

            // register harmony patches, if there are any
            Harmony.CreateAndPatchAll(Assembly, $"{Info.Metadata.GUID}");
        }

        private IEnumerator InitializePrefabs()
        {
            Dbgl($"Adding posters");

            var ingredientList = new List<Ingredient>();

            foreach (var str in ingredients.Value.Split(','))
            {
                if (!str.Contains(':'))
                    continue;
                var split = str.Split(':');
                if (!int.TryParse(split[1], out var amount))
                    continue;
                if (!Enum.TryParse<TechType>(split[0], out var tech))
                    continue;
                ingredientList.Add(new Ingredient(tech, amount));
            }

            CraftTreeHandler.AddTabNode(CraftTree.Type.Fabricator, "SimplePosters", "Posters", SpriteManager.Get(iconTechType.Value));

            List<string> uniques = new List<string>();
            string root = AedenthornUtils.GetAssetPath(context, true);
            int rootPaths = root.Split(Path.DirectorySeparatorChar).Length;
            var all = Directory.GetFiles(root, "*.*", SearchOption.AllDirectories);
            var unsorted = new List<string>();
            var sorted = new Dictionary<string, List<string>>();
            foreach (var s in all)
            {
                var split = s.Split(Path.DirectorySeparatorChar).Skip(rootPaths);
                if (split.Count() > 1 && (!split.First().EndsWith("x") || !float.TryParse(split.First().Substring(0, split.First().Length - 1), NumberStyles.Any, CultureInfo.InvariantCulture, out float f)))
                {
                    List<string> list;
                    if (!sorted.TryGetValue(split.First(), out list))
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
                CraftTreeHandler.AddTabNode(CraftTree.Type.Fabricator, kvp.Key, kvp.Key, SpriteManager.Get(iconTechType.Value), new string[] { "SimplePosters" });
                foreach (var path in kvp.Value)
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
                        CreateSimplePoster(name, tex, ingredientList, scale, new string[] { "SimplePosters", kvp.Key });

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
                    CraftTreeHandler.AddTabNode(CraftTree.Type.Fabricator, "page"+i, $"Page {i}", SpriteManager.Get(iconTechType.Value), new string[] { "SimplePosters" });
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
                var strings = unsorted.Count() <= postersPerPage.Value ? new string[] { "SimplePosters" } : new string[] { "SimplePosters", "page" +  page };
                try
                {                        
                    CreateSimplePoster(name, tex, ingredientList, scale, strings);

                }
                catch
                {

                }
            }

        }

        private void CreateSimplePoster(string name, Texture2D tex, List<Ingredient> ingredientList, float scale, string[] strings)
        {
            PrefabInfo Info = PrefabInfo
                .WithTechType($"SimplePosters{name.Replace(" ", "")}", name, posterDescription.Value)
                .WithIcon(Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f)));

            CustomPrefab customPrefab = new CustomPrefab(Info);
            var posterObj = new CloneTemplate(Info, TechType.PosterAurora);

            posterObj.ModifyPrefab += go =>
            {
                go.name = name;
                var mr = go.GetComponentInChildren<MeshRenderer>();
                var oldTex = mr.materials[1].GetTexture("_MainTex");
                var oldSize = new Vector2(oldTex.width, oldTex.height);
                var newSize = new Vector2(tex.width, tex.height);
                float ratio1 = oldSize.x / oldSize.y;
                float ratio2 = newSize.x / newSize.y;
                float xScale = 1;
                float yScale = 1;
                BepInExPlugin.Dbgl($"old {oldSize}, new {newSize}, r1 {ratio1}, r2 {ratio2}");
                if (ratio2 < ratio1)
                {
                    yScale = ratio1 / ratio2;
                    if (yScale > 1.5f)
                    {
                        xScale /= yScale / 1.5f;
                        yScale = 1.5f;
                        BepInExPlugin.Dbgl($"1");
                    }
                    BepInExPlugin.Dbgl($"2");
                }
                else if (ratio1 < ratio2)
                {
                    xScale = ratio2 / ratio1;
                    BepInExPlugin.Dbgl($"3");
                }
                BepInExPlugin.Dbgl($"xs {xScale}, ys {yScale}");


                // old(512.0, 1024.0), new(1200.0, 1671.0), r1 0.5, r2 0.7181329

                // xs 1.436266, ys 1

                mr.transform.localScale = new Vector3(xScale, 1, yScale) * scale;
                mr.materials[1].SetTexture("_MainTex", tex);
                mr.materials[1].SetTexture("_SpecTex", tex);
                var bc = go.GetComponentInChildren<BoxCollider>();
                bc.size = new Vector3(bc.size.x * xScale, bc.size.y * yScale, bc.size.z) * scale;
                var cb = go.GetComponentInChildren<ConstructableBounds>();
                cb.bounds = new OrientedBounds(cb.bounds.position, cb.bounds.rotation, new Vector3(cb.bounds.extents.x * xScale, cb.bounds.extents.y * yScale, cb.bounds.extents.z) * scale);
            };
            customPrefab.SetGameObject(posterObj);
            customPrefab.SetRecipe(new RecipeData(ingredientList))
                .WithFabricatorType(CraftTree.Type.Fabricator)
                .WithStepsToFabricatorTab(strings)
                .WithCraftingTime(craftTimeMult.Value);
            customPrefab.SetEquipment(EquipmentType.Hand)
                .WithQuickSlotType(QuickSlotType.Selectable);
            customPrefab.Register();
            KnownTechHandler.UnlockOnStart(customPrefab.Info.TechType);
            Dbgl($"Added poster {name}");
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