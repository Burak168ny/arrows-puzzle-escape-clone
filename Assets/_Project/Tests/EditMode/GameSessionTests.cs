using Arrows.Model;
using NUnit.Framework;
using static Arrows.Tests.TestLevels;

namespace Arrows.Tests
{
    public class GameSessionTests
    {
        // 0 and 1 face each other (always blocked), 2 is free.
        private static GameSession StuckSession() =>
            new GameSession(Level(5, 3, A(0, 0, 1, 0), A(3, 0, 2, 0), A(0, 2, 1, 2)));

        [Test]
        public void Blocked_LosesOneLife()
        {
            var session = StuckSession();
            int reported = -1;
            session.LivesChanged += l => reported = l;

            Assert.IsTrue(session.TryLaunch(0, out var result));
            Assert.IsTrue(result.IsBlocked);
            Assert.AreEqual(2, session.Lives);
            Assert.AreEqual(2, reported);
        }

        [Test]
        public void ThreeCollisions_LoseTheLevel()
        {
            var session = StuckSession();
            SessionResult? finished = null;
            session.Finished += r => finished = r;

            session.TryLaunch(0, out _);
            session.TryLaunch(1, out _);
            Assert.IsNull(finished);
            session.TryLaunch(0, out _);

            Assert.AreEqual(SessionResult.Lost, finished);
            Assert.AreEqual(0, session.Lives);
            Assert.IsFalse(session.TryLaunch(2, out _), "No moves after the session ended.");
        }

        [Test]
        public void ClearingAllArrows_WinsTheLevel()
        {
            var session = new GameSession(Level(4, 4, A(0, 0, 1, 0), A(0, 2, 1, 2)));
            SessionResult? finished = null;
            session.Finished += r => finished = r;

            session.TryLaunch(0, out _);
            Assert.IsNull(finished);
            session.TryLaunch(1, out _);

            Assert.AreEqual(SessionResult.Won, finished);
            Assert.AreEqual(3, session.Lives);
        }

        [Test]
        public void TappingRemovedArrow_IsIgnored()
        {
            var session = StuckSession();
            session.TryLaunch(2, out _);
            Assert.IsFalse(session.TryLaunch(2, out _));
            Assert.AreEqual(1, session.Moves);
        }
    }
}
