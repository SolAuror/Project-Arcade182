using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Sol.Minigames
{
    /// <summary>
    /// Prefab-authored 1-of-3 reward picker. Lives on the (initially inactive)
    /// upgrade panel inside the LabyrinthCrawlerHud prefab; the game pauses
    /// while it is open. Cards are picked by clicking or pressing 1/2/3.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/Minigames/Labyrinth Crawler/Upgrade Screen")]
    public class LabyrinthUpgradeScreen : MonoBehaviour
    {
        [Serializable]
        public class UpgradeCardWidget
        {
            public GameObject root;
            public Text titleText;
            public Text descriptionText;
            public Button takeButton;
        }

        [Header("Cards")]
        [SerializeField] private List<UpgradeCardWidget> cards = new List<UpgradeCardWidget>();

        private List<LabyrinthUpgrade> choices;
        private Action<LabyrinthUpgrade> onPicked;
        private CursorLockMode previousLockState;
        private bool previousCursorVisible;

        public bool IsOpen { get; private set; }

        public void Show(List<LabyrinthUpgrade> upgradeChoices, Action<LabyrinthUpgrade> pickedCallback)
        {
            choices = upgradeChoices;
            onPicked = pickedCallback;

            if (choices == null || choices.Count == 0)
            {
                pickedCallback?.Invoke(null);
                return;
            }

            IsOpen = true;

            for (int i = 0; i < cards.Count; i++)
            {
                UpgradeCardWidget card = cards[i];
                if (card == null || card.root == null)
                {
                    continue;
                }

                bool used = i < choices.Count;
                card.root.SetActive(used);
                if (!used)
                {
                    continue;
                }

                if (card.titleText != null)
                {
                    card.titleText.text = choices[i].Title;
                }

                if (card.descriptionText != null)
                {
                    card.descriptionText.text = choices[i].Description;
                }

                if (card.takeButton != null)
                {
                    int index = i;
                    card.takeButton.onClick.RemoveAllListeners();
                    card.takeButton.onClick.AddListener(() => Pick(index));
                }
            }

            previousLockState = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            gameObject.SetActive(true);
        }

        private void Update()
        {
            if (!IsOpen || Keyboard.current == null)
            {
                return;
            }

            if (Keyboard.current.digit1Key.wasPressedThisFrame)
            {
                Pick(0);
            }
            else if (Keyboard.current.digit2Key.wasPressedThisFrame && choices.Count > 1)
            {
                Pick(1);
            }
            else if (Keyboard.current.digit3Key.wasPressedThisFrame && choices.Count > 2)
            {
                Pick(2);
            }
        }

        private void Pick(int index)
        {
            if (!IsOpen || index < 0 || index >= choices.Count)
            {
                return;
            }

            LabyrinthUpgrade picked = choices[index];
            IsOpen = false;
            choices = null;

            Cursor.lockState = previousLockState;
            Cursor.visible = previousCursorVisible;

            gameObject.SetActive(false);

            Action<LabyrinthUpgrade> callback = onPicked;
            onPicked = null;
            callback?.Invoke(picked);
        }
    }
}
