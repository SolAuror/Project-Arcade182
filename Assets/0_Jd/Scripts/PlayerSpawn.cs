using System.Collections.Generic;
using UnityEngine;

namespace Sol
{
    /// <summary>
    /// Simple scene-start spawn point for the shared player or any assigned prefab.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/Player Spawn")]
    public class PlayerSpawn : MonoBehaviour
    {
        // Prevents maze regeneration from cloning the same player prefab repeatedly.
        private static readonly Dictionary<GameObject, GameObject> SpawnedInstancesByPrefab = new Dictionary<GameObject, GameObject>();

        [Header("Spawn")]
        [Tooltip("Prefab spawned at this marker when the scene starts.")]
        [SerializeField] private GameObject prefabToSpawn;

        public GameObject SpawnedInstance { get; private set; }

        private void Start()
        {
            SpawnOrReuse(false);
        }

        public GameObject RespawnExistingAtThisSpawn()
        {
            return SpawnOrReuse(true);
        }

        private GameObject SpawnOrReuse(bool moveExistingToSpawn)
        {
            if (prefabToSpawn == null)
            {
                Debug.LogWarning($"{name} has no prefab assigned to spawn.", this);
                return null;
            }

            if (TryGetExistingSpawnedInstance(out GameObject existingInstance))
            {
                SpawnedInstance = existingInstance;

                if (moveExistingToSpawn)
                {
                    MoveInstanceToSpawn(SpawnedInstance);
                }

                return SpawnedInstance;
            }

            SpawnedInstance = Instantiate(prefabToSpawn, transform.position, transform.rotation);
            SpawnedInstancesByPrefab[prefabToSpawn] = SpawnedInstance;
            return SpawnedInstance;
        }

        private void MoveInstanceToSpawn(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            Rigidbody rb = instance.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            CharacterController characterController = instance.GetComponent<CharacterController>();
            bool wasCharacterControllerEnabled = characterController != null && characterController.enabled;

            // CharacterController must be disabled before direct teleporting.
            if (wasCharacterControllerEnabled)
            {
                characterController.enabled = false;
            }

            instance.transform.SetPositionAndRotation(transform.position, transform.rotation);

            if (wasCharacterControllerEnabled)
            {
                characterController.enabled = true;
            }
        }

        private bool TryGetExistingSpawnedInstance(out GameObject existingInstance)
        {
            if (SpawnedInstancesByPrefab.TryGetValue(prefabToSpawn, out existingInstance))
            {
                if (existingInstance != null)
                {
                    return true;
                }

                SpawnedInstancesByPrefab.Remove(prefabToSpawn);
            }

            existingInstance = FindExistingInstanceByPrefabName();
            if (existingInstance == null)
            {
                return false;
            }

            SpawnedInstancesByPrefab[prefabToSpawn] = existingInstance;
            return true;
        }

        private GameObject FindExistingInstanceByPrefabName()
        {
            string cloneName = $"{prefabToSpawn.name}(Clone)";
            Transform[] sceneTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (Transform sceneTransform in sceneTransforms)
            {
                if (sceneTransform.gameObject == gameObject)
                {
                    continue;
                }

                if (sceneTransform.name == prefabToSpawn.name || sceneTransform.name == cloneName)
                {
                    return sceneTransform.gameObject;
                }
            }

            return null;
        }
    }
}
