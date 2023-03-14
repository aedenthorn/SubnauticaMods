using SMLHelper.V2.Assets;
using SMLHelper.V2.Crafting;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sprite = Atlas.Sprite;

namespace SimplePosters
{
    internal class PosterItem : Equipable
    {
        public static List<Ingredient> ingredientList;
        public static GameObject prefab;

        public float scale = 1;
        public Texture2D texture;

        public string[] stepsToFabricator;

        public PosterItem(Texture2D texture, float scale, string classId, string friendlyName, string description, string[] steps) : base(classId, friendlyName, description)
        {
            this.texture = texture;
            this.scale = scale;
            stepsToFabricator = steps;
        }
        public override CraftTree.Type FabricatorType => CraftTree.Type.Fabricator;
        public override string[] StepsToFabricatorTab => stepsToFabricator;

        public override EquipmentType EquipmentType => EquipmentType.Hand;

        public override QuickSlotType QuickSlotType => QuickSlotType.Selectable;

        public override float CraftingTime => BepInExPlugin.craftTimeMult.Value;

        protected override Sprite GetItemSprite()
        {
            return new Sprite(texture);
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
            var mr = go.GetComponentInChildren<MeshRenderer>();
            var oldTex = mr.materials[1].GetTexture("_MainTex");
            var oldSize = new Vector2(oldTex.width, oldTex.height);
            var newSize = new Vector2(texture.width, texture.height);
            float ratio1 = oldSize.x / oldSize.y;
            float ratio2 = newSize.x / newSize.y;
            float xScale = 1;
            float yScale = 1;
            if(ratio2 < ratio1)
            {
                yScale *= ratio1 / ratio2;
                if (yScale > 1.5f)
                {
                    xScale /= yScale / 1.5f;
                    yScale = 1.5f;
                }
            }
            else if(ratio1 < ratio2)
            {
                xScale *= ratio2 / ratio1;
            }
            
            mr.transform.localScale = new Vector3(xScale, 1, yScale) * scale;
            mr.materials[1].SetTexture("_MainTex", texture);
            mr.materials[1].SetTexture("_SpecTex", texture);
            return go;
        }
    }
}