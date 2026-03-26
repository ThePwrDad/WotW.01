using System.Collections;
using UnityEngine;
using StarterAssets;

// CHANGE: Added StarterAssets using directive so we can access ThirdPersonController
// at the end of the session to wire functional (movement/collider) scaling into the
// same component that drives the character — no separate manager needed.

namespace WeightLifter
{
    public class PlayerStats : MonoBehaviour
    {
        [Header("Progress")]
        public float currentStrength = 15f;
        public bool isBusy = false;

        [Header("Settings")]
        public float strengthGainMultiplier = 0.1f;

        // CHANGE: visualRoot was originally a single field pointing at the rig child.
        // We split it into two fields:
        //   visualRoot  — always the player root transform (for size scaling).
        //   modelRoot   — the visible rig child (for absorb animation fly-to target only).
        // This separation was necessary because Unity's Animator system writes bone/child
        // transform data AFTER LateUpdate, so any scale we applied to the rig child was
        // silently reset every frame by the animation runtime. The root transform is NOT
        // touched by the Animator, so it is the correct place to store persistent scale.
        [Header("Visuals")]
        [Tooltip("Leave empty — player root transform is used for scaling. Assign only to override.")]
        public Transform visualRoot;
        [Tooltip("The model/rig child that the absorb animation flies objects toward. Auto-detected if empty.")]
        public Transform modelRoot;
        public float absorbDuration = 0.6f;

        // CHANGE: Raised from 3f to 200f to support the intended end-game scale where
        // the player absorbs buildings and very large structures.
        public float maxVisualScale = 200f;
        [Tooltip("Power applied to the strength ratio to control growth speed. Lower = slower growth. 0.5 = square root, 0.3 = much slower, 1.0 = fully linear.")]
        [Range(0.1f, 1.0f)]
        public float scaleExponent = 0.3f;

        [Header("Debug")]
        // CHANGE: Added debug flag so scaling logs can be toggled in the Inspector
        // without touching code. Used during diagnosis of the visual scaling issue.
        public bool debugScalingLogs = false;

        // CHANGE: Added functionalScaleFactor so movement/collider scaling can be tuned
        // independently of visual scale. 0 = cosmetic growth only, 1 = fully proportional.
        [Header("Functional Scaling")]
        [Tooltip("Scale factor applied to movement stats and collider. 1 = fully proportional to visual size.")]
        public float functionalScaleFactor = 1f;

        // Cached starting values — recorded once in Start() so all scaling remains
        // relative to the player's original inspector-configured values.
        private float _initialStrength;
        private Vector3 _baseVisualScale;

        // CHANGE: Added CharacterController cache fields so we can resize the physics
        // capsule proportionally as the player grows. Without this, the player's hitbox
        // stays tiny while the model becomes enormous.
        private CharacterController _cc;
        private float _baseCCHeight;
        private float _baseCCRadius;
        private Vector3 _baseCCCenter;
        private float _baseCCStepOffset;

        // CHANGE: Added ThirdPersonController cache fields so movement speed, sprint,
        // jump height, and grounded detection all scale with the player's size.
        // A giant player should run and jump proportionally faster/higher.
        private ThirdPersonController _tpc;
        private float _baseMoveSpeed;
        private float _baseSprintSpeed;
        private float _baseJumpHeight;
        private float _baseGroundedOffset;
        private float _baseGroundedRadius;

        // CHANGE: Tracks the current intended scale value so LateUpdate can reapply it
        // every frame without recalculating the full formula each tick.
        private float _currentTargetScaleValue;

        // CHANGE: ResolveModelRoot replaces the old ResolveDefaultVisualRoot.
        // It now serves a single, narrower purpose: finding the rig child to use as the
        // absorb animation fly-to target. It is NOT used for scaling (see visualRoot above).
        // Walks up from the Animator or SkinnedMeshRenderer until it finds the direct child
        // of the player root, so deeply nested rigs are handled correctly.
        private Transform ResolveModelRoot()
        {
            // Find the top-most child that carries the rig/animator, used only for absorb animation target.
            var anim = GetComponentInChildren<Animator>();
            if (anim != null && anim.transform != transform)
            {
                Transform t = anim.transform;
                while (t.parent != null && t.parent != transform) t = t.parent;
                return t;
            }
            var smr = GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr != null && smr.transform != transform)
            {
                Transform t = smr.transform;
                while (t.parent != null && t.parent != transform) t = t.parent;
                return t;
            }
            return transform;
        }

