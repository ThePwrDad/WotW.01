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
        public float maxWeightMultiplier = 1.5f; // Objects up to 1.5x your strength are liftable

        [Header("Timing")]
        public float gainsDisplaySeconds = 2f;

        private bool isActive = false;
        private bool showingRecentGain = false;
        private bool canToggleMiniGameContainer = true;
        private float currentDrainSpeed;
        private float currentClickPower;

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

            // Prevent hiding context UI via parent toggle
            if (miniGameUI != null && contextUI != null && contextUI.transform.IsChildOf(miniGameUI.transform))
            {
                canToggleMiniGameContainer = false;
                Debug.LogWarning("LiftingMiniGame: contextUI is a child of miniGameUI. miniGameUI will not be toggled to keep prompt/score visible.");
            }

            if (contextUI != null) contextUI.SetActive(true);
            SetMiniGameVisualsActive(false);

            if (progressBar != null) progressBar.value = 0.2f;

            if (swolScoreText != null) swolScoreText.enabled = true;
            if (liftPromptText != null) liftPromptText.enabled = false;
            if (gainsText != null) gainsText.enabled = false;

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
                // --- DYNAMIC DIFFICULTY CALCULATION ---
                // Prevent divide-by-zero just in case strength is 0
                float safeStrength = Mathf.Max(0.1f, stats.currentStrength); 
                float weightRatio = currentTarget.weight / safeStrength;

                // Light objects = huge click power. Heavy objects = weak click power.
                // Clamped to 1f so super light objects can be 1-clicked, but don't break the UI.
                currentClickPower = Mathf.Clamp(clickPower / Mathf.Max(0.01f, weightRatio), 0f, 1f); 

                // Heavy objects drain faster, light objects drain slower.
                currentDrainSpeed = drainSpeed * weightRatio;
            }
            

            if (!isActive) return;

            if (progressBar != null) progressBar.value -= currentDrainSpeed * Time.deltaTime;

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (progressBar != null) progressBar.value += currentClickPower;
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

            // Cache target/gain, then clear target so prompt hides immediately
            WeightData liftedTarget = currentTarget;
            float gain = liftedTarget.weight * stats.strengthGainMultiplier;
            currentTarget = null;

            stats.AbsorbObject(liftedTarget.gameObject, liftedTarget.weight);

            // Show only post-lift gain amount
            if (gainsText != null)
            {
                gainsText.text = $"Gains: +{gain:F1}";
                gainsText.enabled = true;
                showingRecentGain = true;
                CancelInvoke(nameof(HideGainsText));
                Invoke(nameof(HideGainsText), gainsDisplaySeconds);
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
            if (contextUI != null && !contextUI.activeSelf)
                contextUI.SetActive(true);

            // Always-on Swol counter
            if (swolScoreText != null && stats != null)
            {
                swolScoreText.text = $"Swol: {stats.currentStrength:F1}";
                swolScoreText.enabled = true;
            }

            // Prompt only when liftable object is in range and player can act
            if (liftPromptText != null)
            {
                bool canLift = currentTarget != null && !isActive && stats != null && !stats.isBusy;
                liftPromptText.text = canLift ? "E for Reps!" : string.Empty;
                liftPromptText.enabled = canLift;
            }

            // Gains text only after completed lift (no preview while in range)
            if (gainsText != null && !showingRecentGain)
            {
                gainsText.text = string.Empty;
                gainsText.enabled = false;
            }
        }

        private void HideGainsText()
        {
            showingRecentGain = false;
            RefreshContextUI();
        }

        private void SetMiniGameVisualsActive(bool active)
        {
            if (canToggleMiniGameContainer && miniGameUI != null && miniGameUI != contextUI)
                miniGameUI.SetActive(active);

            if (progressBar != null)
                progressBar.gameObject.SetActive(active);
        }
    }
}