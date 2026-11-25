using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Siege.KingdomRoad
{
    /// <summary>
    /// One prebuilt row of the Kingdom Road timeline. Holds its reward data and references to its UI parts.
    /// Built by the editor tool; the manager only changes its state (locked / claimed / claimable).
    /// </summary>
    public class KingdomRoadRow : MonoBehaviour
    {
        [Header("Data")]
        public bool isDivisionBanner;
        public int threshold;
        public RewardKind kind;
        public string rewardTitle;
        public int amount;

        [Header("Parts")]
        public Button button;
        public Image background;
        public Image glow;
        public Image dot;
        public Image icon;
        public Image divisionBadge;
        public TMP_Text thresholdText;
        public TMP_Text titleText;
        public TMP_Text subtitleText;
        public GameObject lockIcon;
        public GameObject claimedIcon;

        public string ClaimKey => "KoS_KR_Claimed_" + threshold;

        /// <summary>Wired to the row's Button in the editor.</summary>
        public void OnTap()
        {
            var manager = GetComponentInParent<KingdomRoadManager>();
            if (manager != null) manager.Claim(this);
        }
    }
}
