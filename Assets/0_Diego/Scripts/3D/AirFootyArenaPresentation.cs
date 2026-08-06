using UnityEngine;

[DisallowMultipleComponent]
public class AirFootyArenaPresentation : MonoBehaviour
{
    // Authoring data retained for the editor bake tool. Fixed pitch markings are
    // no longer constructed during play.
    [SerializeField] private Color pitchLineColor = new Color(0.18f, 0.9f, 1f, 0.8f);
    [SerializeField] private Color playerColor = new Color(0.1f, 0.55f, 1f, 0.85f);
    [SerializeField] private Color aiColor = new Color(1f, 0.18f, 0.25f, 0.85f);
}
