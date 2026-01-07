using HarmonyLib;
using System;
using UnityEngine;
using UWE;

namespace BedTeleport
{
    public class TeleportMenu : uGUI_InputGroup, uGUI_IButtonReceiver
    {
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
            //BepInExPlugin.Dbgl("Closing menu");
            Deselect();
            Destroy(gameObject);
        }
        private void OnEnable()
        {
            uGUI_LegendBar.ClearButtons();
            uGUI_LegendBar.ChangeButton(0, GameInput.FormatButton(GameInput.Button.UICancel, false), Language.main.GetFormat("Back"));
            uGUI_LegendBar.ChangeButton(1, GameInput.FormatButton(GameInput.Button.UISubmit, false), Language.main.GetFormat("ItemSelectorSelect"));
        }
        protected override void OnDisable()
        {
            //BepInExPlugin.Dbgl("Disabling menu");
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
            //BepInExPlugin.Dbgl("Deselecting menu");
            base.OnDeselect();
            FreezeTime.End(FreezeTime.Id.IngameMenu);
            Destroy(gameObject);
        }
    }
}