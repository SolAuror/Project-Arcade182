using System.Collections.Generic;
using UnityEngine;

namespace Sol.Minigames
{
    /// <summary>
    /// Floor cover over a pit in the Atom Smasher arena. While closed the cover
    /// is visible and solid, so balls bounce off it like normal floor. Covers
    /// stay closed through the early waves; afterwards one random pit opens per
    /// wave, and from the late waves the roll may open left, right, or both.
    /// Balls that fall through an open pit are destroyed when they pass the
    /// bottom of the arena, exactly like the regular drain.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/Minigames/Atom Smasher Pit Trap")]
    public class AtomSmasherPitTrap : MonoBehaviour
    {
        private static readonly List<AtomSmasherPitTrap> AllPits = new List<AtomSmasherPitTrap>();
        private static int configuredWave = int.MinValue;

        [Header("Waves")]
        [Tooltip("Every pit stays covered through this wave; openings start on the next one.")]
        [SerializeField, Min(0)] private int coveredThroughWave = 5;

        [Tooltip("From this wave the per-wave roll picks evenly between each single pit and all pits at once.")]
        [SerializeField, Min(1)] private int bothOpenFromWave = 10;

        [Tooltip("Found automatically when left empty.")]
        [SerializeField] private AtomSmasherGame game;

        private Renderer[] coverRenderers;
        private Collider[] coverColliders;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            if (game == null)
            {
                game = FindFirstObjectByType<AtomSmasherGame>();
            }

            coverRenderers = GetComponentsInChildren<Renderer>(true);
            coverColliders = GetComponentsInChildren<Collider>(true);
            SetOpen(false);
        }

        private void OnEnable()
        {
            AllPits.Add(this);
        }

        private void OnDisable()
        {
            AllPits.Remove(this);
        }

        private void Update()
        {
            // The first registered pit runs the shared wave roll for all pits.
            if (game == null || AllPits.Count == 0 || AllPits[0] != this)
            {
                return;
            }

            int wave = game.WaveNumber;
            if (wave != configuredWave)
            {
                configuredWave = wave;
                ConfigureWave(wave);
            }
        }

        private void ConfigureWave(int wave)
        {
            foreach (AtomSmasherPitTrap pit in AllPits)
            {
                pit.SetOpen(false);
            }

            if (wave <= coveredThroughWave || AllPits.Count == 0)
            {
                return;
            }

            // Late waves roll evenly between each single pit and "all open";
            // before that, exactly one random pit opens.
            if (wave >= bothOpenFromWave)
            {
                int roll = Random.Range(0, AllPits.Count + 1);
                if (roll == AllPits.Count)
                {
                    foreach (AtomSmasherPitTrap pit in AllPits)
                    {
                        pit.SetOpen(true);
                    }
                }
                else
                {
                    AllPits[roll].SetOpen(true);
                }

                return;
            }

            AllPits[Random.Range(0, AllPits.Count)].SetOpen(true);
        }

        private void SetOpen(bool open)
        {
            IsOpen = open;

            foreach (Renderer coverRenderer in coverRenderers)
            {
                if (coverRenderer != null)
                {
                    coverRenderer.enabled = !open;
                }
            }

            foreach (Collider coverCollider in coverColliders)
            {
                if (coverCollider != null)
                {
                    coverCollider.enabled = !open;
                }
            }
        }
    }
}
