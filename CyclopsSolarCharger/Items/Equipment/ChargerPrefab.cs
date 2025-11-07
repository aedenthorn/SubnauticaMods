using Nautilus.Assets;
using Nautilus.Assets.Gadgets;
using Nautilus.Assets.PrefabTemplates;
using Nautilus.Crafting;
using Nautilus.Extensions;
using Nautilus.Handlers;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CyclopsSolarCharger.Items.Equipment
{

    public static class ChargerPrefab
    {
        public static List<Ingredient> ingredientList = new List<Ingredient>();

        public static PrefabInfo Info { get; } = PrefabInfo
            .WithTechType(BepInExPlugin.idString, BepInExPlugin.nameString.Value, BepInExPlugin.descriptionString.Value)
            .WithIcon(SpriteManager.Get(TechType.SeamothSolarCharge));

        public static void Register()
        {
            var customPrefab = new CustomPrefab(Info);

            var scannerObj = new CloneTemplate(Info, TechType.SeamothSolarCharge);
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

            customPrefab.SetRecipe(new RecipeData(ingredientList))
                .WithFabricatorType(BepInExPlugin.fabricatorType.Value);
            customPrefab.SetEquipment(EquipmentType.CyclopsModule);
            customPrefab.Register();
            if (BepInExPlugin.requireSeamothCharger.Value)
            {
                KnownTechHandler.SetCompoundUnlock(customPrefab.Info.TechType, new List<TechType>() { TechType.SeamothSolarCharge });
            }
        }
    }

}