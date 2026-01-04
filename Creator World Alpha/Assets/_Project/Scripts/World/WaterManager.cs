using UnityEngine;

namespace CreatorWorld.World
{
    /// <summary>
    /// Manages ocean water plane with stylized water shader.
    /// Creates an infinite-appearing ocean at the configured water level.
    /// </summary>
    public class WaterManager : MonoBehaviour
    {
        [Header("Water Settings")]
        [SerializeField] private Material waterMaterial;
        [SerializeField] private float waterSize = 2000f;
        [SerializeField] private Color shallowColor = new Color(0.2f, 0.6f, 0.8f, 0.9f);
        [SerializeField] private Color deepColor = new Color(0.05f, 0.2f, 0.4f, 0.95f);

        [Header("Follow Camera")]
        [SerializeField] private bool followCamera = true;
        [SerializeField] private float updateInterval = 0.5f;

        private GameObject waterPlane;
        private Camera mainCamera;
        private float lastUpdateTime;
        private Material instanceMaterial;

        private void Start()
        {
            mainCamera = Camera.main;
            CreateWaterPlane();
        }

        private void CreateWaterPlane()
        {
            // Create water plane GameObject
            waterPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            waterPlane.name = "Ocean";
            waterPlane.transform.parent = transform;

            // Position at water level
            waterPlane.transform.position = new Vector3(0, TerrainGenerator.WaterLevel, 0);

            // Scale to cover large area (plane is 10x10 by default)
            float scale = waterSize / 10f;
            waterPlane.transform.localScale = new Vector3(scale, 1f, scale);

            // Remove collider
            var collider = waterPlane.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            // Apply material
            var renderer = waterPlane.GetComponent<MeshRenderer>();
            if (waterMaterial != null)
            {
                instanceMaterial = new Material(waterMaterial);
                renderer.material = instanceMaterial;
            }
            else
            {
                instanceMaterial = CreateWaterMaterial();
                renderer.material = instanceMaterial;
            }

            Debug.Log($"[WaterManager] Ocean created at Y={TerrainGenerator.WaterLevel}, size={waterSize}m");
        }

        private Material CreateWaterMaterial()
        {
            // Try to use URP Lit shader with transparency
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material mat = new Material(shader);
            mat.name = "OceanWater";

            // Set up for transparency
            mat.SetFloat("_Surface", 1); // Transparent
            mat.SetFloat("_Blend", 0); // Alpha blend
            mat.SetFloat("_AlphaClip", 0);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = 3000;

            // Set color (blend between shallow and deep)
            Color waterColor = Color.Lerp(shallowColor, deepColor, 0.5f);
            mat.SetColor("_BaseColor", waterColor);
            mat.color = waterColor;

            // High smoothness for reflections
            mat.SetFloat("_Smoothness", 0.95f);
            mat.SetFloat("_Metallic", 0.1f);

            return mat;
        }

        private void Update()
        {
            if (!followCamera || mainCamera == null || waterPlane == null) return;

            // Only update position periodically
            if (Time.time - lastUpdateTime < updateInterval) return;
            lastUpdateTime = Time.time;

            // Move water plane to follow camera
            Vector3 cameraPos = mainCamera.transform.position;
            waterPlane.transform.position = new Vector3(
                cameraPos.x,
                TerrainGenerator.WaterLevel,
                cameraPos.z
            );
        }

        /// <summary>
        /// Get current water level
        /// </summary>
        public float GetWaterLevel()
        {
            return TerrainGenerator.WaterLevel;
        }

        /// <summary>
        /// Set water colors at runtime
        /// </summary>
        public void SetWaterColors(Color shallow, Color deep)
        {
            shallowColor = shallow;
            deepColor = deep;

            if (instanceMaterial != null)
            {
                Color waterColor = Color.Lerp(shallowColor, deepColor, 0.5f);
                instanceMaterial.SetColor("_BaseColor", waterColor);
                instanceMaterial.color = waterColor;
            }
        }

        private void OnDestroy()
        {
            if (waterPlane != null)
            {
                Destroy(waterPlane);
            }
            if (instanceMaterial != null)
            {
                Destroy(instanceMaterial);
            }
        }
    }
}
