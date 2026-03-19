using System.Collections;
using UnityEngine;
using StarterAssets;

// CHANGE: Added StarterAssets using directive so we can access ThirdPersonController
// and wire functional scaling into the same controller that already drives the character.

namespace WeightLifter
{
    public class PlayerStats : MonoBehaviour
    {
        [Header("Progress")]
        public float currentStrength = 15f;
        public bool isBusy = false;

        [Header("Settings")]
        public float strengthGainMultiplier = 0.1f;

        // CHANGE: visualRoot is the player root transform for persistent scaling.
        // modelRoot is the visible rig child used only as the absorb fly-to target.
        [Header("Visuals")]
        [Tooltip("Leave empty; root transform is used for scaling.")]
        public Transform visualRoot;
        [Tooltip("Optional rig/model target for absorb fly-to visuals.")]
        public Transform modelRoot;
        public float absorbDuration = 0.6f;
        public float maxVisualScale = 200f;

        [Header("Debug")]
        public bool debugScalingLogs = false;

        [Header("Functional Scaling")]
        [Tooltip("Scale factor applied to movement stats and collider. 1 = fully proportional to visual size.")]
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

        // CHANGE: Use a square-root curve so growth stays dramatic but controlled.
        private float GetTargetScaleValue()
        {
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

        // CHANGE: Keep CharacterController and ThirdPersonController proportional to visual size.
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
