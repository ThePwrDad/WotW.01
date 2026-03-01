using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem; // MUST HAVE THIS
using WeightLifter;

namespace WeightLifter
{
    public class LiftingMiniGame : MonoBehaviour
    {
        [Header("UI References")]
        public GameObject miniGameUI; 
        public Slider progressBar;

        [Header("Settings")]
        public float drainSpeed = 0.2f;
        public float clickPower = 0.15f;

       private bool isActive = false;
        private WeightData currentTarget;
        private PlayerStats stats;
        private bool awaitingStart = false;

        void Start()
        {
            stats = GetComponent<PlayerStats>();
            if(miniGameUI != null) miniGameUI.SetActive(false);
        }

        public void StartLifting(WeightData target)
        {
            if (isActive && awaitingStart) return;

                currentTarget = target;
                awaitingStart = true;
        }

        void Update()
        {
            // If waiting for player to press E to start the mini-game
            if (awaitingStart)
            {
                if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                {
                    isActive = true;
                    awaitingStart = false;
                    stats.isBusy = true;
                    miniGameUI.SetActive(true);
                    if (progressBar != null) progressBar.value = 0.2f;
                }
            }

            if (!isActive) return;

            // Constant drain of the progress bar (gravity effect)
            if (progressBar != null) progressBar.value -= drainSpeed * Time.deltaTime;

            // 2. NEW INPUT SYSTEM: Left Mouse Click
            // This checks if the mouse was clicked specifically this frame
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (progressBar != null) progressBar.value += clickPower;
                Debug.Log("<color=yellow>CLICK!</color> Power: " + (progressBar != null ? progressBar.value.ToString() : "N/A"));
            }

            if (progressBar != null && progressBar.value >= 1.0f)
            {
                CompleteLift();
            }

            if (progressBar != null && progressBar.value <= 0)
            {
                FailLift();
            }
        }

        void CompleteLift()
        {
            isActive = false;
            awaitingStart = false;
            stats.isBusy = false;
            miniGameUI.SetActive(false);
            stats.AddStrengthFromObject(currentTarget.weight);
            Destroy(currentTarget.gameObject);
        }

        void FailLift()
        {
            isActive = false;
            awaitingStart = false;
            stats.isBusy = false;
            miniGameUI.SetActive(false);
        }
    }
} 