        private void Start()
        {
            // CHANGE: visualRoot is forced to the player's own root transform here.
            // Previously it was set to the Animator rig child, which caused visual scaling
            // to silently fail — Unity's animation system overwrites child transform scales
            // after LateUpdate via internal animation jobs. The root transform is immune
            // to this, making it the only reliable target for persistent scale changes.
            visualRoot = transform;

            if (modelRoot == null)
                modelRoot = ResolveModelRoot();

            _initialStrength = Mathf.Max(0.1f, currentStrength);
            _baseVisualScale = visualRoot.localScale;
            _currentTargetScaleValue = _baseVisualScale.x;

            // CHANGE: Cache all CharacterController fields at startup so that
            // UpdateFunctionalScale always works relative to the designed baseline,
            // not the previously-modified values (avoids compounding drift over time).
            _cc = GetComponent<CharacterController>();
            if (_cc != null)
            {
                _baseCCHeight = _cc.height;
                _baseCCRadius = _cc.radius;
                _baseCCCenter = _cc.center;
                _baseCCStepOffset = _cc.stepOffset;
            }

            // CHANGE: Cache all ThirdPersonController movement fields at startup for
            // the same reason — relative scaling always works from the original values.
            _tpc = GetComponent<ThirdPersonController>();
            if (_tpc != null)
            {
                _baseMoveSpeed = _tpc.MoveSpeed;
                _baseSprintSpeed = _tpc.SprintSpeed;
                _baseJumpHeight = _tpc.JumpHeight;
                _baseGroundedOffset = _tpc.GroundedOffset;
                _baseGroundedRadius = _tpc.GroundedRadius;
            }

            ApplyCurrentStrengthScaleInstant();

            if (debugScalingLogs)
            {
                Debug.Log($"[ScaleDebug] visualRoot={visualRoot.name}, modelRoot={modelRoot.name}, baseVisualScale={_baseVisualScale}, initialStrength={_initialStrength}");
            }
        }

        private void LateUpdate()
        {
            // CHANGE: Apply the stored target scale every LateUpdate on the root transform.
            // Even though the root is safe from Animator overrides, this ensures that any
            // other system (e.g. a future root-motion feature) cannot accidentally stomp it.
            // _currentTargetScaleValue is only updated when strength actually changes,
            // so this is a cheap assignment each frame, not a full recalculation.
            if (visualRoot == null) return;
            float s = _currentTargetScaleValue;
            visualRoot.localScale = new Vector3(s, s, s);
        }

        // CHANGE: Replaced old per-gain linear formula with power-law growth curve (ratio^scaleExponent).
        // Old formula caused huge single-frame jumps (10,000 weight → 11× scale). Power-law prevents that.
        // Default exponent 0.3 gives slower, controlled growth: 4× strength → 1.52× size (vs sqrt → 2×).
        // Examples: 2× strength → ~1.30× size, 100× strength → ~3.98× size.
        // Clamped by maxVisualScale (200) so progression stays readable.
        private float GetTargetScaleValue()
        {
            float ratio = currentStrength / _initialStrength;
            float multiplier = Mathf.Pow(Mathf.Max(1f, ratio), scaleExponent);
            return Mathf.Min(maxVisualScale, _baseVisualScale.x * multiplier);
        }

        // CHANGE: Consolidated all "apply scale immediately" calls into one method to avoid
        // duplicated logic across AddStrengthFromObject, AbsorbRoutine (null-obj branch),
        // and Start(). Updates both _currentTargetScaleValue (read by LateUpdate) and
        // directly sets the transform so there is no one-frame delay on the first apply.
        private void ApplyCurrentStrengthScaleInstant()
        {
            _currentTargetScaleValue = GetTargetScaleValue();
            if (visualRoot != null)
                visualRoot.localScale = new Vector3(_currentTargetScaleValue, _currentTargetScaleValue, _currentTargetScaleValue);
            UpdateFunctionalScale();

            if (debugScalingLogs)
            {
                Debug.Log($"[ScaleDebug] ApplyInstant currentStrength={currentStrength:F2}, targetScale={_currentTargetScaleValue:F3}, root={visualRoot.name}");
            }
        }

