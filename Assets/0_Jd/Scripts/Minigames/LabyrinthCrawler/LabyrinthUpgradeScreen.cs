using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sol.Minigames
{
    /// <summary>
    /// Minimal IMGUI 1-of-3 reward picker. Gameplay is paused by the game while
    /// this is open; cards are picked by clicking or pressing 1/2/3.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/Minigames/Labyrinth Crawler/Upgrade Screen")]
    public class LabyrinthUpgradeScreen : MonoBehaviour
    {
        private List<LabyrinthUpgrade> choices;
        private Action<LabyrinthUpgrade> onPicked;
        private CursorLockMode previousLockState;
        private bool previousCursorVisible;

        public bool IsOpen { get; private set; }

        public void Show(List<LabyrinthUpgrade> upgradeChoices, Action<LabyrinthUpgrade> pickedCallback)
        {
            choices = upgradeChoices;
            onPicked = pickedCallback;
            IsOpen = choices != null && choices.Count > 0;

            if (!IsOpen)
            {
                pickedCallback?.Invoke(null);
                return;
            }

            previousLockState = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
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

        private void OnGUI()
        {
            if (!IsOpen)
            {
                return;
            }

            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            const float cardWidth = 240f;
            const float cardHeight = 190f;
            const float spacing = 24f;
            int count = choices.Count;
            float totalWidth = count * cardWidth + (count - 1) * spacing;
            float startX = (Screen.width - totalWidth) * 0.5f;
            float y = (Screen.height - cardHeight) * 0.5f;

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            GUIStyle bodyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperCenter,
                wordWrap = true
            };
            GUIStyle headerStyle = new GUIStyle(titleStyle) { fontSize = 24 };

            GUI.Label(new Rect(0f, y - 70f, Screen.width, 40f), "Stage clear! Choose an upgrade", headerStyle);

            for (int i = 0; i < count; i++)
            {
                Rect card = new Rect(startX + i * (cardWidth + spacing), y, cardWidth, cardHeight);
                GUI.Box(card, GUIContent.none);
                GUI.Label(new Rect(card.x + 10f, card.y + 14f, card.width - 20f, 48f), choices[i].Title, titleStyle);
                GUI.Label(new Rect(card.x + 12f, card.y + 66f, card.width - 24f, 70f), choices[i].Description, bodyStyle);

                if (GUI.Button(new Rect(card.x + 40f, card.yMax - 42f, card.width - 80f, 30f), $"Take [{i + 1}]"))
                {
                    Pick(i);
                }
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

            Action<LabyrinthUpgrade> callback = onPicked;
            onPicked = null;
            callback?.Invoke(picked);
        }
    }
}
