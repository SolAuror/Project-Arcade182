using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Sol.UI
{
    /// <summary>
    /// Selected-button dressing: nudges the button up in scale, switches its
    /// labels to a high-contrast colour, and turns on an authored outline.
    ///
    /// Authored onto every menu button alongside the UI Outline it drives —
    /// nothing here is added at runtime, so the look can be tuned in the
    /// inspector per button.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    [AddComponentMenu("Sol/UI/Arcade Button Selection Feedback")]
    public sealed class ArcadeButtonSelectionFeedback : MonoBehaviour,
        ISelectHandler,
        IDeselectHandler
    {
        [Header("Widgets")]
        [SerializeField] private Button button;
        [Tooltip("Authored outline on the button's target graphic. Left off " +
                 "until the button is selected.")]
        [SerializeField] private UnityEngine.UI.Outline selectionOutline;

        [Header("Selected Look")]
        [SerializeField] private Color selectedTextColor =
            new Color(0.035f, 0.04f, 0.065f, 1f);
        [SerializeField] private float selectedScale = 1.045f;

        private Text[] legacyLabels;
        private Color[] legacyLabelColors;
        private TMP_Text[] tmpLabels;
        private Color[] tmpLabelColors;
        private Vector3 restingScale;

        private void Awake()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }

            restingScale = transform.localScale;
            CacheLabels();
            SetSelected(false);
        }

        // A menu panel that gets re-shown re-runs OnEnable but not OnSelect, so
        // the resting look has to be restored here or a button that was
        // selected when its panel closed comes back still lit.
        private void OnEnable()
        {
            SetSelected(
                EventSystem.current != null &&
                EventSystem.current.currentSelectedGameObject == gameObject);
        }

        public void OnSelect(BaseEventData eventData)
        {
            SetSelected(button == null || button.IsInteractable());
        }

        public void OnDeselect(BaseEventData eventData)
        {
            SetSelected(false);
        }

        private void OnDisable()
        {
            SetSelected(false);
        }

        private void CacheLabels()
        {
            legacyLabels = GetComponentsInChildren<Text>(true);
            legacyLabelColors = new Color[legacyLabels.Length];
            for (int i = 0; i < legacyLabels.Length; i++)
            {
                legacyLabelColors[i] = legacyLabels[i].color;
            }

            tmpLabels = GetComponentsInChildren<TMP_Text>(true);
            tmpLabelColors = new Color[tmpLabels.Length];
            for (int i = 0; i < tmpLabels.Length; i++)
            {
                tmpLabelColors[i] = tmpLabels[i].color;
            }
        }

        private void SetSelected(bool value)
        {
            // OnEnable can land before Awake on a freshly instantiated prefab.
            if (legacyLabels == null || tmpLabels == null)
            {
                return;
            }

            transform.localScale = restingScale * (value ? selectedScale : 1f);
            if (selectionOutline != null)
            {
                selectionOutline.enabled = value;
            }

            for (int i = 0; i < legacyLabels.Length; i++)
            {
                if (legacyLabels[i] != null)
                {
                    legacyLabels[i].color = value
                        ? selectedTextColor
                        : legacyLabelColors[i];
                }
            }

            for (int i = 0; i < tmpLabels.Length; i++)
            {
                if (tmpLabels[i] != null)
                {
                    tmpLabels[i].color = value
                        ? selectedTextColor
                        : tmpLabelColors[i];
                }
            }
        }
    }
}
