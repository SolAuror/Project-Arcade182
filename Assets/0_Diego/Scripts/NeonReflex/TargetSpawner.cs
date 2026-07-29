using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NeonReflex
{
    public class TargetSpawner : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private ReactionTarget targetPrefab;
        [SerializeField] private Transform[] spawnPoints;

        private readonly List<ReactionTarget> activeTargets = new List<ReactionTarget>();
        private Coroutine spawnRoutine;
        private int amountToSpawn;
        private int amountSpawned;
        private int availablePoints;
        private int maximumTargets;
        private float targetSize;
        private float targetLifetime;
        private float spawnDelay;
        private float fakeChance;

        public void StartLevel(int level, int targetAmount)
        {
            StopSpawning();
            amountToSpawn = targetAmount;
            amountSpawned = 0;
            SetDifficulty(level);
            spawnRoutine = StartCoroutine(SpawnTargets());
        }

        private void SetDifficulty(int level)
        {
            targetSize = 1.4f;
            targetLifetime = 2.5f;
            spawnDelay = 0.65f;
            maximumTargets = 1;
            availablePoints = Mathf.Min(6, spawnPoints.Length);
            fakeChance = 0f;

            if (level >= 2)
            {
                targetSize = 1.05f;
                targetLifetime = 2f;
                spawnDelay = 0.45f;
                availablePoints = Mathf.Min(9, spawnPoints.Length);
            }

            if (level >= 3)
            {
                targetSize = 0.85f;
                targetLifetime = 1.5f;
                spawnDelay = 0.35f;
                availablePoints = spawnPoints.Length;
            }

            if (level >= 4)
            {
                targetSize = 0.75f;
                targetLifetime = 1.35f;
                maximumTargets = 2;
            }

            if (level >= 5)
            {
                targetSize = 0.65f;
                targetLifetime = 1.2f;
                spawnDelay = 0.25f;
                fakeChance = 0.25f;
            }
        }

        private IEnumerator SpawnTargets()
        {
            while (amountSpawned < amountToSpawn)
            {
                activeTargets.RemoveAll(target => target == null);

                if (activeTargets.Count < maximumTargets)
                {
                    SpawnOneTarget();
                    amountSpawned++;
                }

                yield return new WaitForSeconds(spawnDelay);
            }
        }

        private void SpawnOneTarget()
        {
            Transform point = FindFreeSpawnPoint();
            if (point == null) return;

            ReactionTarget target = Instantiate(targetPrefab, point.position, point.rotation);
            bool isFake = Random.value < fakeChance;
            target.transform.localScale = Vector3.one * targetSize;
            target.Setup(gameManager, this, targetLifetime, isFake);
            activeTargets.Add(target);
        }

        private Transform FindFreeSpawnPoint()
        {
            if (availablePoints == 0) return null;

            for (int attempt = 0; attempt < 20; attempt++)
            {
                Transform point = spawnPoints[Random.Range(0, availablePoints)];
                bool positionIsFree = true;

                foreach (ReactionTarget target in activeTargets)
                {
                    if (target != null && Vector3.Distance(point.position, target.transform.position) < targetSize * 1.5f)
                    {
                        positionIsFree = false;
                    }
                }

                if (positionIsFree) return point;
            }

            return spawnPoints[Random.Range(0, availablePoints)];
        }

        public void RemoveTarget(ReactionTarget target)
        {
            activeTargets.Remove(target);
        }

        public void StopSpawning()
        {
            if (spawnRoutine != null) StopCoroutine(spawnRoutine);
            spawnRoutine = null;

            foreach (ReactionTarget target in activeTargets)
            {
                if (target != null) Destroy(target.gameObject);
            }

            activeTargets.Clear();
        }
    }
}
