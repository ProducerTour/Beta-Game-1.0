using UnityEngine;
using UnityEngine.InputSystem;

namespace CreatorWorld.UI.Minimap
{
    /// <summary>
    /// Configuration for the minimap system.
    /// </summary>
    [CreateAssetMenu(fileName = "MinimapSettings", menuName = "Creator World/Minimap Settings")]
    public class MinimapSettings : ScriptableObject
    {
        [Header("Camera")]
        [Tooltip("Height above the player")]
        public float cameraHeight = 100f;

        [Tooltip("Default zoom (orthographic size)")]
        public float defaultZoom = 50f;

        [Tooltip("Minimum zoom (closest)")]
        public float minZoom = 20f;

        [Tooltip("Maximum zoom (furthest)")]
        public float maxZoom = 150f;

        [Tooltip("Zoom step amount")]
        public float zoomStep = 10f;

        [Header("Display")]
        [Tooltip("Render texture resolution")]
        public int resolution = 512;

        [Tooltip("Minimap size in pixels")]
        public float minimapSize = 400f;

        [Tooltip("Screen padding")]
        public float padding = 20f;

        [Header("Visuals")]
        public Color borderColor = new Color(0.2f, 0.2f, 0.2f, 1f);
        public float borderWidth = 4f;
        public Color playerIconColor = Color.yellow;
        public float playerIconSize = 16f;

        [Header("Input")]
        public Key toggleFullscreenKey = Key.M;
        public Key zoomInKey = Key.Equals;
        public Key zoomOutKey = Key.Minus;
    }
}
