using UnityEngine;

namespace Lotusim
{
    public class ScoreManager : MonoBehaviour
    {
        public static ScoreManager Instance { get; private set; }

        [SerializeField] private int score = 0;
        public int Score => score;

        public event System.Action<int> OnScoreChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void AddScore(int amount)
        {
            score += amount;
            OnScoreChanged?.Invoke(score);
        }

        public void AddScoreMultiplied(int baseAmount, float multiplier)
        {
            score += Mathf.RoundToInt(baseAmount * multiplier);
            OnScoreChanged?.Invoke(score);
        }
    }
}