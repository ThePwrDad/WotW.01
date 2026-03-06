using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

namespace WeightLifter
{
    public class LiftingMiniGame : MonoBehaviour
    {
        [Header("UI References")]
        public GameObject miniGameUI;      // Optional container
        public GameObject contextUI;       // Prompt + score + gains
        public Slider progressBar;         // Gameplay-only visual

        [Header("UI Feedback")]
        public TMP_Text swolScoreText;
        public TMP_Text gainsText;
        public TMP_Text liftPromptText;

        [Header("Settings")]
        public float drainSpeed = 0.2f;
        public float clickPower = 0.15f;

        private bool isActive = false;
        private bool showingRecentGain = false;

        private WeightData currentTarget;
        public WeightData CurrentTarget => currentTarget;

        private PlayerStats stats;

        public void SetCurrentTarget(WeightData target)
        {
            currentTarget = target;
            RefreshContextUI();
        }

        public void ClearCurrentTarget(WeightData target)
        {
            if (currentTarget == target)
            {
                currentTarget = null;
                RefreshContextUI();
            }
        }

        private void Start()
        {
            stats = GetComponent<PlayerStats>();

            // Keep context visible at all times
            if (contextUI != null) contextUI.SetActive(true);

            // Do NOT hard-disable shared containers; only hide gameplay widgets
            SetMiniGameVisualsActive(false);

            if (progressBar != null) progressBar.value = 0.2f;

            RefreshContextUI();
        }

        private void Update()
        {
            RefreshContextUI();

            if (!isActive && currentTarget != null && stats != null && !stats.isBusy &&
                Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                isActive = true;
                stats.isBusy = true;
                SetMiniGameVisualsActive(true);
                if (progressBar != null) progressBar.value = 0.2f;
            }

            if (!isActive) return;

            if (progressBar != null) progressBar.value -= drainSpeed * Time.deltaTime;

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (progressBar != null) progressBar.value += clickPower;
            }

            if (progressBar != null && progressBar.value >= 1.0f) CompleteLift();
            if (progressBar != null && progressBar.value <= 0f) FailLift();
        }

        private void CompleteLift()
        {
            if (currentTarget == null || stats == null) { FailLift(); return; }

            isActive = false;
            stats.isBusy = false;
            SetMiniGameVisualsActive(false);

            stats.AbsorbObject(currentTarget.gameObject, currentTarget.weight);

            float gain = currentTarget.weight * stats.strengthGainMultiplier;
            if (gainsText != null)
            {
                gainsText.text = $"Gains: +{gain:F1}";
                gainsText.enabled = true;
                showingRecentGain = true;
                CancelInvoke(nameof(HideGainsText));
                Invoke(nameof(HideGainsText), 2f);
            }

            RefreshContextUI();
        }

        private void FailLift()
        {
            isActive = false;
            if (stats != null) stats.isBusy = false;
            SetMiniGameVisualsActive(false);
            RefreshContextUI();
        }

        private void RefreshContextUI()
        {
            if (swolScoreText != null && stats != null)
                swolScoreText.text = $"Swol: {stats.currentStrength:F1}";

            if (liftPromptText != null)
            {
                bool canLift = currentTarget != null && !isActive && stats != null && !stats.isBusy;
                liftPromptText.text = canLift ? "Press E for Reps" : string.Empty;
                liftPromptText.enabled = canLift;
            }

            if (gainsText != null && !showingRecentGain)
            {
                if (currentTarget != null && stats != null)
                {
                    float preview = currentTarget.weight * stats.strengthGainMultiplier;
                    gainsText.text = $"Gains: +{preview:F1}";
                    gainsText.enabled = true;
                }
                else
                {
                    gainsText.text = string.Empty;
                    gainsText.enabled = false;
                }
            }
        }

        private void HideGainsText()
        {
            showingRecentGain = false;
            RefreshContextUI();
        }

        private void SetMiniGameVisualsActive(bool active)
        {
            // If miniGameUI is a dedicated gameplay panel, toggle it.
            // If miniGameUI also contains context text, keep it active and only toggle progressBar.
            if (miniGameUI != null && miniGameUI != contextUI)
                miniGameUI.SetActive(active);

            if (progressBar != null)
                progressBar.gameObject.SetActive(active);
        }
    }
}