namespace CraftFromRawIngredients
{
    public class MyIngredient : IIngredient
    {
        public TechType _techType;

        public int _amount;
        public MyIngredient(TechType techType, int amount = 1)
        {
            _techType = techType;
            _amount = amount;
        }

        public TechType techType => _techType;

        public int amount => _amount;
    }
}