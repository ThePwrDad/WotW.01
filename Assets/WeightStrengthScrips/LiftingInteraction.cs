using UnityEngine;

namespace WeightLifter
{
    [RequireComponent(typeof(PlayerStats))]
    [RequireComponent(typeof(LiftingMiniGame))]
    public class LiftingInteraction : MonoBehaviour
    {
        private PlayerStats stats;
        private LiftingMiniGame liftingMiniGame;

        private void Awake()
        {
            stats = GetComponent<PlayerStats>();
            liftingMiniGame = GetComponent<LiftingMiniGame>();
        }

        private void OnTriggerEnter(Collider other) => HandleTrigger(other);
        private void OnTriggerStay(Collider other) => HandleTrigger(other);

        private void HandleTrigger(Collider other)
        {
            if (stats == null || liftingMiniGame == null || stats.isBusy) return;

            WeightData cube = other.GetComponent<WeightData>();
            if (cube == null) return;

            if (stats.currentStrength >= cube.weight)
                liftingMiniGame.SetCurrentTarget(cube);
            else
                liftingMiniGame.ClearCurrentTarget(cube);
        }

        private void OnTriggerExit(Collider other)
        {
            if (liftingMiniGame == null) return;
            WeightData cube = other.GetComponent<WeightData>();
            if (cube != null) liftingMiniGame.ClearCurrentTarget(cube);
        }
    }
}