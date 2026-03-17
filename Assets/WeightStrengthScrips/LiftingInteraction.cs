using UnityEngine;
using System.Collections.Generic;

namespace WeightLifter
{
    [RequireComponent(typeof(PlayerStats))]
    [RequireComponent(typeof(LiftingMiniGame))]
    public class LiftingInteraction : MonoBehaviour
    {
        private PlayerStats stats;
        private LiftingMiniGame liftingMiniGame;
        private Collider _triggerCollider;

        [Header("Debug")]
        public bool debugLiftDetection = false;
        public bool debugVerboseMisses = false;

        private WeightData _lastDebugWeightData;
        private bool _lastDebugLiftable;
        private float _lastDebugMaxLiftable;
        private readonly HashSet<int> _loggedMissingColliderIds = new HashSet<int>();

        private void Awake()
        {
            stats = GetComponent<PlayerStats>();
            liftingMiniGame = GetComponent<LiftingMiniGame>();
            _triggerCollider = GetComponent<Collider>();
        }

        private void OnTriggerEnter(Collider other) => HandleTrigger(other);
        private void OnTriggerStay(Collider other) => HandleTrigger(other);

        private void HandleTrigger(Collider other)
        {
            if (stats == null || liftingMiniGame == null || stats.isBusy) return;

            Transform otherRoot = other.transform.root;
            Transform selfRoot = transform.root;
            if (otherRoot == selfRoot || other.transform.IsChildOf(transform)) return;

            WeightData cube = other.GetComponent<WeightData>();
            if (cube == null) cube = other.GetComponentInParent<WeightData>();
            if (cube == null) cube = other.GetComponentInChildren<WeightData>();
            if (cube == null)
            {
                if (debugLiftDetection && debugVerboseMisses)
                {
                    int id = other.GetInstanceID();
                    if (!_loggedMissingColliderIds.Contains(id))
                    {
                        _loggedMissingColliderIds.Add(id);
                        Debug.Log($"[LiftDebug] No WeightData found for collider '{other.name}' on object '{other.gameObject.name}'.");
                    }
                }
                return;
            }

            _loggedMissingColliderIds.Remove(other.GetInstanceID());

            // Allow lifting objects up to our max multiplier threshold
            float maxLiftableWeight = stats.currentStrength * liftingMiniGame.maxWeightMultiplier;
            bool isLiftable = cube.weight <= maxLiftableWeight;

            if (debugLiftDetection)
            {
                bool changedTarget = cube != _lastDebugWeightData;
                bool changedLiftable = isLiftable != _lastDebugLiftable;
                bool changedThreshold = Mathf.Abs(maxLiftableWeight - _lastDebugMaxLiftable) > 0.5f;

                if (changedTarget || changedLiftable || changedThreshold)
                {
                    Debug.Log($"[LiftDebug] target='{cube.gameObject.name}' weight={cube.weight:F2} strength={stats.currentStrength:F2} maxLiftable={maxLiftableWeight:F2} liftable={isLiftable} viaCollider='{other.name}'");
                    _lastDebugWeightData = cube;
                    _lastDebugLiftable = isLiftable;
                    _lastDebugMaxLiftable = maxLiftableWeight;
                }
            }

            if (isLiftable)
                liftingMiniGame.SetCurrentTarget(cube);
            else
                liftingMiniGame.ClearCurrentTarget(cube);
        }

        private void OnTriggerExit(Collider other)
        {
            if (liftingMiniGame == null) return;
            WeightData cube = other.GetComponent<WeightData>();
            if (cube == null) cube = other.GetComponentInParent<WeightData>();
            if (cube == null) cube = other.GetComponentInChildren<WeightData>();
            if (cube != null) liftingMiniGame.ClearCurrentTarget(cube);
        }

        public void RescanOverlapping()
        {
            if (_triggerCollider == null) return;

            Bounds b = _triggerCollider.bounds;
            Collider[] hits = Physics.OverlapBox(b.center, b.extents, transform.rotation);
            foreach (var col in hits)
            {
                if (col == _triggerCollider) continue;
                HandleTrigger(col);
            }
        }
    }
}