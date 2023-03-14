using UnityEngine;

namespace BedTeleport
{
    public class BedLabel : HandTarget, IHandTarget
    {
        private void Start()
        {
            Language main = Language.main;
            stringBeaconLabel = BepInExPlugin.labelTitle.Value;
            stringBeaconSubmit = main.Get("BeaconSubmit");
            GetLabel();
        }

        public void OnHandHover(GUIHand hand)
        {
            HandReticle main = HandReticle.main;
            main.SetText(HandReticle.TextType.Hand, BepInExPlugin.labelEdit.Value + (labelName != null ? $" ({labelName})" : ""), true, GameInput.Button.LeftHand);
            main.SetText(HandReticle.TextType.HandSubscript, string.Empty, false, GameInput.Button.None);
            main.SetIcon(HandReticle.IconType.Rename, 1f);
        }

        public void OnHandClick(GUIHand hand)
        {
            uGUI.main.userInput.RequestString(stringBeaconLabel, stringBeaconSubmit, labelName, 25, new uGUI_UserInput.UserInputCallback(SetLabel));
        }

        public void SetLabel(string label)
        {
            labelName = label;
            BepInExPlugin.SetLabel(GetComponentInParent<PrefabIdentifier>().Id, label);
        }

        public string GetLabel()
        {
            labelName = BepInExPlugin.GetLabel(GetComponentInParent<PrefabIdentifier>().Id);
            return labelName;
        }

        public BedLabel()
        {
        }

        private string stringBeaconLabel;

        private string stringBeaconSubmit;
        private string labelName;
    }
}