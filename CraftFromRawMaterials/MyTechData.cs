using System.Collections.Generic;

namespace CraftFromRawMaterials
{
    internal class MyTechData : ITechData
    {
        public int amount;
        public TechType _techType;
        public List<IIngredient> _ingredients = new List<IIngredient>();
        public List<TechType> _linkedItems = new List<TechType>();

        public int craftAmount => amount;

        public int ingredientCount => _ingredients.Count;

        public int linkedItemCount => _linkedItems.Count;

        public IIngredient GetIngredient(int index)
        {
            if (_ingredients == null || index >= _ingredients.Count || index < 0)
            {
                return nullIngredient;
            }
            return _ingredients[index];
        }

        public TechType GetLinkedItem(int index)
        {
            throw new System.NotImplementedException();
        }

        private static readonly IIngredient nullIngredient = new MyIngredient(TechType.None, 0);

        public MyTechData(ITechData result)
        {
            for(int i = 0; i < result.linkedItemCount; i++)
            {
                _linkedItems.Add(result.GetLinkedItem(i));
            }
            amount = result.craftAmount;
        }
    }
}