using System.Collections;
using UnityEngine;

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
        public Transform visualRoot;
        public float scaleGainPerStrength = 0.01f;
        public float absorbDuration = 0.6f;
        public float maxVisualScale = 3f;

        private void Start()
        {
            if (visualRoot == null)
            {
                var smr = GetComponentInChildren<SkinnedMeshRenderer>();
                if (smr != null) visualRoot = smr.transform;
                else visualRoot = transform;
            }
        }

        public void AddStrengthFromObject(float objectWeight)
        {
            float gain = objectWeight * strengthGainMultiplier;
            currentStrength += gain;
            Debug.Log($"<color=green>STRENGTH UP!</color> Gained: {gain}. New Total: {currentStrength}");
        }

        public void AbsorbObject(GameObject obj, float objectWeight)
        {
            StartCoroutine(AbsorbRoutine(obj, objectWeight));
        }

        private IEnumerator AbsorbRoutine(GameObject obj, float objectWeight)
        {
            if (obj == null)
            {
                AddStrengthFromObject(objectWeight);
                yield break;
            }

            float t = 0f;
            float dur = Mathf.Max(0.05f, absorbDuration);

            Vector3 startPos = obj.transform.position;
            Vector3 endPos = visualRoot != null ? visualRoot.position : transform.position;
            Vector3 startScale = obj.transform.localScale;

            var rb = obj.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;

            var colliders = obj.GetComponentsInChildren<Collider>();
            foreach (var c in colliders) c.enabled = false;

            // Calculate target scale for player
            float gain = objectWeight * strengthGainMultiplier;
            Vector3 initialPlayerScale = visualRoot.localScale;
            float targetScaleValue = Mathf.Min(maxVisualScale, initialPlayerScale.x * (1f + gain * scaleGainPerStrength));
            Vector3 targetPlayerScale = new Vector3(targetScaleValue, targetScaleValue, targetScaleValue);

            while (t < dur)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / dur);
                obj.transform.position = Vector3.Lerp(startPos, endPos, p);
                obj.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, p);
                if (visualRoot != null)
                    visualRoot.localScale = Vector3.Lerp(initialPlayerScale, targetPlayerScale, p);
                yield return null;
            }

            Destroy(obj);

            currentStrength += gain;
            Debug.Log($"<color=green>STRENGTH UP!</color> Gained: {gain}. New Total: {currentStrength}");

            if (visualRoot != null)
                visualRoot.localScale = targetPlayerScale;
        }
    }
}
