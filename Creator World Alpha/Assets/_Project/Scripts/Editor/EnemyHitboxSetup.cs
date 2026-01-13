#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using CreatorWorld.Enemy;
using CreatorWorld.Interfaces;

namespace CreatorWorld.Editor
{
    /// <summary>
    /// Editor tool to automatically create hitboxes on enemy characters.
    /// Finds skeleton bones and attaches appropriate colliders with EnemyHitbox components.
    /// </summary>
    public static class EnemyHitboxSetup
    {
        [MenuItem("Tools/Enemy/Setup Hitboxes on Selection")]
        public static void SetupHitboxesOnSelection()
        {
            var selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("Setup Hitboxes", "Please select an enemy GameObject in the scene.", "OK");
                return;
            }

            Undo.RegisterCompleteObjectUndo(selected, "Setup Enemy Hitboxes");

            int hitboxCount = 0;

            // Find all transforms in hierarchy
            Transform[] allTransforms = selected.GetComponentsInChildren<Transform>(true);

            foreach (Transform bone in allTransforms)
            {
                string boneName = bone.name.ToLower();

                // Head hitbox
                if (boneName.Contains("head") && !boneName.Contains("headtop"))
                {
                    CreateHitbox(bone, HitboxType.Head, 0.12f);
                    hitboxCount++;
                }
                // Spine/Chest hitbox (body)
                else if (boneName.Contains("spine2") || boneName.Contains("chest"))
                {
                    CreateHitbox(bone, HitboxType.Body, 0.2f);
                    hitboxCount++;
                }
                // Hips hitbox (body)
                else if (boneName.Contains("hips") || boneName.Contains("pelvis"))
                {
                    CreateHitbox(bone, HitboxType.Body, 0.18f);
                    hitboxCount++;
                }
            }

            // If no bones found, create basic hitboxes
            if (hitboxCount == 0)
            {
                Debug.LogWarning("[EnemyHitboxSetup] No standard bones found. Creating basic hitboxes.");
                CreateBasicHitboxes(selected.transform);
                hitboxCount = 2;
            }

            EditorUtility.SetDirty(selected);
            Debug.Log($"[EnemyHitboxSetup] Created {hitboxCount} hitboxes on {selected.name}");
        }

        private static void CreateHitbox(Transform bone, HitboxType type, float radius)
        {
            // Check if hitbox already exists
            var existingHitbox = bone.GetComponent<EnemyHitbox>();
            if (existingHitbox != null)
            {
                Debug.Log($"[EnemyHitboxSetup] Hitbox already exists on {bone.name}, skipping.");
                return;
            }

            // Add sphere collider
            var collider = bone.gameObject.AddComponent<SphereCollider>();
            collider.radius = radius;
            collider.isTrigger = false;

            // Add hitbox component
            var hitbox = bone.gameObject.AddComponent<EnemyHitbox>();

            // Set hitbox type via serialized field
            var serializedObject = new SerializedObject(hitbox);
            var hitboxTypeProperty = serializedObject.FindProperty("hitboxType");
            hitboxTypeProperty.enumValueIndex = (int)type;
            serializedObject.ApplyModifiedProperties();

            // Set layer to Enemy if it exists
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer >= 0)
            {
                bone.gameObject.layer = enemyLayer;
            }

            Debug.Log($"[EnemyHitboxSetup] Created {type} hitbox on {bone.name}");
        }

        private static void CreateBasicHitboxes(Transform root)
        {
            // Create head hitbox child
            GameObject headHitbox = new GameObject("Hitbox_Head");
            headHitbox.transform.SetParent(root);
            headHitbox.transform.localPosition = new Vector3(0, 1.6f, 0);

            var headCollider = headHitbox.AddComponent<SphereCollider>();
            headCollider.radius = 0.12f;

            var headHitboxComponent = headHitbox.AddComponent<EnemyHitbox>();
            var headSO = new SerializedObject(headHitboxComponent);
            headSO.FindProperty("hitboxType").enumValueIndex = (int)HitboxType.Head;
            headSO.ApplyModifiedProperties();

            // Create body hitbox child
            GameObject bodyHitbox = new GameObject("Hitbox_Body");
            bodyHitbox.transform.SetParent(root);
            bodyHitbox.transform.localPosition = new Vector3(0, 1.0f, 0);

            var bodyCollider = bodyHitbox.AddComponent<CapsuleCollider>();
            bodyCollider.radius = 0.25f;
            bodyCollider.height = 0.8f;

            var bodyHitboxComponent = bodyHitbox.AddComponent<EnemyHitbox>();
            var bodySO = new SerializedObject(bodyHitboxComponent);
            bodySO.FindProperty("hitboxType").enumValueIndex = (int)HitboxType.Body;
            bodySO.ApplyModifiedProperties();

            // Set layer
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer >= 0)
            {
                headHitbox.layer = enemyLayer;
                bodyHitbox.layer = enemyLayer;
            }
        }

        [MenuItem("Tools/Enemy/Remove All Hitboxes from Selection")]
        public static void RemoveHitboxesFromSelection()
        {
            var selected = Selection.activeGameObject;
            if (selected == null) return;

            Undo.RegisterCompleteObjectUndo(selected, "Remove Enemy Hitboxes");

            var hitboxes = selected.GetComponentsInChildren<EnemyHitbox>(true);
            foreach (var hitbox in hitboxes)
            {
                // Remove collider too if it's a sphere we added
                var collider = hitbox.GetComponent<Collider>();
                if (collider != null)
                {
                    Undo.DestroyObjectImmediate(collider);
                }
                Undo.DestroyObjectImmediate(hitbox);
            }

            // Remove generated hitbox GameObjects
            Transform[] children = selected.GetComponentsInChildren<Transform>(true);
            foreach (var child in children)
            {
                if (child.name.StartsWith("Hitbox_"))
                {
                    Undo.DestroyObjectImmediate(child.gameObject);
                }
            }

            Debug.Log($"[EnemyHitboxSetup] Removed hitboxes from {selected.name}");
        }
    }
}
#endif
