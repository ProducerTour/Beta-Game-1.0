using UnityEngine;
using UnityEditor;
using CreatorWorld.Combat;

namespace CreatorWorld.Editor
{
    /// <summary>
    /// Editor window for adjusting weapon alignment in real-time.
    /// Works in Play mode - adjust values and copy them to the prefab.
    /// </summary>
    public class WeaponAlignmentEditor : EditorWindow
    {
        private WeaponAlignment targetWeapon;
        private Vector3 gripPosition;
        private Vector3 gripRotation;
        private Vector3 adsPositionOffset;
        private Vector3 adsRotationOffset;

        private bool livePreview = true;
        private float positionStep = 0.01f;
        private float rotationStep = 5f;

        // Saved values for copy to prefab
        private string savedValues = "";

        [MenuItem("Tools/Creator World/Weapon Alignment Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<WeaponAlignmentEditor>("Weapon Alignment");
            window.minSize = new Vector2(350, 500);
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void OnGUI()
        {
            GUILayout.Label("Weapon Alignment Tool", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "1. Enter Play mode\n" +
                "2. Equip a weapon (press 1 or 2)\n" +
                "3. Click 'Find Current Weapon'\n" +
                "4. Adjust position/rotation\n" +
                "5. Click 'Copy Values' and paste into prefab",
                MessageType.Info);

            EditorGUILayout.Space();

            // Find weapon button
            if (GUILayout.Button("Find Current Weapon", GUILayout.Height(30)))
            {
                FindCurrentWeapon();
            }

            EditorGUILayout.Space();

            // Target display
            EditorGUI.BeginDisabledGroup(true);
            targetWeapon = (WeaponAlignment)EditorGUILayout.ObjectField("Target", targetWeapon, typeof(WeaponAlignment), true);
            EditorGUI.EndDisabledGroup();

            if (targetWeapon == null)
            {
                EditorGUILayout.HelpBox("No weapon selected. Enter Play mode and equip a weapon.", MessageType.Warning);
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            // Live preview toggle
            livePreview = EditorGUILayout.Toggle("Live Preview", livePreview);

            EditorGUILayout.Space();

            // Step sizes
            EditorGUILayout.LabelField("Adjustment Steps", EditorStyles.boldLabel);
            positionStep = EditorGUILayout.FloatField("Position Step", positionStep);
            rotationStep = EditorGUILayout.FloatField("Rotation Step", rotationStep);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            // Position controls
            EditorGUILayout.LabelField("Grip Position", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("X", GUILayout.Width(20));
            if (GUILayout.Button("-", GUILayout.Width(30))) gripPosition.x -= positionStep;
            gripPosition.x = EditorGUILayout.FloatField(gripPosition.x);
            if (GUILayout.Button("+", GUILayout.Width(30))) gripPosition.x += positionStep;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Y", GUILayout.Width(20));
            if (GUILayout.Button("-", GUILayout.Width(30))) gripPosition.y -= positionStep;
            gripPosition.y = EditorGUILayout.FloatField(gripPosition.y);
            if (GUILayout.Button("+", GUILayout.Width(30))) gripPosition.y += positionStep;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Z", GUILayout.Width(20));
            if (GUILayout.Button("-", GUILayout.Width(30))) gripPosition.z -= positionStep;
            gripPosition.z = EditorGUILayout.FloatField(gripPosition.z);
            if (GUILayout.Button("+", GUILayout.Width(30))) gripPosition.z += positionStep;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // Rotation controls
            EditorGUILayout.LabelField("Grip Rotation", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("X", GUILayout.Width(20));
            if (GUILayout.Button("-", GUILayout.Width(30))) gripRotation.x -= rotationStep;
            gripRotation.x = EditorGUILayout.FloatField(gripRotation.x);
            if (GUILayout.Button("+", GUILayout.Width(30))) gripRotation.x += rotationStep;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Y", GUILayout.Width(20));
            if (GUILayout.Button("-", GUILayout.Width(30))) gripRotation.y -= rotationStep;
            gripRotation.y = EditorGUILayout.FloatField(gripRotation.y);
            if (GUILayout.Button("+", GUILayout.Width(30))) gripRotation.y += rotationStep;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Z", GUILayout.Width(20));
            if (GUILayout.Button("-", GUILayout.Width(30))) gripRotation.z -= rotationStep;
            gripRotation.z = EditorGUILayout.FloatField(gripRotation.z);
            if (GUILayout.Button("+", GUILayout.Width(30))) gripRotation.z += rotationStep;
            EditorGUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck() && livePreview && Application.isPlaying)
            {
                ApplyValues();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            // Quick presets
            EditorGUILayout.LabelField("Quick Presets", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Rifle"))
            {
                gripPosition = new Vector3(0.05f, 0.02f, 0.1f);
                gripRotation = new Vector3(0, 90, 0);
                ApplyValues();
            }
            if (GUILayout.Button("Pistol"))
            {
                gripPosition = new Vector3(0.02f, 0.01f, 0.05f);
                gripRotation = new Vector3(0, 90, 0);
                ApplyValues();
            }
            if (GUILayout.Button("Reset"))
            {
                gripPosition = Vector3.zero;
                gripRotation = Vector3.zero;
                ApplyValues();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            // Copy/Apply buttons
            EditorGUILayout.LabelField("Save Values", EditorStyles.boldLabel);

            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("Apply to Scene", GUILayout.Height(25)))
            {
                ApplyValues();
            }

            GUI.backgroundColor = Color.cyan;
            if (GUILayout.Button("Copy Values to Clipboard", GUILayout.Height(25)))
            {
                CopyValues();
            }
            GUI.backgroundColor = Color.white;

            if (!string.IsNullOrEmpty(savedValues))
            {
                EditorGUILayout.HelpBox(savedValues, MessageType.None);
            }

            EditorGUILayout.Space();

            // Instructions for applying to prefab
            EditorGUILayout.HelpBox(
                "To save permanently:\n" +
                "1. Copy values above\n" +
                "2. Exit Play mode\n" +
                "3. Select weapon prefab\n" +
                "4. Find WeaponAlignment component\n" +
                "5. Paste values into gripPosition/gripRotation",
                MessageType.Info);
        }

        private void FindCurrentWeapon()
        {
            // Find player's current weapon
            var inventory = Object.FindFirstObjectByType<WeaponInventory>();
            if (inventory != null && inventory.CurrentWeapon != null)
            {
                targetWeapon = inventory.CurrentWeapon.GetComponent<WeaponAlignment>();

                if (targetWeapon == null)
                {
                    // Add component if missing
                    targetWeapon = inventory.CurrentWeapon.gameObject.AddComponent<WeaponAlignment>();
                    Debug.Log("Added WeaponAlignment component to " + inventory.CurrentWeapon.name);
                }

                // Load current values
                gripPosition = targetWeapon.gripPosition;
                gripRotation = targetWeapon.gripRotation;

                // Also try to get current transform values
                gripPosition = targetWeapon.transform.localPosition;
                gripRotation = targetWeapon.transform.localEulerAngles;

                Debug.Log($"Found weapon: {targetWeapon.name}");
                Repaint();
            }
            else
            {
                Debug.LogWarning("No weapon currently equipped. Press 1 or 2 to equip a weapon.");
            }
        }

        private void ApplyValues()
        {
            if (targetWeapon == null) return;

            targetWeapon.gripPosition = gripPosition;
            targetWeapon.gripRotation = gripRotation;
            targetWeapon.transform.localPosition = gripPosition;
            targetWeapon.transform.localRotation = Quaternion.Euler(gripRotation);
        }

        private void CopyValues()
        {
            savedValues = $"gripPosition: ({gripPosition.x:F3}, {gripPosition.y:F3}, {gripPosition.z:F3})\n" +
                         $"gripRotation: ({gripRotation.x:F1}, {gripRotation.y:F1}, {gripRotation.z:F1})";

            string clipboardText = $"Position: new Vector3({gripPosition.x:F3}f, {gripPosition.y:F3}f, {gripPosition.z:F3}f)\n" +
                                  $"Rotation: new Vector3({gripRotation.x:F1}f, {gripRotation.y:F1}f, {gripRotation.z:F1}f)";

            EditorGUIUtility.systemCopyBuffer = clipboardText;
            Debug.Log("Values copied to clipboard:\n" + clipboardText);
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (targetWeapon == null || !Application.isPlaying) return;

            // Draw handles for position adjustment
            EditorGUI.BeginChangeCheck();

            Vector3 worldPos = targetWeapon.transform.position;
            Quaternion worldRot = targetWeapon.transform.rotation;

            // Position handle
            Vector3 newPos = Handles.PositionHandle(worldPos, worldRot);

            if (EditorGUI.EndChangeCheck())
            {
                // Convert back to local space
                Transform parent = targetWeapon.transform.parent;
                if (parent != null)
                {
                    gripPosition = parent.InverseTransformPoint(newPos);
                    ApplyValues();
                    Repaint();
                }
            }
        }
    }
}
