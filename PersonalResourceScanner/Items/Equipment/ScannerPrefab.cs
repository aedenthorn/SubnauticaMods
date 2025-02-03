using Nautilus.Assets;
using Nautilus.Assets.Gadgets;
using Nautilus.Assets.PrefabTemplates;
using Nautilus.Crafting;
using Nautilus.Extensions;
using System;
using System.Collections.Generic;
using UnityEngine;
using Ingredient = CraftData.Ingredient;

namespace PersonalResourceScanner.Items.Equipment
{

    public static class ScannerPrefab
    {
        public static List<Ingredient> ingredientList = new List<Ingredient>();

        public static PrefabInfo Info { get; } = PrefabInfo
            .WithTechType(BepInExPlugin.idString, BepInExPlugin.nameString.Value, BepInExPlugin.descriptionString.Value)
            .WithIcon(SpriteManager.Get(TechType.MapRoomHUDChip));

        public static void Register()
        {
            var customPrefab = new CustomPrefab(Info);

            var scannerObj = new CloneTemplate(Info, TechType.MapRoomHUDChip);
            customPrefab.SetGameObject(scannerObj);

            foreach (var str in BepInExPlugin.ingredients.Value.Split(','))
            {
                if (!str.Contains(":"))
                    continue;
                var split = str.Split(':');
                if (!int.TryParse(split[1], out var amount))
                    continue;
                if (!Enum.TryParse<TechType>(split[0], out var tech))
                    continue;
                ingredientList.Add(new Ingredient(tech, amount));
            }

            customPrefab.SetRecipe(new RecipeData(ingredientList));
            customPrefab.SetEquipment(EquipmentType.Chip);
            customPrefab.Register();
        }
    }

}