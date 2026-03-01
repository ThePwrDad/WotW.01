using UnityEngine;

namespace WeightLifter
{
    public class PlayerStats : MonoBehaviour
    {
        [Header("Progress")]
        public float currentStrength = 15f;
        public bool isBusy = false; 

        [Header("Settings")]
        public float strengthGainMultiplier = 0.1f;

        public void AddStrengthFromObject(float objectWeight)
        {
            float gain = objectWeight * strengthGainMultiplier;
            currentStrength += gain;
            Debug.Log($"<color=green>STRENGTH UP!</color> Gained: {gain}. New Total: {currentStrength}");
        }
    }
}
