using UnityEngine;

namespace CreatorWorld.Combat
{
    /// <summary>
    /// Visual bullet tracer that travels from muzzle to target.
    /// AAA Pattern: Visual feedback without affecting hitscan damage.
    /// Creates the illusion of bullet travel while keeping responsive hit detection.
    /// </summary>
    public class BulletTracer : MonoBehaviour
    {
        [Header("Movement")]
        [Tooltip("Speed of the tracer in units per second")]
        [SerializeField] private float speed = 300f;

        [Tooltip("Maximum lifetime before auto-destroy (safety)")]
        [SerializeField] private float maxLifetime = 2f;

        [Header("Visual")]
        [Tooltip("The glowing bullet mesh/sprite")]
        [SerializeField] private GameObject bulletVisual;

        [Tooltip("Trail renderer for smoke/vapor trail")]
        [SerializeField] private TrailRenderer trailRenderer;

        [Header("Impact")]
        [Tooltip("Particle effect to spawn on impact")]
        [SerializeField] private GameObject impactEffectPrefab;

        [Tooltip("Time to wait after reaching target before destroying (lets trail fade)")]
        [SerializeField] private float destroyDelay = 0.5f;

        // Runtime state
        private Vector3 startPosition;
        private Vector3 targetPosition;
        private float totalDistance;
        private float traveledDistance;
        private bool hasReachedTarget;
        private float spawnTime;

        /// <summary>
        /// Initialize the tracer with start and end positions.
        /// Called by the weapon when spawning.
        /// </summary>
        public void Initialize(Vector3 start, Vector3 target)
        {
            startPosition = start;
            targetPosition = target;
            transform.position = start;

            totalDistance = Vector3.Distance(start, target);
            traveledDistance = 0f;
            hasReachedTarget = false;
            spawnTime = Time.time;

            // Point toward target
            if (totalDistance > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(target - start);
            }

            // Clear any existing trail
            if (trailRenderer != null)
            {
                trailRenderer.Clear();
            }
        }

        private void Update()
        {
            if (hasReachedTarget) return;

            // Safety timeout
            if (Time.time - spawnTime > maxLifetime)
            {
                DestroyTracer();
                return;
            }

            // Move toward target
            float frameDistance = speed * Time.deltaTime;
            traveledDistance += frameDistance;

            if (traveledDistance >= totalDistance)
            {
                // Reached target
                transform.position = targetPosition;
                OnReachTarget();
            }
            else
            {
                // Lerp position
                float t = traveledDistance / totalDistance;
                transform.position = Vector3.Lerp(startPosition, targetPosition, t);
            }
        }

        private void OnReachTarget()
        {
            hasReachedTarget = true;

            // Hide bullet visual but keep trail
            if (bulletVisual != null)
            {
                bulletVisual.SetActive(false);
            }

            // Spawn impact effect
            if (impactEffectPrefab != null)
            {
                Instantiate(impactEffectPrefab, targetPosition, Quaternion.identity);
            }

            // Destroy after trail fades
            Destroy(gameObject, destroyDelay);
        }

        private void DestroyTracer()
        {
            Destroy(gameObject);
        }

        /// <summary>
        /// Static factory method for easy spawning.
        /// </summary>
        public static BulletTracer Spawn(GameObject prefab, Vector3 start, Vector3 target)
        {
            if (prefab == null)
            {
                Debug.LogWarning("[BulletTracer] Spawn called with null prefab!");
                return null;
            }

            GameObject instance = Instantiate(prefab, start, Quaternion.identity);
            BulletTracer tracer = instance.GetComponent<BulletTracer>();

            if (tracer != null)
            {
                tracer.Initialize(start, target);
                Debug.Log($"[BulletTracer] Spawned tracer from {start} to {target} (distance: {Vector3.Distance(start, target):F1}m)");
            }
            else
            {
                Debug.LogWarning("[BulletTracer] Prefab missing BulletTracer component!");
                Destroy(instance);
            }

            return tracer;
        }
    }
}
