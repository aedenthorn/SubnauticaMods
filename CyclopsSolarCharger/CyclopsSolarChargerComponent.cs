using UnityEngine;

namespace CyclopsSolarCharger
{
    public class CyclopsSolarChargerComponent : MonoBehaviour
    {
        public int upgradeModules;
        private void Start()
        {
            InvokeRepeating("UpdateSolarRecharge", 1f, 1f);
        }
        private void UpdateSolarRecharge()
        {
            if (!BepInExPlugin.modEnabled.Value || upgradeModules == 0)
                return;

            DayNightCycle main = DayNightCycle.main;
            if (main == null)
            {
                return;
            }
            float num = Mathf.Clamp01((200f + base.transform.position.y) / 200f);
            float localLightScalar = main.GetLocalLightScalar();
            float amount = 1f * localLightScalar * num * (float)upgradeModules;
            if(amount > 0)
            {
                float num3;
                this.GetComponent<SubRoot>().powerRelay.AddEnergy(amount, out num3);
            }
        }
    }
}