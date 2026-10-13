using System.Collections.Generic;
using Arrows.Model;
using NUnit.Framework;
using static Arrows.Tests.TestLevels;

namespace Arrows.Tests
{
    public class LevelSolverTests
    {
        [Test]
        public void ChainOfBlockers_IsSolvable_InValidOrder()
        {
            // 0 is blocked by 1, 1 is blocked by 2, 2 is free.
            var level = Level(6, 6,
                A(0, 0, 1, 0, 2, 0),
                A(5, 0, 5, 1),
                A(3, 5, 4, 5, 5, 5));

            var result = LevelSolver.Solve(level);

            Assert.IsTrue(result.IsSolvable);
            CollectionAssert.AreEqual(new[] { 2, 1, 0 }, result.Order);
            Assert.AreEqual(1, result.MinOpenArrows);
            AssertOrderIsPlayable(level, result.Order);
        }

        [Test]
        public void FacingArrows_AreUnsolvable_AndReportedAsStuck()
        {
            var level = Level(4, 1, A(0, 0, 1, 0), A(3, 0, 2, 0));
            var result = LevelSolver.Solve(level);

            Assert.IsFalse(result.IsSolvable);
            CollectionAssert.AreEquivalent(new[] { 0, 1 }, result.StuckArrows);
        }

        [Test]
        public void PartiallyStuck_RemovesWhatItCan()
        {
            var level = Level(5, 3,
                A(0, 0, 1, 0), A(3, 0, 2, 0), // facing pair, stuck
                A(0, 2, 1, 2));                // free
            var result = LevelSolver.Solve(level);

            Assert.IsFalse(result.IsSolvable);
            CollectionAssert.AreEqual(new[] { 2 }, result.Order);
            CollectionAssert.AreEquivalent(new[] { 0, 1 }, result.StuckArrows);
        }

        [Test]
        public void SelfBlockedArrow_IsUnsolvable()
        {
            var level = Level(4, 4, A(0, 0, 1, 0, 2, 0, 2, 1, 2, 2, 1, 2, 1, 1));
            Assert.IsFalse(LevelSolver.IsSolvable(level));
        }

        [Test]
        public void Solve_DoesNotMutateTheLevel()
        {
            var level = Level(3, 3, A(0, 0, 1, 0), A(0, 2, 1, 2));
            LevelSolver.Solve(level);
            Assert.AreEqual(2, level.RemainingCount);
        }

        [Test]
        public void Difficulty_MoreOpenArrows_MeansHigherAverage()
        {
            var easy = Level(4, 4, A(0, 0, 1, 0), A(0, 1, 1, 1), A(0, 2, 1, 2), A(0, 3, 1, 3));
            var hard = Level(6, 6, A(0, 0, 1, 0, 2, 0), A(5, 0, 5, 1), A(3, 5, 4, 5, 5, 5));

            Assert.Greater(LevelSolver.Solve(easy).AverageOpenArrows, LevelSolver.Solve(hard).AverageOpenArrows);
        }

        [Test]
        public void Hint_ReturnsAnExitableArrow()
        {
            var level = Level(6, 6, A(0, 0, 1, 0, 2, 0), A(5, 0, 5, 1), A(3, 5, 4, 5, 5, 5));
            int hint = LevelSolver.GetHint(level);
            Assert.AreEqual(2, hint);
            Assert.IsTrue(level.CanExit(hint));
        }

        [Test]
        public void Hint_IsMinusOne_WhenNothingCanMove()
        {
            var level = Level(4, 1, A(0, 0, 1, 0), A(3, 0, 2, 0));
            Assert.AreEqual(-1, LevelSolver.GetHint(level));
        }

        private static void AssertOrderIsPlayable(LevelModel level, IEnumerable<int> order)
        {
            var sim = level.Clone();
            foreach (int id in order) Assert.IsTrue(sim.Launch(id).IsExit, $"Arrow {id} should exit.");
            Assert.IsTrue(sim.IsCleared);
        }
    }
}
