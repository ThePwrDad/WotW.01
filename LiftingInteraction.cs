using StarterAssets;
using UnityEngine;

namespace WeightLifter
{
    [RequireComponent(typeof(PlayerStats))]
    [RequireComponent(typeof(LiftingMiniGame))]
    [RequireComponent(typeof(ThirdPersonController))]
    public class LiftingInteraction : MonoBehaviour
    {
        private PlayerStats stats;
        private LiftingMiniGame liftingMiniGame;
        private ThirdPersonController thirdPersonController;

        private void Awake()
        {
            stats = GetComponent<PlayerStats>();
            liftingMiniGame = GetComponent<LiftingMiniGame>();
            thirdPersonController = GetComponent<ThirdPersonController>();
        }

        private void OnTriggerEnter(Collider other) => HandleTrigger(other);
        private void OnTriggerStay(Collider other) => HandleTrigger(other);

        private void HandleTrigger(Collider other)
        {
            if (stats == null || liftingMiniGame == null || stats.isBusy) return;

            WeightData cube = other.GetComponent<WeightData>();
            if (cube == null) return;

            // Allow lifting objects up to our max multiplier threshold
            float maxLiftableWeight = stats.currentStrength * liftingMiniGame.maxWeightMultiplier;

            if ((cube.weight <= maxLiftableWeight / 3f) && thirdPersonController._speed > 5f)
            {
                liftingMiniGame.SetCurrentTarget(cube);
                liftingMiniGame.CompleteLift();
            }
            else if (cube.weight <= maxLiftableWeight)
                {
                    liftingMiniGame.SetCurrentTarget(cube);
            }
            else
            {
                liftingMiniGame.ClearCurrentTarget(cube);
            }
        }
        private void OnTriggerExit(Collider other)
        {
            if (liftingMiniGame == null) return;
            WeightData cube = other.GetComponent<WeightData>();
            if (cube != null) liftingMiniGame.ClearCurrentTarget(cube);
        }
    }
}