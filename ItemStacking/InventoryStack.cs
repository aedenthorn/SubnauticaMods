using HarmonyLib;

namespace ItemStacking
{
    public class InventoryStack : InventoryItem, IInventoryStack
    {
        public InventoryStack(InventoryItem item, int count, int w, int h) : base(w, h)
        {
            this.Count = count;
            
            AccessTools.Property(typeof(InventoryItem), nameof(InventoryItem.item)).SetValue(this, item.item);
            AccessTools.Field(typeof(InventoryItem), "_techType").SetValue(this, item.item.GetTechType());
            AccessTools.Field(typeof(Pickupable), "inventoryItem").SetValue(item.item, this);
        }

        private int count;

        public int Count { get => this.count; set => this.count = value; }
    }
}