#nullable enable
using SchoolDirectorTycoon.Core;
using UnityEngine;

namespace SchoolDirectorTycoon.Bridge
{
    /// <summary>Turns real time into <see cref="Simulation.AdvanceDay"/> calls. Holds no rules.</summary>
    public sealed class DayClock : MonoBehaviour
    {
        public static readonly int[] Speeds = { 0, 1, 2, 4 };

        [SerializeField, Min(0.1f)] private float secondsPerDay = 10f;

        private float elapsed;

        public GameSession? Session { get; set; }

        /// <summary>Optional; its completed-save callbacks are drained on the main thread each frame.</summary>
        public SaveService? Saves { get; set; }

        public int Speed { get; private set; } = 1;

        public float SecondsPerDay
        {
            get => secondsPerDay;
            set => secondsPerDay = Mathf.Max(0.1f, value);
        }

        /// <summary>0..1 progress through the current day, for visual interpolation only.</summary>
        public float DayProgress => Mathf.Clamp01(elapsed / secondsPerDay);

        public bool IsWaiting => Session == null
            || Session.State.PendingIncident != null
            || Session.State.Outcome != Outcome.InProgress;

        public void SetSpeed(int speed)
        {
            if (System.Array.IndexOf(Speeds, speed) < 0)
                throw new System.ArgumentOutOfRangeException(nameof(speed));
            Speed = speed;
        }

        // Unscaled: Time.timeScale (menus, slow-mo effects) must not change the sim speed.
        private void Update()
        {
            Saves?.DrainCallbacks();
            Tick(Time.unscaledDeltaTime);
        }

        /// <summary>Advances the clock by <paramref name="seconds"/> of real time; public for tests.</summary>
        public void Tick(float seconds)
        {
            if (Speed == 0 || IsWaiting || seconds <= 0f) return;
            elapsed += seconds * Speed;
            if (elapsed < secondsPerDay) return;
            // One day per tick at most, so an incident can pause the next one. Carry the overflow,
            // but less than a full day, so a long hitch cannot bank extra days.
            elapsed -= secondsPerDay;
            if (elapsed >= secondsPerDay) elapsed = secondsPerDay * 0.999f;
            Session!.Dispatch(Simulation.AdvanceDay);
        }
    }
}
