using Arrows.Model;
using Arrows.View;
using NUnit.Framework;
using UnityEngine;
using static Arrows.Tests.TestLevels;

namespace Arrows.Tests
{
    public class ArrowPathTests
    {
        // L-shaped arrow: (0,0) -> (1,0) -> (1,1), pointing up.
        private static readonly GridPos[] Cells = { P(0, 0), P(1, 0), P(1, 1) };
        private static readonly BoardLayout Identity = new BoardLayout(1, 1, 1f); // origin at (0,0)

        [Test]
        public void PointAt_FollowsBodyThenExtendsPastHead()
        {
            Assert.AreEqual(new Vector2(0.5f, 0f), ArrowPath.PointAt(Cells, GridPos.Up, 0.5f));
            Assert.AreEqual(new Vector2(1f, 0.5f), ArrowPath.PointAt(Cells, GridPos.Up, 1.5f));
            Assert.AreEqual(new Vector2(1f, 3f), ArrowPath.PointAt(Cells, GridPos.Up, 4f));
            Assert.AreEqual(new Vector2(-0.5f, 0f), ArrowPath.PointAt(Cells, GridPos.Up, -0.5f));
        }

        [Test]
        public void Sample_AtRest_EmitsTailCornerHead()
        {
            var buffer = new Vector3[Cells.Length + 1];
            int count = ArrowPath.Sample(Cells, GridPos.Up, 0f, Identity, buffer);

            Assert.AreEqual(3, count);
            Assert.AreEqual(new Vector3(0, 0), buffer[0]);
            Assert.AreEqual(new Vector3(1, 0), buffer[1]);
            Assert.AreEqual(new Vector3(1, 1), buffer[2]);
        }

        [Test]
        public void Sample_PastTheCorner_IsAStraightLine()
        {
            var buffer = new Vector3[Cells.Length + 1];
            int count = ArrowPath.Sample(Cells, GridPos.Up, 1.5f, Identity, buffer);

            Assert.AreEqual(2, count);
            Assert.AreEqual(new Vector3(1f, 0.5f), buffer[0]);
            Assert.AreEqual(new Vector3(1f, 2.5f), buffer[1]);
        }

        [Test]
        public void Sample_KeepsArrowLength()
        {
            var buffer = new Vector3[Cells.Length + 1];
            foreach (float offset in new[] { 0f, 0.3f, 1f, 1.7f, 5f })
            {
                int count = ArrowPath.Sample(Cells, GridPos.Up, offset, Identity, buffer);
                float length = 0f;
                for (int i = 1; i < count; i++) length += Vector3.Distance(buffer[i - 1], buffer[i]);
                Assert.AreEqual(Cells.Length - 1, length, 1e-4f, $"offset {offset}");
            }
        }
    }
}
