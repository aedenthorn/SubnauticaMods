using UnityEngine;

namespace CyclopsSolarCharger
{
    public class CyclopsSolarChargerComponent : MonoBehaviour
    {
        public int upgradeModules;
        private void Awake()
        {
            InvokeRepeating("UpdateSolarRecharge", 1f, 1f);
        }
        private void UpdateSolarRecharge()
        {
            if (!BepInExPlugin.modEnabled.Value)
                return;
            if(upgradeModules == 0)
            {
                return;
            }
            BepInExPlugin.Dbgl($"got {upgradeModules} modules");

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
                this.GetComponent<SubRoot>().powerRelay.AddEnergy(amount, out float stored);
                BepInExPlugin.Dbgl($"Added energy: {stored}");
            }
        }
    }
}