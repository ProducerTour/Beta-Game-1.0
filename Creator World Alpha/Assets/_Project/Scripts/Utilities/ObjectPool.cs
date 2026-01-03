using System.Collections.Generic;
using UnityEngine;

namespace CreatorWorld.Utilities
{
    /// <summary>
    /// Generic object pool for reusing GameObjects.
    /// Use for projectiles, particles, damage numbers, etc.
    /// </summary>
    public class ObjectPool : MonoBehaviour
    {
        [SerializeField] private GameObject prefab;
        [SerializeField] private int initialSize = 20;
        [SerializeField] private int maxSize = 100;
        [SerializeField] private bool autoExpand = true;

        private Queue<GameObject> pool = new Queue<GameObject>();
        private List<GameObject> activeObjects = new List<GameObject>();
        private Transform poolContainer;

        public int AvailableCount => pool.Count;
        public int ActiveCount => activeObjects.Count;
        public int TotalCount => pool.Count + activeObjects.Count;

        private void Awake()
        {
            poolContainer = new GameObject($"{prefab.name}_Pool").transform;
            poolContainer.parent = transform;

            // Pre-populate pool
            for (int i = 0; i < initialSize; i++)
            {
                CreateObject();
            }
        }

        private GameObject CreateObject()
        {
            GameObject obj = Instantiate(prefab, poolContainer);
            obj.SetActive(false);
            pool.Enqueue(obj);
            return obj;
        }

        /// <summary>
        /// Get an object from the pool
        /// </summary>
        public GameObject Get()
        {
            GameObject obj;

            if (pool.Count > 0)
            {
                obj = pool.Dequeue();
            }
            else if (autoExpand && TotalCount < maxSize)
            {
                // Create new object directly without adding to pool
                obj = Instantiate(prefab, poolContainer);
            }
            else
            {
                Debug.LogWarning($"[ObjectPool] Pool exhausted for {prefab.name}");
                return null;
            }

            obj.SetActive(true);
            activeObjects.Add(obj);
            return obj;
        }

        /// <summary>
        /// Get an object and set its position/rotation
        /// </summary>
        public GameObject Get(Vector3 position, Quaternion rotation)
        {
            GameObject obj = Get();
            if (obj != null)
            {
                obj.transform.position = position;
                obj.transform.rotation = rotation;
            }
            return obj;
        }

        /// <summary>
        /// Return an object to the pool
        /// </summary>
        public void Return(GameObject obj)
        {
            if (obj == null) return;

            obj.SetActive(false);
            obj.transform.parent = poolContainer;

            activeObjects.Remove(obj);
            pool.Enqueue(obj);
        }

        /// <summary>
        /// Return all active objects to the pool
        /// </summary>
        public void ReturnAll()
        {
            foreach (var obj in activeObjects.ToArray())
            {
                Return(obj);
            }
        }

        /// <summary>
        /// Clear the pool and destroy all objects
        /// </summary>
        public void Clear()
        {
            foreach (var obj in pool)
            {
                if (obj != null) Destroy(obj);
            }
            foreach (var obj in activeObjects)
            {
                if (obj != null) Destroy(obj);
            }

            pool.Clear();
            activeObjects.Clear();
        }
    }

    /// <summary>
    /// Component to auto-return pooled objects after a delay
    /// </summary>
    public class PooledObject : MonoBehaviour
    {
        [SerializeField] private float lifetime = 5f;

        private ObjectPool pool;
        private float spawnTime;

        public void Initialize(ObjectPool pool)
        {
            this.pool = pool;
            spawnTime = Time.time;
        }

        private void Update()
        {
            if (Time.time - spawnTime >= lifetime)
            {
                ReturnToPool();
            }
        }

        public void ReturnToPool()
        {
            if (pool != null)
            {
                pool.Return(gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
}
