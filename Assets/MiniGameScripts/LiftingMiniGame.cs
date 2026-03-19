using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using StarterAssets;
using System.Collections;

namespace WeightLifter
{
    public class LiftingMiniGame : MonoBehaviour
    {
        [Header("UI References")]
        public GameObject miniGameUI;      // Optional container
        public GameObject contextUI;       // Prompt + score + gains
        public Slider progressBar;         // Gameplay-only visual

        [Header("Lift Pose")]
        [Tooltip("Optional grab point near the hands. Auto-falls back to right hand/chest/front of player.")]
        public Transform holdStartAnchor;
        [Tooltip("Optional chest target. Auto-falls back to chest bone or player chest area.")]
        public Transform holdChestAnchor;
        [Tooltip("How quickly held objects follow the target point while lifting.")]
        public float holdFollowSpeed = 18f;

        [Header("Drop Tuning")]
        [Tooltip("How far in front of the player dropped objects are placed to avoid capsule overlap.")]
        public float dropForwardDistance = 1.1f;
        [Tooltip("Vertical offset used when placing dropped objects in front of the player.")]
        public float dropUpOffset = 0.45f;
        [Tooltip("Seconds to ignore player-object collisions right after dropping to prevent bounce/pop.")]
        public float dropIgnoreCollisionSeconds = 0.35f;

        [Header("UI Feedback")]
        public TMP_Text swolScoreText;
        public TMP_Text gainsText;
        public TMP_Text liftPromptText;
        [Tooltip("Shown only while lifting a heavy object that locks movement.")]
        public TMP_Text heavyLiftMessageText;

        [Header("Settings")]
        public float drainSpeed = 0.2f;
        public float clickPower = 0.15f;
        [Tooltip("If object weight / player strength is above this ratio, movement is locked while lifting.")]
        [Range(0f, 1f)] public float movementLockWeightRatio = 0.35f;
        // CHANGE: Raised from 1.5 to 3.0. The original cap meant objects heavier than
        // 1.5x the player's strength were silently unliftable with no feedback. Raising
        // it to 3x supports the intended design of absorbing progressively larger objects.
        // The dynamic difficulty system (weightRatio) already makes heavier objects harder
        // to lift via faster drain and reduced click power, so no separate difficulty cap
        // is needed at this multiplier.
        public float maxWeightMultiplier = 3f;

        [Header("Timing")]
        public float gainsDisplaySeconds = 2f;

        [Header("Messaging")]
        public string heavyLiftMessage = "Heavy Object, Keeping good Form";

        private bool isActive = false;
        private bool showingRecentGain = false;
        private bool canToggleMiniGameContainer = true;
        private float currentDrainSpeed;
        private float currentClickPower;
        private int consecutiveFailedLifts;

        private WeightData heldTarget;
        private Transform heldTransform;
        private Rigidbody heldBody;
        private Collider[] heldColliders;
        private bool[] heldColliderEnabledStates;
        private bool heldBodyWasKinematic;
        private bool heldBodyUsedGravity;
        private Transform heldOriginalParent;

        private WeightData currentTarget;
        public WeightData CurrentTarget => currentTarget;

        private PlayerStats stats;
        private Animator animator;
        private ThirdPersonController movementController;
        private bool lockMovementForCurrentLift;
        private Collider[] playerColliders;
        private CharacterController playerController;
        private bool useRightHand = true;

        public void SetCurrentTarget(WeightData target)
        {
            if (isActive) return;
            currentTarget = target;
            RefreshContextUI();
        }

        public void ClearCurrentTarget(WeightData target)
        {
            if (isActive && target == heldTarget) return;
            if (currentTarget == target)
            {
                currentTarget = null;
                RefreshContextUI();
            }
        }

        private void Start()
        {
            stats = GetComponent<PlayerStats>();
            animator = GetComponentInChildren<Animator>();
            movementController = GetComponent<ThirdPersonController>();
            playerColliders = GetComponentsInChildren<Collider>();
            playerController = GetComponent<CharacterController>();

            // If contextUI is nested inside miniGameUI, toggling the container would hide
            // the prompt/score, so we disable that toggle. Both layouts are supported.
            if (miniGameUI != null && contextUI != null && contextUI.transform.IsChildOf(miniGameUI.transform))
                canToggleMiniGameContainer = false;

            if (contextUI != null) contextUI.SetActive(true);
            SetMiniGameVisualsActive(false);

            if (progressBar != null) progressBar.value = 0.2f;

            if (swolScoreText != null) swolScoreText.enabled = true;
            if (liftPromptText != null) liftPromptText.enabled = false;
            if (gainsText != null) gainsText.enabled = false;
            if (heavyLiftMessageText != null) heavyLiftMessageText.enabled = false;

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
                GrabTarget(currentTarget);
                SetMiniGameVisualsActive(true);
                if (progressBar != null) progressBar.value = 0.2f;
                // --- DYNAMIC DIFFICULTY CALCULATION ---
                // Prevent divide-by-zero just in case strength is 0
                float safeStrength = Mathf.Max(0.1f, stats.currentStrength); 
                float weightRatio = currentTarget.weight / safeStrength;

                lockMovementForCurrentLift = weightRatio > movementLockWeightRatio;
                SetMovementLock(lockMovementForCurrentLift);

                // Light objects = huge click power. Heavy objects = weak click power.
                // Clamped to 1f so super light objects can be 1-clicked, but don't break the UI.
                currentClickPower = Mathf.Clamp(clickPower / Mathf.Max(0.01f, weightRatio), 0f, 1f); 

                // Heavy objects drain faster, light objects drain slower.
                currentDrainSpeed = drainSpeed * weightRatio;
            }
            

