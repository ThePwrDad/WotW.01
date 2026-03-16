using UnityEngine;
using System.Collections.Generic;

// CHANGE: Added System.Collections.Generic for HashSet, used to deduplicate
// "No WeightData" debug log entries so the console isn't spammed every OnTriggerStay frame.

namespace WeightLifter
{
    [RequireComponent(typeof(PlayerStats))]
    [RequireComponent(typeof(LiftingMiniGame))]
    public class LiftingInteraction : MonoBehaviour
    {
        private PlayerStats stats;
        private LiftingMiniGame liftingMiniGame;

        // CHANGE: Added _triggerCollider cache so RescanOverlapping can use OverlapBox
        // with the actual trigger bounds rather than a hardcoded radius. This ensures
        // the rescan matches the exact shape of the trigger used for normal detection.
        private Collider _triggerCollider;

        // CHANGE: Added debug flags so lift detection logging can be toggled in the
        // Inspector at runtime. This was added during diagnosis of the "unliftable at
        // high strength" bug to see object name, weight, threshold, and liftable state
        // without modifying code.
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
            // CHANGE: Cache the trigger collider here for use in RescanOverlapping.
            _triggerCollider = GetComponent<Collider>();
        }

        private void OnTriggerEnter(Collider other) => HandleTrigger(other);
        private void OnTriggerStay(Collider other) => HandleTrigger(other);

        private void HandleTrigger(Collider other)
        {
            if (stats == null || liftingMiniGame == null || stats.isBusy) return;

            // CHANGE: Added self-collider filter. The player's own rig colliders (e.g.
            // PlayerArmature, ragdoll bones) share the same physics scene and can enter
            // the player trigger, generating constant false-positive "No WeightData" spam.
            // Filtering by root transform identity and IsChildOf catches both flat and
            // deeply nested player colliders regardless of prefab structure.
            Transform otherRoot = other.transform.root;
            Transform selfRoot = transform.root;
            if (otherRoot == selfRoot || other.transform.IsChildOf(transform)) return;

            // CHANGE: Extended WeightData lookup to search parent and children, not just
            // the exact collider GameObject. Many prefabs place the collider on a child mesh
            // object while WeightData lives on the parent, causing identical-looking objects
            // to behave differently based on their internal hierarchy.
            WeightData cube = other.GetComponent<WeightData>();
            if (cube == null) cube = other.GetComponentInParent<WeightData>();
            if (cube == null) cube = other.GetComponentInChildren<WeightData>();
            if (cube == null)
            {
                if (debugLiftDetection && debugVerboseMisses)
                {
                    // CHANGE: Log each missing collider only once (by instance ID) to avoid
                    // per-frame spam from OnTriggerStay. The HashSet acts as a seen-set.
                    int id = other.GetInstanceID();
                    if (!_loggedMissingColliderIds.Contains(id))
                    {
                        _loggedMissingColliderIds.Add(id);
                        Debug.Log($"[LiftDebug] No WeightData found for collider '{other.name}' on object '{other.gameObject.name}'.");
                    }
                }
                return;
            }

            // Clear cached miss-log if collider became valid due to runtime changes.
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

        // CHANGE: RescanOverlapping is a new method added to fix the "objects unliftable
        // after high strength" bug. When isBusy=true, HandleTrigger returns early, so any
        // object that entered the trigger zone during an absorb animation was silently dropped.
        // PlayerStats.AbsorbRoutine calls this immediately after clearing isBusy so those
        // missed objects are retroactively evaluated without the player needing to re-enter them.
        public void RescanOverlapping()
        {
            if (_triggerCollider == null) return;

            // Use the actual trigger collider bounds for an accurate re-check,
            // matching the same volume that OnTriggerEnter/Stay would use.
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