using System;
using Arrows.Model;
using NUnit.Framework;
using static Arrows.Tests.TestLevels;

namespace Arrows.Tests
{
    public class LevelModelTests
    {
        [Test]
        public void Direction_IsDifferenceOfLastTwoCells()
        {
            var level = Level(5, 5, A(0, 0, 1, 0, 1, 1));
            Assert.AreEqual(GridPos.Up, level.Arrows[0].Direction);
            Assert.AreEqual(P(1, 1), level.Arrows[0].Head);
        }

        [Test]
        public void Evaluate_ClearPath_Exits_WithFreeCellCount()
        {
            var level = Level(5, 1, A(0, 0, 1, 0));
            var result = level.Evaluate(0);
            Assert.IsTrue(result.IsExit);
            Assert.AreEqual(3, result.FreeCells);
        }

        [Test]
        public void Evaluate_HeadOnEdge_ExitsWithZeroFreeCells()
        {
            var level = Level(3, 1, A(1, 0, 2, 0));
            Assert.AreEqual(0, level.Evaluate(0).FreeCells);
        }

        [Test]
        public void Evaluate_OtherArrowInPath_IsBlocked()
        {
            // Arrow 0 points right; arrow 1 sits two cells ahead.
            var level = Level(6, 3, A(0, 1, 1, 1), A(3, 0, 3, 1, 3, 2));
            var result = level.Evaluate(0);

            Assert.IsTrue(result.IsBlocked);
            Assert.AreEqual(1, result.BlockerId);
            Assert.AreEqual(P(3, 1), result.BlockedCell);
            Assert.AreEqual(1, result.FreeCells);
        }

        [Test]
        public void Evaluate_DoesNotLookBehindTheHead()
        {
            // Arrow 1 sits right behind arrow 0, both pointing right.
            var level = Level(6, 1, A(2, 0, 3, 0), A(0, 0, 1, 0));
            Assert.IsTrue(level.Evaluate(0).IsExit);
            Assert.IsTrue(level.Evaluate(1).IsBlocked);
        }

        [Test]
        public void Evaluate_OwnTailAhead_IsAlreadyVacated()
        {
            // U-turn whose tail lies in front of the head: tail moves away before the head arrives.
            // Head (2,1) points up; (2,3) is the tail (index 0), reached at step 2 > 0.
            var level = Level(4, 5, A(2, 3, 1, 3, 0, 3, 0, 2, 0, 1, 0, 0, 1, 0, 2, 0, 2, 1));
            Assert.IsTrue(level.Evaluate(0).IsExit);
        }

        [Test]
        public void Evaluate_OwnBodyAhead_TooEarly_IsSelfBlocked()
        {
            // Spiral: head (1,1) points down at (1,0), which is body index 1 and is reached at step 1.
            // The body cell is still occupied at that moment, so the arrow hits itself.
            var level = Level(4, 4, A(0, 0, 1, 0, 2, 0, 2, 1, 2, 2, 1, 2, 1, 1));
            var result = level.Evaluate(0);
            Assert.IsTrue(result.IsBlocked);
            Assert.AreEqual(0, result.BlockerId);
            Assert.AreEqual(P(1, 0), result.BlockedCell);
        }

        [Test]
        public void Launch_Exit_RemovesArrowAndFreesCells()
        {
            var level = Level(4, 4, A(0, 0, 1, 0), A(2, 2, 2, 3));
            ArrowModel removed = null;
            level.ArrowRemoved += a => removed = a;

            var result = level.Launch(0);

            Assert.IsTrue(result.IsExit);
            Assert.IsTrue(level.Arrows[0].IsRemoved);
            Assert.AreSame(level.Arrows[0], removed);
            Assert.AreEqual(1, level.RemainingCount);
            Assert.IsTrue(level.Grid.IsEmpty(P(0, 0)));
            Assert.IsTrue(level.Grid.IsEmpty(P(1, 0)));
        }

        [Test]
        public void Launch_Blocked_LeavesBoardUnchanged()
        {
            var level = Level(4, 1, A(0, 0, 1, 0), A(3, 0, 2, 0));
            var result = level.Launch(0);

            Assert.IsTrue(result.IsBlocked);
            Assert.IsFalse(level.Arrows[0].IsRemoved);
            Assert.AreEqual(2, level.RemainingCount);
            Assert.AreEqual(0, level.Grid.Get(P(0, 0)));
        }

        [Test]
        public void Removing_A_Blocker_Unblocks_The_Arrow()
        {
            var level = Level(5, 3, A(0, 1, 1, 1), A(3, 0, 3, 1, 3, 2));
            Assert.IsTrue(level.Evaluate(0).IsBlocked);
            level.Launch(1);
            Assert.IsTrue(level.Evaluate(0).IsExit);
        }

        [Test]
        public void Launch_RemovedArrow_Throws()
        {
            var level = Level(3, 1, A(0, 0, 1, 0));
            level.Launch(0);
            Assert.Throws<InvalidOperationException>(() => level.Launch(0));
        }

        [Test]
        public void Clone_IsIndependent()
        {
            var level = Level(3, 3, A(0, 0, 1, 0));
            var clone = level.Clone();
            clone.Launch(0);
            Assert.IsFalse(level.Arrows[0].IsRemoved);
            Assert.AreEqual(0, level.Grid.Get(P(0, 0)));
        }

        [Test]
        public void FindArrowNear_FindsArrowWithinTolerance()
        {
            var level = Level(5, 5, A(1, 1, 2, 1, 3, 1));
            Assert.AreEqual(0, level.FindArrowNear(2.1f, 1.3f, 0.5f));
            // Between two cells of the same arrow, on the line.
            Assert.AreEqual(0, level.FindArrowNear(1.5f, 1.0f, 0.2f));
            // Next to the arrow in an empty cell, but still within tolerance.
            Assert.AreEqual(0, level.FindArrowNear(2f, 1.6f, 0.65f));
            Assert.AreEqual(-1, level.FindArrowNear(2f, 3f, 0.65f));
        }

        [Test]
        public void FindArrowNear_PicksClosestArrow()
        {
            var level = Level(5, 5, A(0, 0, 1, 0), A(0, 1, 1, 1));
            Assert.AreEqual(0, level.FindArrowNear(0.5f, 0.3f, 0.6f));
            Assert.AreEqual(1, level.FindArrowNear(0.5f, 0.7f, 0.6f));
        }

        [Test]
        public void FindArrowNear_IgnoresRemovedArrows()
        {
            var level = Level(5, 5, A(0, 0, 1, 0));
            level.Launch(0);
            Assert.AreEqual(-1, level.FindArrowNear(0f, 0f, 0.6f));
        }
    }
}
