using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Sol.Minigames
{
    /// <summary>
    /// Every board element the spectrometer can introduce, in the order the
    /// wave ladder reveals them. Enum order is the announcement order when a
    /// wave debuts several kinds at once.
    /// </summary>
    public enum AtomSmasherParticleKind
    {
        StableAtom,
        QuantumState,
        ContainmentBar,
        SweepBar,
        DriftingAtom,
        RotorArray,
        UnstableIsotope,
        Singularity,
        OrbitalCluster,
        VolatileNucleus,
        Wormhole,
        PolarityArray
    }

    /// <summary>
    /// Drives the diegetic "spectrometer" card that introduces each atom and
    /// apparatus the first time it appears in a run. All visuals live in the
    /// AtomSmasherHud prefab; this only fills the widgets and animates the
    /// card in and out while draining the game's announcement queue.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/Minigames/Atom Smasher/Particle Log")]
    public class AtomSmasherParticleLog : MonoBehaviour
    {
        public enum EntryCategory
        {
            Particle,
            Apparatus,
            Anomaly
        }

        [System.Serializable]
        public class Entry
        {
            public AtomSmasherParticleKind kind;
            public EntryCategory category;
            [Tooltip("Display name, e.g. UNSTABLE ISOTOPE.")]
            public string title;
            [Tooltip("Pseudo isotope tag shown after the name, e.g. T-3.")]
            public string symbol;
            [TextArea] public string description;
            public Sprite icon;
            public Color accent = Color.cyan;
        }

        [Header("Game")]
        [Tooltip("Found automatically when left empty.")]
        [SerializeField] private AtomSmasherGame game;

        [Header("Card Widgets")]
        [SerializeField] private RectTransform cardRoot;
        [SerializeField] private CanvasGroup cardGroup;
        [SerializeField] private Image headerBand;
        [SerializeField] private Text headerText;
        [SerializeField] private Text symbolText;
        [SerializeField] private Image iconImage;
        [SerializeField] private Text titleText;
        [SerializeField] private Text bodyText;

        [Header("Timing")]
        [SerializeField, Min(0.05f)] private float slideSeconds = 0.28f;
        [SerializeField, Min(1f)] private float holdSeconds = 6.5f;
        [SerializeField, Min(0f)] private float slideDistance = 64f;

        [Header("Entries")]
        [SerializeField] private List<Entry> entries = new List<Entry>();

        private enum Phase
        {
            Hidden,
            SlideIn,
            Hold,
            SlideOut
        }

        private Phase phase = Phase.Hidden;
        private float phaseStartTime;
        private Vector2 cardHomePosition;
        private bool homeCaptured;

        private void Awake()
        {
            if (game == null)
            {
                game = FindFirstObjectByType<AtomSmasherGame>();
            }

            CaptureHome();
            SetHidden();
        }

        private void CaptureHome()
        {
            if (!homeCaptured && cardRoot != null)
            {
                cardHomePosition = cardRoot.anchoredPosition;
                homeCaptured = true;
            }
        }

        private void SetHidden()
        {
            phase = Phase.Hidden;
            if (cardGroup != null)
            {
                cardGroup.alpha = 0f;
            }

            if (cardRoot != null)
            {
                cardRoot.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (game == null)
            {
                return;
            }

            // The end-of-run report owns the screen; drop anything unread.
            if (game.IsComplete || game.HasFailed)
            {
                while (game.TryDequeueParticleAnnouncement(out _))
                {
                }

                if (phase != Phase.Hidden)
                {
                    SetHidden();
                }

                return;
            }

            float now = Time.unscaledTime;

            switch (phase)
            {
                case Phase.Hidden:
                    TryShowNext(now);
                    break;

                case Phase.SlideIn:
                    if (AnimateSlide(now, entering: true))
                    {
                        phase = Phase.Hold;
                        phaseStartTime = now;
                    }

                    break;

                case Phase.Hold:
                    if (now - phaseStartTime >= holdSeconds)
                    {
                        phase = Phase.SlideOut;
                        phaseStartTime = now;
                    }

                    break;

                case Phase.SlideOut:
                    if (AnimateSlide(now, entering: false))
                    {
                        SetHidden();
                        TryShowNext(now);
                    }

                    break;
            }
        }

        private void TryShowNext(float now)
        {
            while (game.TryDequeueParticleAnnouncement(out AtomSmasherParticleKind kind))
            {
                Entry entry = FindEntry(kind);
                if (entry == null)
                {
                    continue; // no card authored for this kind; skip silently
                }

                Populate(entry);
                CaptureHome();

                if (cardRoot != null)
                {
                    cardRoot.gameObject.SetActive(true);
                }

                phase = Phase.SlideIn;
                phaseStartTime = now;
                AnimateSlide(now, entering: true);
                return;
            }
        }

        private Entry FindEntry(AtomSmasherParticleKind kind)
        {
            foreach (Entry entry in entries)
            {
                if (entry != null && entry.kind == kind)
                {
                    return entry;
                }
            }

            return null;
        }

        private void Populate(Entry entry)
        {
            if (headerText != null)
            {
                headerText.text = entry.category switch
                {
                    EntryCategory.Apparatus => "APPARATUS ONLINE",
                    EntryCategory.Anomaly => "ANOMALY DETECTED",
                    _ => "PARTICLE ISOLATED"
                };
            }

            if (headerBand != null)
            {
                headerBand.color = entry.accent;
            }

            if (symbolText != null)
            {
                symbolText.text = entry.symbol ?? string.Empty;
            }

            if (titleText != null)
            {
                titleText.text = entry.title;
                titleText.color = entry.accent;
            }

            if (bodyText != null)
            {
                bodyText.text = entry.description;
            }

            if (iconImage != null)
            {
                iconImage.sprite = entry.icon;
                iconImage.enabled = entry.icon != null;
            }
        }

        // Returns true once the slide phase has finished.
        private bool AnimateSlide(float now, bool entering)
        {
            float progress = Mathf.Clamp01((now - phaseStartTime) / slideSeconds);
            float eased = entering
                ? 1f - (1f - progress) * (1f - progress)
                : progress * progress;
            float shown = entering ? eased : 1f - eased;

            if (cardGroup != null)
            {
                cardGroup.alpha = shown;
            }

            if (cardRoot != null)
            {
                cardRoot.anchoredPosition = cardHomePosition + new Vector2((1f - shown) * slideDistance, 0f);
            }

            return progress >= 1f;
        }

#if UNITY_EDITOR
        /// <summary>One-shot wiring hook for the authoring script.</summary>
        public void EditorConfigure(
            RectTransform root,
            CanvasGroup group,
            Image band,
            Text header,
            Text symbol,
            Image icon,
            Text title,
            Text body,
            List<Entry> authoredEntries)
        {
            cardRoot = root;
            cardGroup = group;
            headerBand = band;
            headerText = header;
            symbolText = symbol;
            iconImage = icon;
            titleText = title;
            bodyText = body;
            entries = authoredEntries;
        }
#endif
    }
}
