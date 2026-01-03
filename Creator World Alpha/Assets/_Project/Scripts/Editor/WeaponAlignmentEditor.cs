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
        private Vector3 position;
        private Vector3 rotation;
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
            if (GUILayout.Button("-", GUILayout.Width(30))) position.x -= positionStep;
            position.x = EditorGUILayout.FloatField(position.x);
            if (GUILayout.Button("+", GUILayout.Width(30))) position.x += positionStep;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Y", GUILayout.Width(20));
            if (GUILayout.Button("-", GUILayout.Width(30))) position.y -= positionStep;
            position.y = EditorGUILayout.FloatField(position.y);
            if (GUILayout.Button("+", GUILayout.Width(30))) position.y += positionStep;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Z", GUILayout.Width(20));
            if (GUILayout.Button("-", GUILayout.Width(30))) position.z -= positionStep;
            position.z = EditorGUILayout.FloatField(position.z);
            if (GUILayout.Button("+", GUILayout.Width(30))) position.z += positionStep;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // Rotation controls
            EditorGUILayout.LabelField("Grip Rotation", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("X", GUILayout.Width(20));
            if (GUILayout.Button("-", GUILayout.Width(30))) rotation.x -= rotationStep;
            rotation.x = EditorGUILayout.FloatField(rotation.x);
            if (GUILayout.Button("+", GUILayout.Width(30))) rotation.x += rotationStep;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Y", GUILayout.Width(20));
            if (GUILayout.Button("-", GUILayout.Width(30))) rotation.y -= rotationStep;
            rotation.y = EditorGUILayout.FloatField(rotation.y);
            if (GUILayout.Button("+", GUILayout.Width(30))) rotation.y += rotationStep;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Z", GUILayout.Width(20));
            if (GUILayout.Button("-", GUILayout.Width(30))) rotation.z -= rotationStep;
            rotation.z = EditorGUILayout.FloatField(rotation.z);
            if (GUILayout.Button("+", GUILayout.Width(30))) rotation.z += rotationStep;
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
                position = new Vector3(0.05f, 0.02f, 0.1f);
                rotation = new Vector3(0, 90, 0);
                ApplyValues();
            }
            if (GUILayout.Button("Pistol"))
            {
                position = new Vector3(0.02f, 0.01f, 0.05f);
                rotation = new Vector3(0, 90, 0);
                ApplyValues();
            }
            if (GUILayout.Button("Reset"))
            {
                position = Vector3.zero;
                rotation = Vector3.zero;
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
                position = targetWeapon.gripPosition;
                rotation = targetWeapon.gripRotation;

                // Also try to get current transform values
                position = targetWeapon.transform.localPosition;
                rotation = targetWeapon.transform.localEulerAngles;

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

            targetWeapon.gripPosition = position;
            targetWeapon.gripRotation = rotation;
            targetWeapon.transform.localPosition = position;
            targetWeapon.transform.localRotation = Quaternion.Euler(rotation);
        }

        private void CopyValues()
        {
            savedValues = $"gripPosition: ({position.x:F3}, {position.y:F3}, {position.z:F3})\n" +
                         $"gripRotation: ({rotation.x:F1}, {rotation.y:F1}, {rotation.z:F1})";

            string clipboardText = $"Position: new Vector3({position.x:F3}f, {position.y:F3}f, {position.z:F3}f)\n" +
                                  $"Rotation: new Vector3({rotation.x:F1}f, {rotation.y:F1}f, {rotation.z:F1}f)";

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
                    position = parent.InverseTransformPoint(newPos);
                    ApplyValues();
                    Repaint();
                }
            }
        }
    }
}
