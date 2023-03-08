using HarmonyLib;
using System;
using UnityEngine;
using UWE;

namespace BedTeleport
{
    public class TeleportMenu : uGUI_InputGroup, uGUI_IButtonReceiver
    {
        protected override void Awake()
        {
            base.Awake();
            interactionRaycaster = (uGUI_GraphicRaycaster)AccessTools.Field(typeof(IngameMenu), "interactionRaycaster").GetValue(IngameMenu.main);
            interactionRaycaster.updateRaycasterStatusDelegate = new uGUI_GraphicRaycaster.UpdateRaycasterStatus(UpdateRaycasterStatus);
        }

        public bool OnButtonDown(GameInput.Button button)
        {
            if (button == GameInput.Button.UIMenu)
            {
                Close();
                GameInput.ClearInput();
                return true;
            }
            return false;
        }
        public void Close()
        {
            BepInExPlugin.Dbgl("Closing menu");
            Deselect();
            Destroy(gameObject);
        }
        private uGUI_GraphicRaycaster interactionRaycaster;
        private void UpdateRaycasterStatus(uGUI_GraphicRaycaster raycaster)
        {
            if (GameInput.IsPrimaryDeviceGamepad() && !VROptions.GetUseGazeBasedCursor())
            {
                raycaster.enabled = false;
                return;
            }
            raycaster.enabled = focused;
        }
        private void OnEnable()
        {
            uGUI_LegendBar.ClearButtons();
            uGUI_LegendBar.ChangeButton(0, uGUI.FormatButton(GameInput.Button.UICancel, false, " / ", true), Language.main.GetFormat("Back"));
            uGUI_LegendBar.ChangeButton(1, uGUI.FormatButton(GameInput.Button.UISubmit, false, " / ", true), Language.main.GetFormat("ItemSelectorSelect"));
        }
        protected override void OnDisable()
        {
            BepInExPlugin.Dbgl("Disabling menu");
            base.OnDisable();
            uGUI_LegendBar.ClearButtons();
            Destroy(gameObject);
        }
        public override void OnSelect(bool lockMovement)
        {
            base.OnSelect(lockMovement);
            gameObject.SetActive(true);
            FreezeTime.Begin(FreezeTime.Id.IngameMenu);
            UWE.Utils.lockCursor = false;
        }
        public override void OnDeselect()
        {
            BepInExPlugin.Dbgl("Deselecting menu");
            base.OnDeselect();
            FreezeTime.End(FreezeTime.Id.IngameMenu);
            Destroy(gameObject);
        }
    }
}