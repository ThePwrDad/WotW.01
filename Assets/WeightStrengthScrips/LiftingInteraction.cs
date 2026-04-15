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

        [Header("Fallback Proximity Detection")]
        [Tooltip("Scans nearby colliders to recover lift targets when trigger callbacks miss at very large scales.")]
        public bool enableProximityFallback = true;
        [Tooltip("How often to run fallback proximity scans.")]
        public float fallbackScanInterval = 0.12f;
        [Tooltip("Minimum radius used by fallback proximity scan.")]
        public float fallbackBaseRadius = 2.25f;
        [Tooltip("Extra radius added per unit of world scale.")]
        public float fallbackRadiusPerScale = 0.08f;
        [Tooltip("Maximum fallback scan radius to limit expensive overlap queries.")]
        public float fallbackMaxRadius = 16f;
        [Tooltip("Vertical offset for fallback scan origin relative to player root.")]
        public float fallbackOriginHeight = 0.9f;

        private float _nextFallbackScanTime;
        private readonly Collider[] _fallbackHits = new Collider[128];

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

        private void Update()
        {
            if (!enableProximityFallback || stats == null || liftingMiniGame == null || stats.isBusy)
                return;

            if (Time.time < _nextFallbackScanTime)
                return;

            _nextFallbackScanTime = Time.time + Mathf.Max(0.02f, fallbackScanInterval);
            RunFallbackProximityScan();
        }

        private void HandleTrigger(Collider other)
        {
            if (stats == null || liftingMiniGame == null || stats.isBusy) return;

            Transform otherRoot = other.transform.root;
            Transform selfRoot = transform.root;
            if (otherRoot == selfRoot || other.transform.IsChildOf(transform)) return;

            WeightData cube = ResolveWeightData(other);
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

        private void RunFallbackProximityScan()
        {
            float worldScale = Mathf.Max(1f, transform.lossyScale.x);
            float radius = Mathf.Clamp(
                fallbackBaseRadius + (worldScale * fallbackRadiusPerScale),
                fallbackBaseRadius,
                fallbackMaxRadius);

            Vector3 origin = transform.position + Vector3.up * Mathf.Max(0f, fallbackOriginHeight);
            int hitCount = Physics.OverlapSphereNonAlloc(origin, radius, _fallbackHits, ~0, QueryTriggerInteraction.Collide);

            float maxLiftableWeight = stats.currentStrength * liftingMiniGame.maxWeightMultiplier;
            WeightData nearestLiftable = null;
            float nearestDistSqr = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = _fallbackHits[i];
                if (hit == null || hit == _triggerCollider) continue;

                Transform otherRoot = hit.transform.root;
                Transform selfRoot = transform.root;
                if (otherRoot == selfRoot || hit.transform.IsChildOf(transform)) continue;

                WeightData cube = ResolveWeightData(hit);
                if (cube == null) continue;
                if (cube.weight > maxLiftableWeight) continue;

                float distSqr = (cube.transform.position - transform.position).sqrMagnitude;
                if (distSqr < nearestDistSqr)
                {
                    nearestDistSqr = distSqr;
                    nearestLiftable = cube;
                }
            }

            if (nearestLiftable != null)
            {
                liftingMiniGame.SetCurrentTarget(nearestLiftable);
            }
            else
            {
                WeightData existing = liftingMiniGame.CurrentTarget;
                if (existing != null)
                    liftingMiniGame.ClearCurrentTarget(existing);
            }
        }

        private static WeightData ResolveWeightData(Collider col)
        {
            if (col == null) return null;

            WeightData cube = col.GetComponent<WeightData>();
            if (cube == null) cube = col.GetComponentInParent<WeightData>();
            if (cube == null) cube = col.GetComponentInChildren<WeightData>();
            return cube;
        }

        private void OnTriggerExit(Collider other)
        {
            if (liftingMiniGame == null) return;
            WeightData cube = ResolveWeightData(other);
            if (cube != null) liftingMiniGame.ClearCurrentTarget(cube);
        }

        public void RescanOverlapping()
        {
            if (enableProximityFallback && stats != null && liftingMiniGame != null)
                RunFallbackProximityScan();

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