        // CHANGE: UpdateFunctionalScale is a new method that did not exist in the original.
        // It drives CharacterController and ThirdPersonController fields proportionally to
        // the player's current visual scale vs their starting scale, multiplied by
        // functionalScaleFactor so the designer can dial back functional growth independently.
        // Called every frame during AbsorbRoutine's animation loop so the capsule and movement
        // grow smoothly in sync with the visible model during the absorb animation.
        private void UpdateFunctionalScale()
        {
            if (visualRoot == null || _baseVisualScale.x <= 0f) return;

            float scaleRatio = (visualRoot.localScale.x / _baseVisualScale.x) * functionalScaleFactor;

            if (_cc != null)
            {
                _cc.height = _baseCCHeight * scaleRatio;
                _cc.radius = _baseCCRadius * scaleRatio;
                _cc.center = _baseCCCenter * scaleRatio;
                _cc.stepOffset = _baseCCStepOffset * scaleRatio;
            }

            if (_tpc != null)
            {
                _tpc.MoveSpeed = _baseMoveSpeed * scaleRatio;
                _tpc.SprintSpeed = _baseSprintSpeed * scaleRatio;
                _tpc.JumpHeight = _baseJumpHeight * scaleRatio;
                _tpc.GroundedOffset = _baseGroundedOffset * scaleRatio;
                _tpc.GroundedRadius = _baseGroundedRadius * scaleRatio;
            }
        }

        public void AddStrengthFromObject(float objectWeight)
        {
            float gain = objectWeight * strengthGainMultiplier;
            currentStrength += gain;
            ApplyCurrentStrengthScaleInstant();

            Debug.Log($"<color=green>STRENGTH UP!</color> Gained: {gain}. New Total: {currentStrength}");
        }

        public void AbsorbObject(GameObject obj, float objectWeight)
        {
            StartCoroutine(AbsorbRoutine(obj, objectWeight));
        }

        private IEnumerator AbsorbRoutine(GameObject obj, float objectWeight)
        {
            float gain = objectWeight * strengthGainMultiplier;
            currentStrength += gain;

            if (obj == null)
            {
                ApplyCurrentStrengthScaleInstant();
                Debug.Log($"<color=green>STRENGTH UP!</color> Gained: {gain}. New Total: {currentStrength}");
                yield break;
            }

            float t = 0f;
            float dur = Mathf.Max(0.05f, absorbDuration);

            Vector3 startPos = obj.transform.position;
            // CHANGE: Point the fly-to destination at modelRoot (the visible rig child)
            // rather than transform.position (the root pivot which can be at floor level).
            // This makes the absorbed object visually travel into the character's body
            // rather than toward the character's feet.
            Vector3 endPos = modelRoot != null ? modelRoot.position : transform.position;
            Vector3 startScale = obj.transform.localScale;

            var rb = obj.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;

            var colliders = obj.GetComponentsInChildren<Collider>();
            foreach (var c in colliders) c.enabled = false;

            Vector3 initialPlayerScale = visualRoot != null ? visualRoot.localScale : Vector3.one;
            float targetScaleValue = GetTargetScaleValue();
            _currentTargetScaleValue = targetScaleValue;
            Vector3 targetPlayerScale = new Vector3(targetScaleValue, targetScaleValue, targetScaleValue);

            while (t < dur)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / dur);
                obj.transform.position = Vector3.Lerp(startPos, endPos, p);
                obj.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, p);
                if (visualRoot != null)
                    visualRoot.localScale = Vector3.Lerp(initialPlayerScale, targetPlayerScale, p);
                UpdateFunctionalScale();
                yield return null;
            }

            Destroy(obj);

            if (visualRoot != null)
                visualRoot.localScale = targetPlayerScale;
            _currentTargetScaleValue = targetScaleValue;
            UpdateFunctionalScale();

            Debug.Log($"<color=green>STRENGTH UP!</color> Gained: {gain}. New Total: {currentStrength}");

            // CHANGE: isBusy ownership was moved here from LiftingMiniGame.CompleteLift.
            // Previously CompleteLift cleared isBusy before AbsorbRoutine started (coroutines
            // are queued, not immediate), causing a race condition: the coroutine set isBusy=true
            // but CompleteLift had already cleared it, so objects that entered the trigger zone
            // during the absorb animation were never detected and appeared unliftable.
            // Clearing it here — at the true end of the absorb — guarantees the window of
            // isBusy=true is exactly the duration of the animation, no more.
            isBusy = false;

            // CHANGE: After clearing isBusy, immediately re-scan the trigger volume for any
            // liftable objects that entered while we were busy. Without this, the player would
            // need to walk away and back to re-trigger detection on nearby objects.
            var interaction = GetComponent<LiftingInteraction>();
            if (interaction != null) interaction.RescanOverlapping();

            if (debugScalingLogs && visualRoot != null)
            {
                Debug.Log($"[ScaleDebug] AbsorbComplete rootScale={visualRoot.localScale}, ccHeight={(_cc != null ? _cc.height : 0f):F2}");
            }
        }
    }
}
