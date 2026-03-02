using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem; // MUST HAVE THIS
using WeightLifter;
using TMPro;

namespace WeightLifter
{
    public class LiftingMiniGame : MonoBehaviour
    {
        [Header("UI References")]
        public GameObject miniGameUI; 
        public Slider progressBar;

        [Header("UI Feedback")]
        public TMP_Text swolScoreText;
        public TMP_Text gainsText;
        public TMP_Text liftPromptText;

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
            if (swolScoreText != null) swolScoreText.text = "Swol: " + stats.currentStrength.ToString("F1");
            if (gainsText != null) gainsText.enabled = false;
            if (liftPromptText != null) liftPromptText.enabled = false;
        }

        public void StartLifting(WeightData target)
        {
            if (isActive && awaitingStart) return;
            currentTarget = target;
            awaitingStart = true;
            UpdateSwolScore();
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

            // Allow retriggering minigame by pressing E again after fail
            if (!isActive && currentTarget != null && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                awaitingStart = true;
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

            // Show lift prompt if in range and not active
            if (!isActive && currentTarget != null)
            {
                if (liftPromptText != null) liftPromptText.enabled = true;
            }
            else
            {
                if (liftPromptText != null) liftPromptText.enabled = false;
            }
        }

        void CompleteLift()
        {
            isActive = false;
            awaitingStart = false;
            stats.isBusy = false;
            miniGameUI.SetActive(false);
            stats.AbsorbObject(currentTarget.gameObject, currentTarget.weight);
            if (gainsText != null)
            {
                gainsText.text = "Gains Received, Swol Increased";
                gainsText.enabled = true;
                Invoke("HideGainsText", 2f);
            }
            UpdateSwolScore();
        }

        void FailLift()
        {
            isActive = false;
            awaitingStart = false;
            stats.isBusy = false;
            miniGameUI.SetActive(false);
            // Do not clear currentTarget, so E can retrigger
        }

        void UpdateSwolScore()
        {
            if (swolScoreText != null)
                swolScoreText.text = "Swol: " + stats.currentStrength.ToString("F1");
        }

        void HideGainsText()
        {
            if (gainsText != null) gainsText.enabled = false;
        }
    }
}