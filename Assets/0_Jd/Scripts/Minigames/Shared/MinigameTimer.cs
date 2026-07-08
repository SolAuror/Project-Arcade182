using UnityEngine;
using UnityEngine.Events;

namespace Sol.Minigames
{
    /// <summary>
    /// Reusable run timer for any minigame. Stopwatch mode counts up forever;
    /// Countdown mode counts down from <see cref="countdownSeconds"/> and fires
    /// <see cref="OnExpired"/> once. Each minigame picks the mode it needs.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/Minigames/Shared/Minigame Timer")]
    public class MinigameTimer : MonoBehaviour
    {
        public enum TimerMode
        {
            Stopwatch = 0,
            Countdown = 1
        }

        [Header("Timer")]
        [Tooltip("Stopwatch counts up; Countdown counts down and fires On Expired.")]
        [SerializeField] private TimerMode mode = TimerMode.Stopwatch;

        [Tooltip("Countdown duration in seconds. Ignored in Stopwatch mode.")]
        [SerializeField, Min(0f)] private float countdownSeconds = 90f;

        [Tooltip("Begin ticking automatically when this component is enabled.")]
        [SerializeField] private bool startOnEnable;

        [Header("Events")]
        [Tooltip("Invoked once when a Countdown reaches zero.")]
        [SerializeField] private UnityEvent onExpired = new UnityEvent();

        private float elapsed;
        private bool running;
        private bool expired;

        public TimerMode Mode
        {
            get => mode;
            set => mode = value;
        }

        public float CountdownSeconds
        {
            get => countdownSeconds;
            set => countdownSeconds = Mathf.Max(0f, value);
        }

        public float Elapsed => elapsed;
        public float Remaining => mode == TimerMode.Countdown ? Mathf.Max(0f, countdownSeconds - elapsed) : 0f;
        public bool IsExpired => expired;
        public bool IsRunning => running;
        public UnityEvent OnExpired => onExpired;

        private void OnEnable()
        {
            if (startOnEnable)
            {
                Begin();
            }
        }

        private void OnValidate()
        {
            countdownSeconds = Mathf.Max(0f, countdownSeconds);
        }

        private void Update()
        {
            if (!running)
            {
                return;
            }

            elapsed += Time.deltaTime;

            if (mode == TimerMode.Countdown && !expired && elapsed >= countdownSeconds)
            {
                elapsed = countdownSeconds;
                expired = true;
                running = false;
                onExpired.Invoke();
            }
        }

        public void Begin()
        {
            elapsed = 0f;
            expired = false;
            running = true;
        }

        public void Pause()
        {
            running = false;
        }

        public void Resume()
        {
            if (!expired)
            {
                running = true;
            }
        }

        public void ResetTimer()
        {
            elapsed = 0f;
            expired = false;
            running = false;
        }
    }
}
