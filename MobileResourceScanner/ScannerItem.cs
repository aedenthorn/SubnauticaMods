using SMLHelper.V2.Assets;
using SMLHelper.V2.Crafting;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sprite = Atlas.Sprite;

namespace MobileResourceScanner
{
    internal class ScannerItem : Equipable
    {
        public static List<Ingredient> ingredientList = new List<Ingredient>();
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