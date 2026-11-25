using TMPro;
using UnityEngine;

namespace Siege.Tutorial
{
    /// <summary>
    /// Optional art for the onboarding tutorial. Kings of Siege > Tutorial > Build Tutorial Assets creates it at
    /// Assets/Resources/Tutorial/KoSTutorialArt.asset. Every field is optional: missing art falls back to generated shapes.
    /// </summary>
    [CreateAssetMenu(fileName = "KoSTutorialArt", menuName = "Kings of Siege/Tutorial Art")]
    public class KoSTutorialArt : ScriptableObject
    {
        [Tooltip("Advisor portrait shown beside the dialog (UIPACK Characters/king.png).")]
        public Sprite advisor;
        [Tooltip("Ribbon behind the speaker name (Label_Ribbon01_Yellow).")]
        public Sprite nameRibbon;
        [Tooltip("Pointing hand (Resources/Tutorial/kos_tutorial_hand.png).")]
        public Sprite hand;
        [Tooltip("Dialog and popup panel, 9-sliced (battlevs Panels/wide_status_panel.png).")]
        public Sprite dialogPanel;
        [Tooltip("Ribbon behind YOUR TURN on action steps (Label_Ribbon01_Green).")]
        public Sprite actionRibbon;
        [Tooltip("Sparkles that run around the highlight (battlevs Effects/gold_glow_orb.png).")]
        public Sprite sparkle;
        [Tooltip("9-sliced pill buttons (UIPACK Button01_Demo_*).")]
        public Sprite buttonYellow, buttonGreen, buttonRed, buttonNavy;
        public TMP_FontAsset font;
        public TMP_FontAsset outlineFont;
        public string advisorName = "The King";
    }
}
