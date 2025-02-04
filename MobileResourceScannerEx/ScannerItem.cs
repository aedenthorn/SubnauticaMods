using Nautilus;
using Nautilus.Assets.PrefabTemplates;
using Nautilus.Assets;
using Nautilus.Crafting;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UWE.FreezeTime;
using Sprite = Atlas.Sprite;
using System;

namespace MobileResourceScanner
{
    internal class ScannerItem
    {
        public static List<Ingredient> ingredientList = new List<Ingredient>();

        public static void Register()
        {
            var Info = PrefabInfo.WithTechType(BepInExPlugin.idString, BepInExPlugin.nameString.Value, BepInExPlugin.descriptionString.Value).WithIcon(SpriteManager.Get(TechType.MapRoomHUDChip));
            var customPrefab = new CustomPrefab(Info);

            var chipObject = new CloneTemplate(Info, TechType.MapRoomHUDChip);
            customPrefab.SetGameObject(chipObject);

            foreach (var str in BepInExPlugin.ingredients.Value.Split(','))
            {
                if (!str.Contains(":"))
                    continue;
                var split = str.Split(':');
                if (!int.TryParse(split[1], out var amount))
                    continue;
                if (!Enum.TryParse<TechType>(split[0], out var tech))
                    continue;
                ingredientList.Add(CraftData.Ingredient(tech, amount));
            }

            // Recipe requires 1 Heat blade and 4 Coal.
            var recipe = new RecipeData();
            recipe.Ingredients
            customPrefab.SetRecipe(recipe)
                .WithFabricatorType(CraftTree.Type.Workbench);
            customPrefab.SetEquipment(EquipmentType.Hand);
            customPrefab.Register();
        }
        public static GameObject prefab;

        public ScannerItem(string classId, string friendlyName, string description) : base(classId, friendlyName, description)
        {
        }
        public override CraftTree.Type FabricatorType => BepInExPlugin.fabricatorType.Value;
        public override string[] StepsToFabricatorTab => new string[0];

        public override EquipmentType EquipmentType => EquipmentType.Chip;

        public override QuickSlotType QuickSlotType => QuickSlotType.None;

        public override float CraftingTime => 1;

        protected override Sprite GetItemSprite()
        {
            return SpriteManager.Get(TechType.MapRoomHUDChip);
        }
        protected override TechData GetBlueprintRecipe()
        {
            return new TechData(ingredientList) 
            {
                craftAmount = 1
            };
        }

        public override GameObject GetGameObject()
        {
            var go = Object.Instantiate(prefab);
            go.name = FriendlyName;
            return go;
        }
    }
}