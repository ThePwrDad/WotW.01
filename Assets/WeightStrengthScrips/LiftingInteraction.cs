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
        [Tooltip("Scans nearby colliders to recover lift targets when direct trigger contact is unreliable in dense scenes or at large player scales.")]
        public bool enableProximityFallback = true;
        [Tooltip("Forces fallback scanning when no target is active so older scene instances cannot silently disable lift recovery.")]
        public bool forceFallbackWhenNoTarget = true;
        [Tooltip("Player world scale threshold where fallback scans are always allowed as a safety net.")]
        public float forceFallbackScaleThreshold = 1.2f;
        [Tooltip("How often to run fallback proximity scans.")]
        public float fallbackScanInterval = 0.12f;
        [Tooltip("Minimum radius used by fallback proximity scan.")]
        public float fallbackBaseRadius = 2.25f;
        [Tooltip("Extra radius added per unit of world scale.")]
        public float fallbackRadiusPerScale = 0.08f;
        [Tooltip("Maximum fallback scan radius to limit expensive overlap queries.")]
        public float fallbackMaxRadius = 16f;
        [Tooltip("Multiplier for a softer secondary scan radius used when no direct-contact target is available.")]
        public float fallbackSoftRadiusMultiplier = 1.75f;
        [Tooltip("Maximum soft scan radius for front-biased target selection.")]
        public float fallbackSoftMaxRadius = 24f;
        [Tooltip("Distance from scan origin considered direct contact. Direct-contact targets are always preferred.")]
        public float fallbackDirectContactDistance = 1.05f;
        [Tooltip("Extra direct-contact distance added from CharacterController radius to account for larger bodies.")]
        public float fallbackContactDistanceFromControllerRadius = 0.3f;
        [Tooltip("How strongly to prefer objects in front when no direct-contact target exists.")]
        [Range(0f, 2f)] public float fallbackForwardBiasWeight = 0.65f;
        [Tooltip("Minimum forward dot allowed for soft-range candidate selection.")]
        [Range(-1f, 1f)] public float fallbackMinForwardDot = -0.2f;
        [Tooltip("Vertical offset for fallback scan origin relative to player root.")]
        public float fallbackOriginHeight = 0.9f;
        [Tooltip("Uses CharacterController bounds to place scan origin near the torso instead of the root pivot at the feet.")]
        public bool fallbackUseCharacterControllerOrigin = true;
        [Tooltip("Normalized height across CharacterController bounds for scan origin. 0 = feet, 1 = head.")]
        [Range(0f, 1f)] public float fallbackControllerHeightRatio = 0.55f;
        [Tooltip("Scales fallbackOriginHeight by world scale when CharacterController origin is unavailable.")]
        public bool fallbackScaleHeightWithPlayer = true;
        [Tooltip("Layers considered by fallback proximity scan.")]
        public LayerMask fallbackLayerMask = ~0;
        [Tooltip("Ignore trigger colliders in fallback scans to reduce noise in dense levels.")]
        public bool fallbackIgnoreTriggerColliders = true;

        private float _nextFallbackScanTime;
        private readonly Collider[] _fallbackHits = new Collider[512];
        private readonly HashSet<Collider> _activeOverlapColliders = new HashSet<Collider>();
        private readonly List<Collider> _staleOverlapColliders = new List<Collider>(16);
        private CharacterController _characterController;

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
            _characterController = GetComponent<CharacterController>();
        }

        private void OnTriggerEnter(Collider other) => HandleTrigger(other);
        private void OnTriggerStay(Collider other) => HandleTrigger(other);

        private void Update()
        {
            if (stats == null || liftingMiniGame == null || stats.isBusy)
                return;

            float worldScale = Mathf.Max(1f, transform.lossyScale.x);
            // Trigger-based detection remains the cheapest path. The fallback scan is a
            // recovery system that stays active when the player becomes large enough that
            // contact geometry alone stops being reliable.
            bool shouldForceFallback = forceFallbackWhenNoTarget &&
                                       (liftingMiniGame.CurrentTarget == null || worldScale >= forceFallbackScaleThreshold);
            if (!enableProximityFallback && !shouldForceFallback)
                return;

            if (Time.time < _nextFallbackScanTime)
                return;

            _nextFallbackScanTime = Time.time + Mathf.Max(0.02f, fallbackScanInterval);
            RunFallbackProximityScan();
        }

        private void HandleTrigger(Collider other)
        {
            if (stats == null || liftingMiniGame == null || stats.isBusy) return;
            if (other == null) return;

            _activeOverlapColliders.Add(other);

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
            float hardRadius = Mathf.Clamp(
                fallbackBaseRadius + (worldScale * fallbackRadiusPerScale),
                fallbackBaseRadius,
                fallbackMaxRadius);
            // The hard radius preserves the original "must be near me" behavior. The soft
            // radius is only used when object shapes or collider placement make exact contact
            // too strict for a prompt that still feels correct to the player.
            float softRadius = Mathf.Clamp(hardRadius * Mathf.Max(1f, fallbackSoftRadiusMultiplier), hardRadius, Mathf.Max(hardRadius, fallbackSoftMaxRadius));
            float directContactDistance = GetDirectContactDistance();

            Vector3 origin = GetFallbackScanOrigin(worldScale);
            QueryTriggerInteraction triggerMode = fallbackIgnoreTriggerColliders
                ? QueryTriggerInteraction.Ignore
                : QueryTriggerInteraction.Collide;
            int hitCount = Physics.OverlapSphereNonAlloc(origin, softRadius, _fallbackHits, fallbackLayerMask, triggerMode);

            float maxLiftableWeight = stats.currentStrength * liftingMiniGame.maxWeightMultiplier;
            WeightData closestContactLiftable = null;
            float closestContactDistSqr = float.MaxValue;
            WeightData bestSoftLiftable = null;
            float bestSoftScore = float.MinValue;
            float bestSoftDistSqr = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = _fallbackHits[i];
                if (hit == null || hit == _triggerCollider) continue;

                Transform otherRoot = hit.transform.root;
                Transform selfRoot = transform.root;
                if (otherRoot == selfRoot || hit.transform.IsChildOf(transform)) continue;

                EvaluateCandidate(
                    hit,
                    origin,
                    hardRadius,
                    softRadius,
                    directContactDistance,
                    maxLiftableWeight,
                    ref closestContactLiftable,
                    ref closestContactDistSqr,
                    ref bestSoftLiftable,
                    ref bestSoftScore,
                    ref bestSoftDistSqr);
            }

            // In dense scenes, non-alloc overlap can fill the buffer. Run a one-off full query
            // so nearby liftables are not missed when player scale increases scan radius.
            if (hitCount >= _fallbackHits.Length)
            {
                Collider[] expandedHits = Physics.OverlapSphere(origin, softRadius, fallbackLayerMask, triggerMode);
                for (int i = 0; i < expandedHits.Length; i++)
                {
                    EvaluateCandidate(
                        expandedHits[i],
                        origin,
                        hardRadius,
                        softRadius,
                        directContactDistance,
                        maxLiftableWeight,
                        ref closestContactLiftable,
                        ref closestContactDistSqr,
                        ref bestSoftLiftable,
                        ref bestSoftScore,
                        ref bestSoftDistSqr);
                }

                if (debugLiftDetection)
                {
                    Debug.Log($"[LiftDebug] Fallback scan saturated ({hitCount}/{_fallbackHits.Length}). Expanded scan considered {expandedHits.Length} colliders.");
                }
            }

            // Prefer physically close targets first. Only fall back to the softer forward-biased
            // selection when nothing is close enough to count as direct interaction.
            WeightData selectedLiftable = closestContactLiftable != null ? closestContactLiftable : bestSoftLiftable;

            if (selectedLiftable != null)
            {
                liftingMiniGame.SetCurrentTarget(selectedLiftable);
            }
            else
            {
                WeightData existing = liftingMiniGame.CurrentTarget;
                if (existing != null && !IsTargetStillLiftableAndNearby(existing, origin, softRadius, maxLiftableWeight))
                    liftingMiniGame.ClearCurrentTarget(existing);
            }
        }

        private float GetDirectContactDistance()
        {
            // Larger controllers need a slightly larger "close enough" window or the prompt can
            // feel like it ignores objects that are visually right in front of the player.
            float distance = Mathf.Max(0.1f, fallbackDirectContactDistance);
            if (_characterController != null)
                distance += _characterController.radius * Mathf.Max(0f, fallbackContactDistanceFromControllerRadius);

            return distance;
        }

        private Vector3 GetFallbackScanOrigin(float worldScale)
        {
            if (fallbackUseCharacterControllerOrigin && _characterController != null)
            {
                // Use the live controller bounds so the scan origin follows the actual body height
                // after growth, rather than a fixed offset near the feet.
                Bounds b = _characterController.bounds;
                float y = Mathf.Lerp(b.min.y, b.max.y, Mathf.Clamp01(fallbackControllerHeightRatio));
                return new Vector3(b.center.x, y, b.center.z);
            }

            float offset = Mathf.Max(0f, fallbackOriginHeight);
            if (fallbackScaleHeightWithPlayer)
                offset *= worldScale;

            return transform.position + Vector3.up * offset;
        }

        private void EvaluateCandidate(
            Collider hit,
            Vector3 origin,
            float hardRadius,
            float softRadius,
            float directContactDistance,
            float maxLiftableWeight,
            ref WeightData closestContactLiftable,
            ref float closestContactDistSqr,
            ref WeightData bestSoftLiftable,
            ref float bestSoftScore,
            ref float bestSoftDistSqr)
        {
            if (hit == null || hit == _triggerCollider) return;

            Transform otherRoot = hit.transform.root;
            Transform selfRoot = transform.root;
            if (otherRoot == selfRoot || hit.transform.IsChildOf(transform)) return;

            WeightData cube = ResolveWeightData(hit);
            if (cube == null || cube.weight > maxLiftableWeight) return;

            Vector3 closest = GetClosestPointSafe(hit, origin);
            float distSqr = (closest - origin).sqrMagnitude;
            float softRadiusSqr = softRadius * softRadius;
            if (distSqr > softRadiusSqr)
                return;

            float directContactSqr = directContactDistance * directContactDistance;
            if (distSqr <= directContactSqr)
            {
                if (distSqr < closestContactDistSqr)
                {
                    closestContactDistSqr = distSqr;
                    closestContactLiftable = cube;
                }
                return;
            }

            Vector3 toClosest = closest - origin;
            float mag = toClosest.magnitude;
            if (mag <= 0.0001f) return;

            float forwardDot = Vector3.Dot(transform.forward, toClosest / mag);
            if (forwardDot < fallbackMinForwardDot)
                return;

            float hardRadiusSqr = hardRadius * hardRadius;
            // Score candidates by a mix of nearness and how naturally "in front" they are.
            // This keeps prompt selection usable around irregular object shapes without letting
            // distant side objects steal focus too easily.
            float distanceScore = distSqr <= hardRadiusSqr
                ? 1f
                : 1f - Mathf.Clamp01((mag - hardRadius) / Mathf.Max(0.01f, softRadius - hardRadius));
            float forwardScore = Mathf.Clamp01((forwardDot + 1f) * 0.5f);
            float score = distanceScore + (forwardScore * Mathf.Max(0f, fallbackForwardBiasWeight));

            if (score > bestSoftScore || (Mathf.Approximately(score, bestSoftScore) && distSqr < bestSoftDistSqr))
            {
                bestSoftScore = score;
                bestSoftDistSqr = distSqr;
                bestSoftLiftable = cube;
            }
        }

        private bool IsTargetStillLiftableAndNearby(WeightData target, Vector3 origin, float radius, float maxLiftableWeight)
        {
            if (target == null || target.weight > maxLiftableWeight)
                return false;

            Collider[] targetColliders = target.GetComponentsInChildren<Collider>();
            float radiusSqr = radius * radius;
            for (int i = 0; i < targetColliders.Length; i++)
            {
                Collider col = targetColliders[i];
                if (col == null) continue;
                Vector3 closest = GetClosestPointSafe(col, origin);
                if ((closest - origin).sqrMagnitude <= radiusSqr)
                    return true;
            }

            return false;
        }

        private static Vector3 GetClosestPointSafe(Collider col, Vector3 point)
        {
            if (col == null)
                return point;

            // Some world geometry uses collider types where ClosestPoint is unsupported.
            // Bounds are less precise, but good enough for prompt range checks.
            if (col is MeshCollider mesh && !mesh.convex)
                return col.bounds.ClosestPoint(point);

            if (col is TerrainCollider)
                return col.bounds.ClosestPoint(point);

            return col.ClosestPoint(point);
        }

        private bool HasActiveColliderForTarget(WeightData target)
        {
            if (target == null) return false;

            _staleOverlapColliders.Clear();
            foreach (Collider col in _activeOverlapColliders)
            {
                if (col == null)
                {
                    _staleOverlapColliders.Add(col);
                    continue;
                }

                if (ResolveWeightData(col) == target)
                    return true;
            }

            for (int i = 0; i < _staleOverlapColliders.Count; i++)
                _activeOverlapColliders.Remove(_staleOverlapColliders[i]);

            return false;
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
            if (other != null)
                _activeOverlapColliders.Remove(other);

            WeightData cube = ResolveWeightData(other);
            if (cube != null && !HasActiveColliderForTarget(cube))
                liftingMiniGame.ClearCurrentTarget(cube);
        }

        public void RescanOverlapping()
        {
            // Called after absorb and other state transitions so the interaction state snaps back
            // immediately instead of waiting for a fresh trigger callback.
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