using System.Collections;
using UnityEngine;
using StarterAssets;

namespace WeightLifter
{
    public class PlayerStats : MonoBehaviour
    {
        [Header("Progress")]
        public float currentStrength = 15f;
        public bool isBusy = false;

        [Header("Settings")]
        public float strengthGainMultiplier = 0.1f;

        [Header("Visuals")]
        [Tooltip("Leave empty to scale the player root. Root scaling stays stable even when the animator updates the rig.")]
        public Transform visualRoot;
        [Tooltip("Optional rig/model target used as the absorb fly-to destination.")]
        public Transform modelRoot;
        public float absorbDuration = 0.6f;
        public float maxVisualScale = 200f;

        [Header("Debug")]
        public bool debugScalingLogs = false;

        [Header("Functional Scaling")]
        [Tooltip("Multiplier applied to gameplay scaling. Visual growth still comes from strength; this tunes how strongly movement and controller sizing follow it.")]
        public float functionalScaleFactor = 1f;

        private float _initialStrength;
        private Vector3 _baseVisualScale;

        private CharacterController _cc;
        private float _baseCCHeight;
        private float _baseCCRadius;
        private Vector3 _baseCCCenter;
        private float _baseCCStepOffset;

        private ThirdPersonController _tpc;
        private float _baseMoveSpeed;
        private float _baseSprintSpeed;
        private float _baseJumpHeight;
        private float _baseGroundedOffset;
        private float _baseGroundedRadius;

        private float _currentTargetScaleValue;

        private Transform ResolveModelRoot()
        {
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
            // Scale the player root, not the rig child. Animator updates can overwrite child scales.
            visualRoot = transform;

            if (modelRoot == null)
                modelRoot = ResolveModelRoot();

            _initialStrength = Mathf.Max(0.1f, currentStrength);
            _baseVisualScale = visualRoot.localScale;
            _currentTargetScaleValue = _baseVisualScale.x;

            _cc = GetComponent<CharacterController>();
            if (_cc != null)
            {
                _baseCCHeight = _cc.height;
                _baseCCRadius = _cc.radius;
                _baseCCCenter = _cc.center;
                _baseCCStepOffset = _cc.stepOffset;
            }

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
                string modelRootName = modelRoot != null ? modelRoot.name : "<null>";
                Debug.Log($"[ScaleDebug] visualRoot={visualRoot.name}, modelRoot={modelRootName}, baseVisualScale={_baseVisualScale}, initialStrength={_initialStrength}");
            }
        }

        private void LateUpdate()
        {
            if (visualRoot == null) return;
            float s = _currentTargetScaleValue;
            visualRoot.localScale = new Vector3(s, s, s);
        }

        private float GetTargetScaleValue()
        {
            // Strength can grow very quickly, so use a square-root curve to keep the
            // player feeling larger without making scale explode too early in a run.
            float ratio = currentStrength / _initialStrength;
            float multiplier = Mathf.Sqrt(Mathf.Max(1f, ratio));
            return Mathf.Min(maxVisualScale, _baseVisualScale.x * multiplier);
        }

        private void ApplyCurrentStrengthScaleInstant()
        {
            _currentTargetScaleValue = GetTargetScaleValue();
            if (visualRoot != null)
                visualRoot.localScale = new Vector3(_currentTargetScaleValue, _currentTargetScaleValue, _currentTargetScaleValue);

            UpdateFunctionalScale();

            if (debugScalingLogs)
            {
                Debug.Log($"[ScaleDebug] ApplyInstant currentStrength={currentStrength:F2}, targetScale={_currentTargetScaleValue:F3}");
            }
        }

        private void UpdateFunctionalScale()
        {
            if (visualRoot == null || _baseVisualScale.x <= 0f) return;

            float visualScaleRatio = visualRoot.localScale.x / _baseVisualScale.x;
            float clampedFunctionalFactor = Mathf.Max(0.1f, functionalScaleFactor);

            if (_cc != null)
            {
                // CharacterController values are local-space. Because the player root is already
                // scaled visually, reapplying visualScaleRatio here would create an oversized
                // invisible collision shell in world-space.
                _cc.height = _baseCCHeight * clampedFunctionalFactor;
                _cc.radius = _baseCCRadius * clampedFunctionalFactor;
                _cc.center = _baseCCCenter * clampedFunctionalFactor;
                _cc.stepOffset = _baseCCStepOffset * clampedFunctionalFactor;
            }

            if (_tpc != null)
            {
                // Movement and grounded probes should still feel proportional to the visible
                // body size, so they intentionally follow visual growth.
                float movementScale = visualScaleRatio * clampedFunctionalFactor;
                _tpc.MoveSpeed = _baseMoveSpeed * movementScale;
                _tpc.SprintSpeed = _baseSprintSpeed * movementScale;
                _tpc.JumpHeight = _baseJumpHeight * movementScale;
                _tpc.GroundedOffset = _baseGroundedOffset * movementScale;
                _tpc.GroundedRadius = _baseGroundedRadius * movementScale;
            }
        }

        public void AddStrengthFromObject(float objectWeight)
        {
            float gain = objectWeight * strengthGainMultiplier;
            currentStrength += gain;
            ApplyCurrentStrengthScaleInstant();

            if (debugScalingLogs)
                Debug.Log($"<color=green>STRENGTH UP!</color> Gained: {gain}. New Total: {currentStrength}");
        }

        public void AbsorbObject(GameObject obj, float objectWeight)
        {
            StartCoroutine(AbsorbRoutine(obj, objectWeight));
        }

        public void AbsorbObjectInstant(GameObject obj, float objectWeight, bool rescanOverlapping = true)
        {
            float gain = objectWeight * strengthGainMultiplier;
            currentStrength += gain;
            ApplyCurrentStrengthScaleInstant();

            if (obj != null)
                Destroy(obj);

            if (debugScalingLogs)
                Debug.Log($"<color=green>STRENGTH UP!</color> Gained: {gain}. New Total: {currentStrength}");

            isBusy = false;

            if (rescanOverlapping)
            {
                var interaction = GetComponent<LiftingInteraction>();
                if (interaction != null) interaction.RescanOverlapping();
            }
        }

        private IEnumerator AbsorbRoutine(GameObject obj, float objectWeight)
        {
            float gain = objectWeight * strengthGainMultiplier;
            currentStrength += gain;

            if (obj == null)
            {
                ApplyCurrentStrengthScaleInstant();
                if (debugScalingLogs)
                    Debug.Log($"<color=green>STRENGTH UP!</color> Gained: {gain}. New Total: {currentStrength}");
                yield break;
            }

            float t = 0f;
            float dur = Mathf.Max(0.05f, absorbDuration);

            Vector3 startPos = obj.transform.position;
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

            if (debugScalingLogs)
                Debug.Log($"<color=green>STRENGTH UP!</color> Gained: {gain}. New Total: {currentStrength}");

            isBusy = false;

            var interaction = GetComponent<LiftingInteraction>();
            if (interaction != null) interaction.RescanOverlapping();

            if (debugScalingLogs && visualRoot != null)
            {
                Debug.Log($"[ScaleDebug] AbsorbComplete rootScale={visualRoot.localScale}, ccHeight={(_cc != null ? _cc.height : 0f):F2}");
            }
        }
    }
}
