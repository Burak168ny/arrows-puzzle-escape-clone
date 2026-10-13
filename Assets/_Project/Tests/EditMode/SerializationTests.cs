using System;
using System.IO;
using Arrows.Model;
using Arrows.Model.Serialization;
using NUnit.Framework;
using UnityEngine;
using static Arrows.Tests.TestLevels;

namespace Arrows.Tests
{
    public class SerializationTests
    {
        [Test]
        public void FromJson_ReadsSizeCellsAndColor()
        {
            const string json = @"{ ""width"": 4, ""height"": 3,
                ""arrows"": [ { ""cells"": [[0,0],[1,0],[1,1]], ""color"": ""#FF8800"" },
                              { ""cells"": [[3,2],[3,1]] } ] }";

            var data = LevelSerializer.FromJson(json);

            Assert.AreEqual(4, data.Width);
            Assert.AreEqual(3, data.Height);
            Assert.AreEqual(2, data.Arrows.Count);
            CollectionAssert.AreEqual(new[] { P(0, 0), P(1, 0), P(1, 1) }, data.Arrows[0].Cells);
            Assert.AreEqual("#FF8800", data.Arrows[0].Color);
            Assert.IsNull(data.Arrows[1].Color);
        }

        [Test]
        public void ToJson_RoundTrips()
        {
            var data = Data(5, 5, A(0, 0, 1, 0, 1, 1), A(4, 4, 4, 3));
            data.Arrows[1].Color = "#123456";

            var back = LevelSerializer.FromJson(LevelSerializer.ToJson(data));

            Assert.AreEqual(data.Width, back.Width);
            Assert.AreEqual(data.Height, back.Height);
            Assert.AreEqual(data.Arrows.Count, back.Arrows.Count);
            for (int i = 0; i < data.Arrows.Count; i++)
            {
                CollectionAssert.AreEqual(data.Arrows[i].Cells, back.Arrows[i].Cells);
                Assert.AreEqual(data.Arrows[i].Color, back.Arrows[i].Color);
            }
        }

        [TestCase("")]
        [TestCase("[]")]
        [TestCase(@"{ ""width"": 3 }")]
        [TestCase(@"{ ""width"": 3, ""height"": 3, ""arrows"": [ { ""cells"": [[0]] } ] }")]
        [TestCase(@"{ ""width"": 3.5, ""height"": 3, ""arrows"": [] }")]
        [TestCase(@"{ ""width"": 3, ""height"": 3, ""arrows"": [ ")]
        public void FromJson_RejectsMalformedInput(string json)
        {
            Assert.Throws<FormatException>(() => LevelSerializer.FromJson(json));
        }

        [Test]
        public void Validator_ReportsOverlapGapsBoundsAndSelfCrossing()
        {
            Assert.IsNotEmpty(LevelValidator.Validate(Data(3, 3, A(0, 0, 1, 0), A(1, 0, 1, 1))), "overlap");
            Assert.IsNotEmpty(LevelValidator.Validate(Data(3, 3, A(0, 0, 2, 0))), "gap");
            Assert.IsNotEmpty(LevelValidator.Validate(Data(3, 3, A(2, 0, 3, 0))), "bounds");
            Assert.IsNotEmpty(LevelValidator.Validate(Data(3, 3, A(0, 0, 1, 0, 1, 1, 0, 1, 0, 0))), "self");
            Assert.IsNotEmpty(LevelValidator.Validate(Data(3, 3, A(0, 0))), "too short");
            Assert.IsEmpty(LevelValidator.Validate(Data(3, 3, A(0, 0, 1, 0, 1, 1))));
        }

        [Test]
        public void FromData_InvalidLevel_Throws()
        {
            Assert.Throws<ArgumentException>(() => LevelModel.FromData(Data(3, 3, A(0, 0, 2, 0))));
        }

        /// <summary>Every shipped level must parse, validate and be solvable.</summary>
        [Test]
        public void AllShippedLevels_AreValidAndSolvable()
        {
            string folder = Path.Combine(Application.dataPath, "_Project", "Levels");
            string[] files = Directory.GetFiles(folder, "*.json");
            Assert.IsNotEmpty(files, "No level files found.");

            foreach (string file in files)
            {
                var data = LevelSerializer.FromJson(File.ReadAllText(file));
                CollectionAssert.IsEmpty(LevelValidator.Validate(data), Path.GetFileName(file));
                Assert.IsTrue(LevelSolver.IsSolvable(LevelModel.FromData(data)), $"{Path.GetFileName(file)} is not solvable.");
            }
        }
    }
}
