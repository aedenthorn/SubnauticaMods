using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace ImageReplace
{
    [BepInPlugin("aedenthorn.ImageReplace", "Image Replace", "0.3.1")]
    public partial class BepInExPlugin : BaseUnityPlugin
    {
        private static BepInExPlugin context;

        public static ConfigEntry<bool> modEnabled;
        public static ConfigEntry<bool> isDebug;

        public static ConfigEntry<KeyboardShortcut> dumpHotKey;
        public static ConfigEntry<KeyboardShortcut> hotKey;
        
        public static Dictionary<string, List<string>> imagePaths;
        public static Dictionary<string, Texture2D> cachedTextures = new Dictionary<string, Texture2D>();
        public static Dictionary<string, TextureData> originalTextures = new Dictionary<string, TextureData>();
        public static List<Image> foundImages = new List<Image>();

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
            isDebug = Config.Bind<bool>("General", "IsDebug", false, "Enable debug logs");
            hotKey = Config.Bind<KeyboardShortcut>("Options", "HotKey", new KeyboardShortcut(KeyCode.F5), "Key to press to reload images.");
            dumpHotKey = Config.Bind<KeyboardShortcut>("Options", "DumpHotKey", new KeyboardShortcut(KeyCode.PageUp), "Key to press to dump image paths.");

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), Info.Metadata.GUID);
            Dbgl("Plugin awake");
            ReloadImagePaths();
        }

        private void Update()
        {
            if (!modEnabled.Value)
                return;
            if (hotKey.Value.IsDown())
            {
                cachedTextures.Clear();
                StartCoroutine(ReplaceImages());
            }
            else if (dumpHotKey.Value.IsDown())
            {
                StartCoroutine(DumpImages());
            }

        }
        private static IEnumerator DumpImages()
        {
            Directory.CreateDirectory(Path.Combine(AedenthornUtils.GetAssetPath(context, true), "Originals"));
            foreach (var key in originalTextures.Keys.ToArray())
            {
                if (originalTextures[key].texture == null)
                    continue;
                var path = Path.Combine(AedenthornUtils.GetAssetPath(context, true), "Originals", $"{key}.png");
                if(!File.Exists(path))
                    File.WriteAllBytes(path, AedenthornUtils.EncodeToPNG(originalTextures[key].texture));
            }
            yield break;
        }
        private static IEnumerator ReplaceImages()
        {
                
            yield return ReloadImagePaths();
            var images = FindObjectsOfType<Image>();
            var count = foundImages.Count;
            Dbgl($"found {images.Length} images");
            foreach (var image in images)
            {
                if (image == null || image.sprite?.texture?.name == null)
                    continue;
                if(!foundImages.Contains(image))
                    foundImages.Add(image);
            }
            Dbgl($"{foundImages.Count - count} new images");
            for (int i = foundImages.Count - 1; i >= 0; i--)
            {
                var image = foundImages[i];
                if (image == null || image.sprite?.texture?.name == null)
                {
                    foundImages.RemoveAt(i);
                    continue;
                }
                yield return TryReplaceImage(image);
            }
            yield break;
        }

        private static IEnumerator TryReplaceImage(Image image)
        {
            var name = image.sprite.texture.name;
            if (string.IsNullOrEmpty(name))
                yield break;
            if (!originalTextures.ContainsKey(name))
            {
                originalTextures[name] = new TextureData()
                {
                    texture = image.sprite.texture,
                    width = image.sprite.texture.width, 
                    height = image.sprite.texture.height,
                    rect = image.sprite.rect
                };
            }
            if(imagePaths == null)
            {
                yield return ReloadImagePaths();
            }
            if ((!imagePaths.TryGetValue(name, out var paths) && !imagePaths.TryGetValue(image.name + "_" + name, out paths)) || paths.Count < 1)
            {
                yield break;
            }
            var path = paths.Count > 1 ? paths[Random.Range(0, paths.Count)] : paths[0];
            //Dbgl($"Replacing {image.name} texture {name}");

            if (!cachedTextures.TryGetValue(path, out var tex))
            {
                Uri uri = new Uri(path);
                using (UnityWebRequest uwr = UnityWebRequestTexture.GetTexture(uri))
                {
                    yield return uwr.SendWebRequest();

                    DownloadHandlerTexture.GetContent(uwr);
                    if (!string.IsNullOrEmpty(uwr.error))
                    {
                        Debug.LogError(uwr.error);
                    }
                    else
                    {
                        tex = DownloadHandlerTexture.GetContent(uwr);
                        tex.name = name;
                        cachedTextures[path] = tex;
                    }
                }
            }
            skip = true;
            image.sprite = Sprite.Create(tex, GetRect(originalTextures[name].rect, originalTextures[name].width, originalTextures[name].height, tex), new Vector2(0, 0));
            skip = false;
            //Dbgl($"Replaced {image.name} texture {name}");
            yield break;
        }

        private static Rect GetRect(Rect rect, int width, int height, Texture2D tex)
        {
            if (width == tex.width && height == tex.height)
                return rect;
            float sizeDiff = Math.Min(tex.width / (float)width, tex.height / (float)height);
            Rect newRect = new Rect((int)(rect.x * sizeDiff), (int)(rect.y * sizeDiff), (int)(rect.width * sizeDiff), (int)(rect.height * sizeDiff));
            //Dbgl($"Rect {rect}; source {width}x{height}; dest {tex.width}x{tex.height}; size diff {sizeDiff}; new rect {newRect}");
            return newRect;
        }

        private static IEnumerator ReloadImagePaths()
        {
            imagePaths = new Dictionary<string, List<string>>();
            foreach(string path in Directory.GetFiles(AedenthornUtils.GetAssetPath(context, true), "*.*", SearchOption.AllDirectories))
            {
                if (path.Contains("Originals") || (!path.EndsWith(".png") && !path.EndsWith(".jpg") && !path.EndsWith(".gif") && !path.EndsWith(".jpeg")))
                    continue;
                string name;
                if (Path.GetDirectoryName(path).EndsWith("_"))
                {
                    name = Path.GetFileName(Path.GetDirectoryName(path));
                    name = name.Remove(name.Length - 1);
                }
                else
                {
                    name = Path.GetFileNameWithoutExtension(path);
                    if (name.Contains("_"))
                    {
                        var split = name.Split('_');
                        if (int.TryParse(split[1], out var i))
                            name = split[0];
                    }
                }
                if (!imagePaths.TryGetValue(name, out var paths))
                {
                    imagePaths[name] = new List<string>();
                }

                //Dbgl($"adding {name}");
                imagePaths[name].Add(path);
            }
            yield break;
        }
        [HarmonyPatch(typeof(Image), "OnEnable")]
        private static class Image_OnEnable_Patch
        {
            static void Postfix(Image __instance)
            {
                if (!modEnabled.Value)
                    return;
                if(__instance.sprite?.texture?.name != null)
                {
                    context.StartCoroutine(TryReplaceImage(__instance));
                }
            }
        }
        [HarmonyPatch(typeof(Image), "sprite")]
        [HarmonyPatch(MethodType.Setter)]
        private static class Image_sprite_Patch
        {
            static void Postfix(Image __instance)
            {
                if (!modEnabled.Value || skip)
                    return;
                if(__instance.sprite?.texture?.name != null)
                {
                    context.StartCoroutine(TryReplaceImage(__instance));
                }
            }
        }
    }
}
