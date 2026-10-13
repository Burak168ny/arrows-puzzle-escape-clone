using Arrows.Model;
using Arrows.Model.Generation;
using Arrows.Model.Serialization;
using NUnit.Framework;
using static Arrows.Tests.TestLevels;

namespace Arrows.Tests
{
    public class GeneratorTests
    {
        private static GeneratorSettings Settings(int w, int h, int seed, ShapeKind shape = ShapeKind.Rectangle) =>
            new GeneratorSettings
            {
                Width = w, Height = h, Shape = shape, Seed = seed,
                FillRatio = 0.95f, MinLength = 2, MaxLength = 6, TurnChance = 0.4f,
            };

        [Test]
        public void GeneratedLevels_AreAlwaysValidAndSolvable([Range(1, 25)] int seed)
        {
            var shape = (ShapeKind)(seed % 5);
            var level = LevelGenerator.Generate(Settings(6 + seed % 10, 8 + seed % 7, seed, shape));

            CollectionAssert.IsEmpty(LevelValidator.Validate(level.Data));
            Assert.IsTrue(level.IsSolvable);
            Assert.Greater(level.Data.Arrows.Count, 0);
        }

        [Test]
        public void SameSeed_GivesSameLevel()
        {
            var a = LevelGenerator.Generate(Settings(10, 12, 42));
            var b = LevelGenerator.Generate(Settings(10, 12, 42));
            Assert.AreEqual(LevelSerializer.ToJson(a.Data), LevelSerializer.ToJson(b.Data));
        }

        [Test]
        public void DifferentSeeds_GiveDifferentLevels()
        {
            var a = LevelGenerator.Generate(Settings(10, 12, 1));
            var b = LevelGenerator.Generate(Settings(10, 12, 2));
            Assert.AreNotEqual(LevelSerializer.ToJson(a.Data), LevelSerializer.ToJson(b.Data));
        }

        [Test]
        public void LargeBoard_IsDenselyFilled()
        {
            var level = LevelGenerator.Generate(Settings(30, 30, 7));
            Assert.IsTrue(level.IsSolvable);
            Assert.GreaterOrEqual(level.Coverage, 0.85f);
        }

        [Test]
        public void ArrowLengths_RespectMinimum_AndExtendedMaximum()
        {
            var s = Settings(12, 12, 3);
            var level = LevelGenerator.Generate(s);
            foreach (var arrow in level.Data.Arrows)
            {
                Assert.GreaterOrEqual(arrow.Cells.Count, s.MinLength);
                Assert.LessOrEqual(arrow.Cells.Count, s.MaxLength + 2); // tail extension may add up to 2
            }
        }

        [Test]
        public void ShapeMask_IsRespected()
        {
            var s = Settings(15, 15, 5, ShapeKind.Circle);
            var mask = LevelShape.CreateMask(s.Shape, s.Width, s.Height);
            var level = LevelGenerator.Generate(s);
            foreach (var arrow in level.Data.Arrows)
            foreach (var c in arrow.Cells)
                Assert.IsTrue(mask[c.Y * s.Width + c.X], $"cell {c} is outside the circle");
        }

        [Test]
        public void MoreCandidates_NeverPickAnEasierLevel()
        {
            var one = Settings(10, 12, 9);
            var many = one.Clone();
            many.Candidates = 6;
            Assert.GreaterOrEqual(LevelGenerator.Generate(many).Difficulty, LevelGenerator.Generate(one).Difficulty);
        }

        [Test]
        public void Repair_ReversesFacingArrow_ToMakeLevelSolvable()
        {
            var data = Data(4, 1, A(0, 0, 1, 0), A(3, 0, 2, 0));
            var report = LevelRepair.Repair(data);

            Assert.IsTrue(report.Changed);
            Assert.AreEqual(2, data.Arrows.Count, "reversal alone fixes it, nothing removed");
            Assert.IsTrue(LevelSolver.IsSolvable(LevelModel.FromData(data)));
        }

        [Test]
        public void Repair_AlwaysEndsWithASolvableLevel([Range(1, 10)] int seed)
        {
            // Flip every arrow of a generated level: this usually creates many deadlocks.
            var data = LevelGenerator.Generate(Settings(10, 10, seed)).Data;
            foreach (var arrow in data.Arrows) arrow.Cells.Reverse();

            LevelRepair.Repair(data);

            CollectionAssert.IsEmpty(LevelValidator.Validate(data));
            Assert.IsTrue(LevelSolver.IsSolvable(LevelModel.FromData(data)));
        }

        [Test]
        public void Campaign_HasThirtySolvableLevels_WithTutorialsFirst()
        {
            var levels = Campaign.Generate();
            Assert.AreEqual(30, levels.Count);
            foreach (var level in levels) Assert.IsTrue(level.IsSolvable);

            for (int i = 0; i < Campaign.TutorialCount; i++)
                Assert.LessOrEqual(levels[i].Data.Width * levels[i].Data.Height, 36, "tutorials use tiny grids");
        }
    }
}