            if (!isActive) return;

            if (progressBar != null) progressBar.value -= currentDrainSpeed * Time.deltaTime;

            UpdateHeldObjectPose();

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (progressBar != null) progressBar.value += currentClickPower;
                UpdateHeldObjectPose();
            }

            if (progressBar != null && progressBar.value >= 1.0f) CompleteLift();
            if (progressBar != null && progressBar.value <= 0f) FailLift();
        }

        private void CompleteLift()
        {
            WeightData liftedTarget = heldTarget != null ? heldTarget : currentTarget;
            if (liftedTarget == null || stats == null) { FailLift(); return; }

            consecutiveFailedLifts = 0;
            isActive = false;
            SetMovementLock(false);
            // CHANGE: Removed stats.isBusy = false from here. Previously this cleared isBusy
            // before AbsorbRoutine even started (StartCoroutine queues the coroutine for next
            // frame), meaning isBusy was effectively false during the absorb animation and
            // objects entering the trigger zone during that window were never registered.
            // Ownership of isBusy=false now belongs exclusively to PlayerStats.AbsorbRoutine,
            // which clears it at the true end of the absorb animation, then immediately calls
            // RescanOverlapping to catch anything that was missed.
            SetMiniGameVisualsActive(false);
            FinalizeHeldForAbsorb();

            // Cache target/gain, then clear target so prompt hides immediately
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
            WeightData failedTarget = heldTarget != null ? heldTarget : currentTarget;

            if (failedTarget != null && stats != null)
            {
                consecutiveFailedLifts++;
                if (consecutiveFailedLifts >= 2)
                {
                    float potentialGain = failedTarget.weight * stats.strengthGainMultiplier;
                    float penalty = potentialGain * 0.5f;
                    stats.currentStrength = Mathf.Max(0.1f, stats.currentStrength - penalty);
                    ShowFormBreakFeedback(penalty);
                    consecutiveFailedLifts = 0;
                }
            }

            isActive = false;
            if (stats != null) stats.isBusy = false;
            SetMovementLock(false);
            ReleaseHeldObject(true);
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

            UpdateHeavyLiftMessage();
        }

        private void HideGainsText()
        {
            showingRecentGain = false;
            RefreshContextUI();
        }

        private void ShowFormBreakFeedback(float penalty)
        {
            if (gainsText == null) return;

            gainsText.text = $"Form Break: -{penalty:F1} Swol";
            gainsText.enabled = true;
            showingRecentGain = true;
            CancelInvoke(nameof(HideGainsText));
            Invoke(nameof(HideGainsText), gainsDisplaySeconds);
        }

        private void SetMiniGameVisualsActive(bool active)
        {
            if (canToggleMiniGameContainer && miniGameUI != null && miniGameUI != contextUI)
                miniGameUI.SetActive(active);

            if (progressBar != null)
                progressBar.gameObject.SetActive(active);
        }

        private void GrabTarget(WeightData target)
        {
            if (target == null) return;

            useRightHand = !useRightHand; // alternate hand each lift
            heldTarget = target;
            heldTransform = target.transform;
            heldOriginalParent = heldTransform.parent;

            heldBody = target.GetComponent<Rigidbody>();
            if (heldBody != null)
            {
                heldBodyWasKinematic = heldBody.isKinematic;
                heldBodyUsedGravity = heldBody.useGravity;
                heldBody.linearVelocity = Vector3.zero;
                heldBody.angularVelocity = Vector3.zero;
                heldBody.isKinematic = true;
                heldBody.useGravity = false;
            }

            heldColliders = target.GetComponentsInChildren<Collider>();
            heldColliderEnabledStates = new bool[heldColliders.Length];
            for (int i = 0; i < heldColliders.Length; i++)
            {
                heldColliderEnabledStates[i] = heldColliders[i].enabled;
                heldColliders[i].enabled = false;
            }

            heldTransform.SetParent(null, true);
            heldTransform.position = GetHoldStartPosition();
        }

        private void UpdateHeldObjectPose()
        {
            if (heldTransform == null) return;

            float progress = progressBar != null ? Mathf.Clamp01(progressBar.value) : 0f;
            Vector3 targetPos = Vector3.Lerp(GetHoldStartPosition(), GetHoldChestPosition(), progress);
            heldTransform.position = Vector3.Lerp(heldTransform.position, targetPos, Time.deltaTime * holdFollowSpeed);
        }

        private void FinalizeHeldForAbsorb()
        {
            if (heldTransform == null) return;

            // Only zero velocity if the body is not kinematic; Unity 6+ throws if you
            // set velocity on a kinematic rigidbody (which it is, set during GrabTarget).
            if (heldBody != null && !heldBody.isKinematic)
            {
                heldBody.linearVelocity = Vector3.zero;
                heldBody.angularVelocity = Vector3.zero;
            }

            heldTransform.SetParent(heldOriginalParent, true);

            // Keep colliders disabled while the absorb animation is running to avoid
            // collisions/trigger churn while the object flies into the player.
            ClearHeldReferences();
        }

        private void ReleaseHeldObject(bool restoreParent)
        {
            if (heldTransform == null)
            {
                ClearHeldReferences();
                return;
            }

            if (restoreParent)
                heldTransform.SetParent(heldOriginalParent, true);

            PositionDroppedObjectAwayFromPlayer();

            if (heldColliders != null && playerColliders != null && dropIgnoreCollisionSeconds > 0f)
            {
                StartCoroutine(TemporarilyIgnorePlayerCollision(heldColliders, dropIgnoreCollisionSeconds));
            }

            if (heldColliders != null)
            {
                for (int i = 0; i < heldColliders.Length; i++)
                {
                    if (heldColliders[i] != null)
                        heldColliders[i].enabled = heldColliderEnabledStates[i];
                }
            }

            if (heldBody != null)
            {
                heldBody.isKinematic = heldBodyWasKinematic;
                heldBody.useGravity = heldBodyUsedGravity;
            }

            ClearHeldReferences();
        }

        private void PositionDroppedObjectAwayFromPlayer()
        {
            if (heldTransform == null) return;

            float separation = dropForwardDistance;
            if (playerController != null)
                separation = Mathf.Max(separation, playerController.radius + 0.35f);

            Vector3 dropPos = transform.position + transform.forward * separation + transform.up * dropUpOffset;
            heldTransform.position = dropPos;
        }

        private IEnumerator TemporarilyIgnorePlayerCollision(Collider[] droppedColliders, float seconds)
        {
            for (int i = 0; i < droppedColliders.Length; i++)
            {
                Collider dropped = droppedColliders[i];
                if (dropped == null) continue;

                for (int j = 0; j < playerColliders.Length; j++)
                {
                    Collider playerCol = playerColliders[j];
                    if (playerCol == null || playerCol == dropped) continue;
                    Physics.IgnoreCollision(playerCol, dropped, true);
                }
            }

            yield return new WaitForSeconds(seconds);

            for (int i = 0; i < droppedColliders.Length; i++)
            {
                Collider dropped = droppedColliders[i];
                if (dropped == null) continue;

                for (int j = 0; j < playerColliders.Length; j++)
                {
                    Collider playerCol = playerColliders[j];
                    if (playerCol == null || playerCol == dropped) continue;
                    Physics.IgnoreCollision(playerCol, dropped, false);
                }
            }
        }

        private void ClearHeldReferences()
        {
            heldTarget = null;
            heldTransform = null;
            heldBody = null;
            heldColliders = null;
            heldColliderEnabledStates = null;
            heldOriginalParent = null;
        }

        private Vector3 GetHoldStartPosition()
        {
            if (holdStartAnchor != null) return holdStartAnchor.position;

            if (animator != null)
            {
                HumanBodyBones handBone = useRightHand ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand;
                Transform hand = animator.GetBoneTransform(handBone);
                if (hand != null) return hand.position;
            }

            // Fallback: offset to the right or left of the player forward+up point
            float sideOffset = useRightHand ? 0.35f : -0.35f;
            return transform.position + transform.forward * 0.8f + transform.up * 1.0f + transform.right * sideOffset;
        }

        private Vector3 GetHoldChestPosition()
        {
            float sideOffset = useRightHand ? 0.15f : -0.15f;

            if (holdChestAnchor != null)
                return holdChestAnchor.position + holdChestAnchor.right * sideOffset;

            if (animator != null)
            {
                Transform chest = animator.GetBoneTransform(HumanBodyBones.Chest);
                if (chest != null) return chest.position + transform.right * sideOffset;
            }

            return transform.position + transform.forward * 0.5f + transform.up * 1.35f + transform.right * sideOffset;
        }

        private void OnDisable()
        {
            SetMovementLock(false);
            ReleaseHeldObject(true);
            UpdateHeavyLiftMessage();
        }

        private void SetMovementLock(bool shouldLock)
        {
            if (movementController != null)
                movementController.SetExternalMovementLock(shouldLock);
            lockMovementForCurrentLift = shouldLock;
            UpdateHeavyLiftMessage();
        }

        private void UpdateHeavyLiftMessage()
        {
            if (heavyLiftMessageText == null) return;

            bool show = isActive && lockMovementForCurrentLift;
            heavyLiftMessageText.text = show ? heavyLiftMessage : string.Empty;
            heavyLiftMessageText.enabled = show;
        }
    }
}