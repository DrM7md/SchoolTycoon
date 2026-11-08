#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SchoolDirectorTycoon.Core;
using UnityEngine;

namespace SchoolDirectorTycoon.Bridge
{
    /// <summary>
    /// Wraps Core's <see cref="SaveStore"/> and auto-saves after each completed day. The immutable state is
    /// captured on the main thread and written by one background worker; the latest pending state per slot wins.
    /// Failures are queued and raised through <see cref="SaveFailed"/> when <see cref="DrainCallbacks"/> runs on the main thread.
    /// </summary>
    public sealed class SaveService : IDisposable
    {
        private readonly GameSession session;
        private readonly SaveStore store;
        private readonly object gate = new object();
        private readonly Dictionary<SaveSlot, SchoolState> pending = new Dictionary<SaveSlot, SchoolState>();
        private readonly Queue<Exception> failures = new Queue<Exception>();
        private Task worker = Task.CompletedTask;
        private bool running;
        private bool disposed;

        public SaveService(GameSession session, string directory)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            store = new SaveStore(directory);
            session.StateChanged += OnStateChanged;
            Application.quitting += OnQuitting;
        }

        public static SaveService ForPlayer(GameSession session) =>
            new SaveService(session, System.IO.Path.Combine(Application.persistentDataPath, "Saves"));

        /// <summary>Raised on the main thread (from <see cref="DrainCallbacks"/>) when a write failed; the game keeps running.</summary>
        public event Action<Exception>? SaveFailed;

        public bool HasPendingSaves
        {
            get { lock (gate) return running || pending.Count > 0; }
        }

        /// <summary>Queues the current state for <paramref name="slot"/>; it is written in the background.</summary>
        public void Save(SaveSlot slot) => Enqueue(slot, session.State);

        /// <summary>Synchronous read. Waits for pending writes first so a load sees the latest save.</summary>
        public SchoolState Load(SaveSlot slot)
        {
            Flush();
            return store.Read(slot);
        }

        /// <summary>Blocks until every queued save is written or <paramref name="timeout"/> (default 10 s) passes. For quit and tests.</summary>
        /// <returns>False when the timeout elapsed first.</returns>
        public bool Flush(TimeSpan? timeout = null)
        {
            DateTime deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));
            while (true)
            {
                Task current;
                lock (gate)
                {
                    if (!running && pending.Count == 0) return true;
                    current = worker;
                }
                TimeSpan left = deadline - DateTime.UtcNow;
                if (left <= TimeSpan.Zero) return false;
                try { current.Wait(left); } catch (AggregateException) { }
            }
        }

        /// <summary>Raises queued <see cref="SaveFailed"/> callbacks. Call from the main thread; DayClock does it every frame.</summary>
        public void DrainCallbacks()
        {
            while (true)
            {
                Exception error;
                lock (gate)
                {
                    if (failures.Count == 0) return;
                    error = failures.Dequeue();
                }
                SaveFailed?.Invoke(error);
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            session.StateChanged -= OnStateChanged;
            Application.quitting -= OnQuitting;
            Flush();
            DrainCallbacks();
        }

        private void OnQuitting() => Flush();

        private void OnStateChanged(SchoolState old, SchoolState next)
        {
            if (next.Day > old.Day) Enqueue(SaveSlot.Auto, next);
        }

        private void Enqueue(SaveSlot slot, SchoolState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            lock (gate)
            {
                pending[slot] = state;
                if (running) return;
                running = true;
                worker = Task.Run(WriteLoop);
            }
        }

        private void WriteLoop()
        {
            while (true)
            {
                SaveSlot slot;
                SchoolState state;
                lock (gate)
                {
                    if (pending.Count == 0)
                    {
                        running = false;
                        return;
                    }
                    using (Dictionary<SaveSlot, SchoolState>.Enumerator next = pending.GetEnumerator())
                    {
                        next.MoveNext();
                        slot = next.Current.Key;
                        state = next.Current.Value;
                    }
                    pending.Remove(slot);
                }
                try
                {
                    store.Write(slot, state);
                }
                catch (Exception error) // IOException, UnauthorizedAccessException, serializer errors: never kill the worker.
                {
                    lock (gate) failures.Enqueue(error);
                }
            }
        }
    }
}
