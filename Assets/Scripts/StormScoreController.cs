using UnityEngine;

namespace Lotusim
{
    public class StormScoreController : MonoBehaviour
    {
        [Header("References")]
        public StormController stormController;
        public ScoreManager scoreManager;

        [Header("Scoring")]
        [Tooltip("Flat points awarded each time the storm is triggered on.")]
        public int stormTriggerBonus = 10;

        private bool wasStormActive = false;

        void Update()
        {
            if (stormController == null || scoreManager == null) return;

            bool isActive = stormController.StormActive;

            if (isActive && !wasStormActive)
            {
                scoreManager.AddScore(stormTriggerBonus);
                Debug.Log($"[StormScoreController] Storm triggered! +{stormTriggerBonus}.");
            }

            wasStormActive = isActive;
        }
    }
}