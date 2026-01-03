using UnityEngine;

namespace CreatorWorld.UI.Minimap
{
    /// <summary>
    /// Marks an object to appear on the minimap.
    /// Attach to any GameObject that should show on the minimap.
    /// </summary>
    public class MinimapMarker : MonoBehaviour
    {
        public enum MarkerType
        {
            Default,
            Enemy,
            Friendly,
            Objective,
            Item
        }

        [Header("Marker Settings")]
        [SerializeField] private MarkerType type = MarkerType.Default;
        [SerializeField] private Color markerColor = Color.white;
        [SerializeField] private float markerSize = 1f;
        [SerializeField] private bool rotateWithObject = false;

        // Visual representation for minimap camera
        private GameObject markerVisual;
        private MeshRenderer markerRenderer;

        public MarkerType Type => type;
        public Color MarkerColor => markerColor;

        private void Start()
        {
            CreateMarkerVisual();
        }

        private void CreateMarkerVisual()
        {
            // Create a simple quad that will be visible to the minimap camera
            markerVisual = GameObject.CreatePrimitive(PrimitiveType.Quad);
            markerVisual.name = "MinimapMarker";
            markerVisual.transform.SetParent(transform);
            markerVisual.transform.localPosition = Vector3.up * 50f; // High up for minimap camera
            markerVisual.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // Face up
            markerVisual.transform.localScale = Vector3.one * markerSize * 5f;

            // Remove collider
            var collider = markerVisual.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            // Setup material
            markerRenderer = markerVisual.GetComponent<MeshRenderer>();
            markerRenderer.material = new Material(Shader.Find("Unlit/Color"));
            markerRenderer.material.color = GetColorForType();

            // Set layer if you have a minimap-specific layer
            // markerVisual.layer = LayerMask.NameToLayer("Minimap");
        }

        private Color GetColorForType()
        {
            if (markerColor != Color.white) return markerColor;

            return type switch
            {
                MarkerType.Enemy => Color.red,
                MarkerType.Friendly => Color.green,
                MarkerType.Objective => Color.yellow,
                MarkerType.Item => Color.cyan,
                _ => Color.white
            };
        }

        private void LateUpdate()
        {
            if (markerVisual == null) return;

            // Keep marker at fixed height above object
            markerVisual.transform.position = transform.position + Vector3.up * 50f;

            if (rotateWithObject)
            {
                markerVisual.transform.rotation = Quaternion.Euler(90f, transform.eulerAngles.y, 0f);
            }
        }

        public void SetColor(Color color)
        {
            markerColor = color;
            if (markerRenderer != null)
            {
                markerRenderer.material.color = color;
            }
        }

        public void SetSize(float size)
        {
            markerSize = size;
            if (markerVisual != null)
            {
                markerVisual.transform.localScale = Vector3.one * size * 5f;
            }
        }

        private void OnDestroy()
        {
            if (markerVisual != null)
            {
                Destroy(markerVisual);
            }
        }
    }
}
