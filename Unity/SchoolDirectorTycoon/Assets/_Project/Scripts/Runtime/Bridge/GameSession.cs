#nullable enable
using System;
using SchoolDirectorTycoon.Core;

namespace SchoolDirectorTycoon.Bridge
{
    /// <summary>Receives exceptions the bridge isolates instead of throwing them out of Update.</summary>
    public interface IBridgeLogger
    {
        void LogException(Exception error);
    }

    /// <summary>Default logger: <c>Debug.LogException</c> inside Unity.</summary>
    public sealed class UnityBridgeLogger : IBridgeLogger
    {
        public static readonly UnityBridgeLogger Instance = new UnityBridgeLogger();
        public void LogException(Exception error) => UnityEngine.Debug.LogException(error);
    }

    /// <summary>Owns the current immutable state. Every decision goes through <see cref="Dispatch"/>.</summary>
    public sealed class GameSession
    {
        private readonly IBridgeLogger logger;

        public GameSession(SchoolState initial, IBridgeLogger? logger = null)
        {
            State = initial ?? throw new ArgumentNullException(nameof(initial));
            this.logger = logger ?? UnityBridgeLogger.Instance;
        }

        public static GameSession New(uint seed, IBridgeLogger? logger = null) =>
            new GameSession(Simulation.NewSchool(seed), logger);

        public SchoolState State { get; private set; }

        /// <summary>Raised with (old, new) after a command succeeds. Each subscriber is isolated.</summary>
        public event Action<SchoolState, SchoolState>? StateChanged;

        /// <summary>Raised when Core rejects a command; the state is unchanged.</summary>
        public event Action<RuleCode>? CommandRejected;

        /// <summary>Raised when Core throws anything other than a rule rejection (a bug); the state is unchanged.</summary>
        public event Action<Exception>? Faulted;

        /// <returns>True when the command was applied.</returns>
        public bool Dispatch(Func<SchoolState, SchoolState> command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            SchoolState old = State;
            SchoolState next;
            try
            {
                next = command(old) ?? throw new InvalidOperationException("Command returned no state.");
            }
            catch (RuleException rejection)
            {
                RuleCode code = rejection.Code;
                Raise(CommandRejected, handler => ((Action<RuleCode>)handler)(code));
                return false;
            }
            catch (Exception fault)
            {
                logger.LogException(fault);
                Raise(Faulted, handler => ((Action<Exception>)handler)(fault));
                return false;
            }
            State = next;
            Raise(StateChanged, handler => ((Action<SchoolState, SchoolState>)handler)(old, next));
            return true;
        }

        private void Raise(Delegate? subscribers, Action<Delegate> invoke)
        {
            if (subscribers == null) return;
            foreach (Delegate handler in subscribers.GetInvocationList())
            {
                try
                {
                    invoke(handler);
                }
                catch (Exception error)
                {
                    logger.LogException(error);
                }
            }
        }
    }
}
