#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SchoolDirectorTycoon.Bridge;
using SchoolDirectorTycoon.Core;
using UnityEngine;

namespace SchoolDirectorTycoon.Tests
{
    public sealed class BridgeRobustnessTests
    {
        private sealed class ListLogger : IBridgeLogger
        {
            public readonly List<Exception> Logged = new List<Exception>();
            public void LogException(Exception error) => Logged.Add(error);
        }

        [Test]
        public void ThrowingSubscriberDoesNotBreakDispatch()
        {
            var logger = new ListLogger();
            var session = GameSession.New(3, logger);
            int laterCalls = 0;
            session.StateChanged += (_, __) => throw new InvalidOperationException("ui bug");
            session.StateChanged += (_, __) => laterCalls++;

            bool applied = session.Dispatch(Simulation.AdvanceDay);

            Assert.That(applied, Is.True);
            Assert.That(laterCalls, Is.EqualTo(1));
            Assert.That(logger.Logged, Has.Count.EqualTo(1));
        }

        [Test]
        public void NonRuleExceptionRaisesFaultedAndKeepsState()
        {
            var logger = new ListLogger();
            var session = GameSession.New(3, logger);
            SchoolState before = session.State;
            Exception? fault = null;
            session.Faulted += error => fault = error;

            bool applied = session.Dispatch(_ => throw new NullReferenceException("core bug"));

            Assert.That(applied, Is.False);
            Assert.That(fault, Is.InstanceOf<NullReferenceException>());
            Assert.That(session.State, Is.SameAs(before));
        }

        private static DayClock NewClock(GameSession session, float secondsPerDay)
        {
            var clock = new GameObject("clock").AddComponent<DayClock>();
            clock.SecondsPerDay = secondsPerDay;
            clock.Session = session;
            return clock;
        }

        [Test]
        public void DayClockCarriesOverflow()
        {
            var session = GameSession.New(11);
            int startDay = session.State.Day;
            DayClock clock = NewClock(session, 10f);

            clock.Tick(12f);
            Assert.That(session.State.Day, Is.EqualTo(startDay + 1));
            Assert.That(clock.DayProgress, Is.EqualTo(0.2f).Within(1e-4f));

            clock.Tick(8f);
            Assert.That(session.State.Day, Is.EqualTo(startDay + 2));
        }

        [Test]
        public void DayClockAdvancesAtMostOneDayPerTick()
        {
            var session = GameSession.New(11);
            int startDay = session.State.Day;
            DayClock clock = NewClock(session, 10f);

            clock.Tick(1000f);

            Assert.That(session.State.Day, Is.EqualTo(startDay + 1));
            Assert.That(clock.DayProgress, Is.LessThan(1f));
            Assert.That(clock.DayProgress, Is.GreaterThanOrEqualTo(0f));
        }

        [Test]
        public void SaveServiceWritesInBackgroundAndFlushWaits()
        {
            string dir = Path.Combine(Path.GetTempPath(), "sdt-save-" + Guid.NewGuid().ToString("N"));
            try
            {
                var session = GameSession.New(5);
                using (var saves = new SaveService(session, dir))
                {
                    session.Dispatch(Simulation.AdvanceDay);
                    session.Dispatch(Simulation.AdvanceDay);
                    saves.Save(SaveSlot.Manual);

                    Assert.That(saves.Flush(TimeSpan.FromSeconds(10)), Is.True);
                    Assert.That(saves.HasPendingSaves, Is.False);
                    Assert.That(saves.Load(SaveSlot.Auto).Day, Is.EqualTo(session.State.Day));
                    Assert.That(saves.Load(SaveSlot.Manual).Day, Is.EqualTo(session.State.Day));
                }
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            }
        }

        [Test]
        public void SaveFailureIsReportedOnDrain()
        {
            // A file where the save directory should be makes every write fail.
            string blocker = Path.Combine(Path.GetTempPath(), "sdt-block-" + Guid.NewGuid().ToString("N"));
            File.WriteAllText(blocker, "x");
            try
            {
                var session = GameSession.New(5);
                using (var saves = new SaveService(session, blocker))
                {
                    var failures = new List<Exception>();
                    saves.SaveFailed += failures.Add;

                    saves.Save(SaveSlot.Manual);
                    saves.Flush(TimeSpan.FromSeconds(10));
                    Assert.That(failures, Is.Empty, "callbacks must wait for the main-thread drain");

                    saves.DrainCallbacks();
                    Assert.That(failures, Has.Count.EqualTo(1));
                }
            }
            finally
            {
                File.Delete(blocker);
            }
        }
    }
}
