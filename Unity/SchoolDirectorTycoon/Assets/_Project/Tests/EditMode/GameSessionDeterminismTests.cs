#nullable enable
using System;
using System.Linq;
using NUnit.Framework;
using SchoolDirectorTycoon.Bridge;
using SchoolDirectorTycoon.Core;

namespace SchoolDirectorTycoon.Tests
{
    public sealed class GameSessionDeterminismTests
    {
        private static GameSession Play(uint seed)
        {
            var session = GameSession.New(seed);
            session.Dispatch(s => Simulation.Build(s, Facility.Classroom));
            session.Dispatch(s => Simulation.SetTuition(s, s.Tuition));
            for (int day = 0; day < 5; day++)
            {
                if (session.State.PendingIncident != null)
                    session.Dispatch(s => Simulation.ResolveIncident(s, intervene: true));
                session.Dispatch(Simulation.AdvanceDay);
            }
            return session;
        }

        [Test]
        public void SameSeedAndCommandsGiveEqualState()
        {
            SchoolState first = Play(42).State, second = Play(42).State;

            Assert.That(first.Day, Is.GreaterThan(1));
            Assert.That(second with { Reports = first.Reports, Buildings = first.Buildings }, Is.EqualTo(first));
            Assert.That(second.Reports.SequenceEqual(first.Reports), Is.True);
            Assert.That(second.Buildings.SequenceEqual(first.Buildings), Is.True);
        }

        [Test]
        public void RejectedCommandRaisesCodeAndKeepsState()
        {
            var session = GameSession.New(7);
            SchoolState before = session.State;
            RuleCode? rejected = null;
            session.CommandRejected += code => rejected = code;

            bool applied = session.Dispatch(s => Simulation.ResolveIncident(s with { PendingIncident = null }, true));

            Assert.That(applied, Is.False);
            Assert.That(rejected, Is.EqualTo(RuleCode.NoPendingIncident));
            Assert.That(session.State, Is.SameAs(before));
        }
    }
}
