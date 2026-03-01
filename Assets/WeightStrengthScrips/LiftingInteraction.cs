using UnityEngine;
using WeightLifter;

namespace WeightLifter
{
    public class LiftingInteraction : MonoBehaviour
    {
        private PlayerStats stats;

        void Start()
        {
            stats = GetComponent<PlayerStats>();
        }

        private void OnTriggerEnter(Collider other)
        {
            // --- PLACE IT HERE ---
            Debug.Log("SENSING SOMETHING: " + other.gameObject.name);
            // ---------------------

            if (stats.isBusy) return;

            WeightData cube = other.gameObject.GetComponent<WeightData>();

            if (cube != null)
            {
                if (stats.currentStrength >= cube.weight)
                {
                    Debug.Log("<color=cyan>STARTING GAME:</color> " + other.gameObject.name);
                    GetComponent<LiftingMiniGame>().StartLifting(cube);
                }
                else
                {
                    Debug.Log("<color=red>TOO HEAVY:</color> " + cube.weight + "kg");
                }
            }
        }
    }
}
