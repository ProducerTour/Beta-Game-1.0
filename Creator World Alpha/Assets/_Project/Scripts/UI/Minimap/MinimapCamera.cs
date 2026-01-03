using UnityEngine;

namespace CreatorWorld.UI.Minimap
{
    /// <summary>
    /// Orthographic camera that renders the minimap view.
    /// Follows a target transform from above.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class MinimapCamera : MonoBehaviour
    {
        private Camera cam;
        private RenderTexture renderTexture;
        private Transform target;
        private float height;
        private float zoom;

        public RenderTexture RenderTexture => renderTexture;
        public float Zoom => zoom;

        public void Initialize(MinimapSettings settings, Transform followTarget)
        {
            target = followTarget;
            height = settings.cameraHeight;
            zoom = settings.defaultZoom;

            // Setup camera
            cam = GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = zoom;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.15f, 0.1f, 1f);
            cam.cullingMask = ~0; // Render everything
            cam.depth = -10; // Render before main camera

            // Create render texture
            renderTexture = new RenderTexture(settings.resolution, settings.resolution, 16);
            renderTexture.filterMode = FilterMode.Bilinear;
            cam.targetTexture = renderTexture;

            // Position camera
            UpdatePosition();
        }

        private void LateUpdate()
        {
            UpdatePosition();
        }

        private void UpdatePosition()
        {
            if (target == null) return;

            // Position above target, looking down
            transform.position = target.position + Vector3.up * height;
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        public void SetZoom(float newZoom)
        {
            zoom = newZoom;
            if (cam != null)
            {
                cam.orthographicSize = zoom;
            }
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        private void OnDestroy()
        {
            if (renderTexture != null)
            {
                renderTexture.Release();
                Destroy(renderTexture);
            }
        }
    }
}
