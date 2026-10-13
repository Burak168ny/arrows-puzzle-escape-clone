using System;
using System.Collections.Generic;

namespace Arrows.Model.Generation
{
    public sealed class GeneratedLevel
    {
        public LevelData Data;
        public SolveResult Solution;
        public GeneratorSettings Settings;

        /// <summary>Share of usable (masked) cells covered by arrows.</summary>
        public float Coverage;

        public bool IsSolvable => Solution != null && Solution.IsSolvable;
        public float Difficulty => Solution?.DifficultyScore ?? 0f;
    }

    /// <summary>
    /// Builds levels that are solvable by construction.
    ///
    /// Arrows are placed one at a time. A new arrow is only accepted if its exit ray is clear
    /// with respect to the arrows already on the board. Reversing the placement order is then
    /// always a valid solution: when arrow k is removed, only arrows placed before it remain,
    /// and its ray was clear of all of them. Later arrows may freely sit in earlier arrows' rays,
    /// which is what creates the dependencies the player has to untangle.
    ///
    /// Each arrow is grown backwards from its head: pick an empty cell with a clear ray in some
    /// direction, then random-walk the body away from it (never onto its own ray).
    /// Heads are tried roughly from the center outwards: inner cells only have clear rays while
    /// the board is still empty, outer cells have short rays that stay clear for longer.
    ///
    /// Finally, leftover holes are absorbed by extending an adjacent arrow's tail, which is only
    /// allowed if the hole is not on the ray of any arrow placed later (those leave earlier and
    /// must not find it occupied).
    /// </summary>
    public sealed class LevelGenerator
    {
        private readonly GeneratorSettings _s;
        private readonly Random _rng;
        private readonly int[] _owner;
        private readonly bool[] _mask;
        private readonly List<List<GridPos>> _arrows = new List<List<GridPos>>();
        private readonly List<GridPos> _body = new List<GridPos>();
        private readonly GridPos[] _dirs = (GridPos[])GridPos.Directions.Clone();
        private int _covered;
        private int _usable;

        private LevelGenerator(GeneratorSettings settings, int seed)
        {
            _s = settings;
            _rng = new Random(seed);
            _owner = new int[settings.Width * settings.Height];
            Array.Fill(_owner, GridModel.Empty);
            _mask = LevelShape.CreateMask(settings.Shape, settings.Width, settings.Height);
            foreach (bool m in _mask) if (m) _usable++;
        }

        /// <summary>
        /// Generates <see cref="GeneratorSettings.Candidates"/> levels from consecutive seeds and returns the one
        /// closest to the target difficulty (or the hardest if no target is set).
        /// </summary>
        public static GeneratedLevel Generate(GeneratorSettings settings)
        {
            var s = settings.Clone();
            s.Sanitize();

            GeneratedLevel best = null;
            for (int i = 0; i < s.Candidates; i++)
            {
                var candidate = new LevelGenerator(s, unchecked(s.Seed * 7919 + i)).Run();
                if (!candidate.IsSolvable || candidate.Data.Arrows.Count == 0) continue;
                if (best == null || IsBetter(candidate, best, s.TargetDifficulty)) best = candidate;
            }

            return best ?? new GeneratedLevel
            {
                Data = new LevelData { Width = s.Width, Height = s.Height },
                Solution = new SolveResult(),
                Settings = s,
            };
        }

        private static bool IsBetter(GeneratedLevel a, GeneratedLevel b, float target)
        {
            if (target <= 0f) return a.Difficulty > b.Difficulty;
            return Math.Abs(a.Difficulty - target) < Math.Abs(b.Difficulty - target);
        }

        private GeneratedLevel Run()
        {
            var cells = new List<GridPos>(_usable);
            for (int y = 0; y < _s.Height; y++)
            for (int x = 0; x < _s.Width; x++)
                if (_mask[y * _s.Width + x]) cells.Add(new GridPos(x, y));

            // Rays only ever get blocked as arrows are added, but a cell that failed once can still
            // succeed with a different random direction order or length, so a few passes raise coverage.
            for (int pass = 0; pass < 3 && Coverage < _s.FillRatio; pass++)
            {
                SortCenterFirst(cells);
                bool placed = false;
                foreach (var head in cells)
                {
                    if (Coverage >= _s.FillRatio) break;
                    if (_owner[Index(head)] != GridModel.Empty) continue;
                    placed |= TryPlaceArrowAt(head);
                }
                if (!placed) break;
            }

            FillHolesByExtendingTails(cells);

            var data = new LevelData { Width = _s.Width, Height = _s.Height };
            foreach (var arrow in _arrows) data.Arrows.Add(new ArrowData(arrow));

            return new GeneratedLevel
            {
                Data = data,
                Solution = LevelSolver.Solve(LevelModel.FromData(data)),
                Settings = _s,
                Coverage = Coverage,
            };
        }

        private void SortCenterFirst(List<GridPos> cells)
        {
            var keys = new Dictionary<GridPos, double>(cells.Count);
            foreach (var c in cells)
            {
                int edge = Math.Min(Math.Min(c.X, c.Y), Math.Min(_s.Width - 1 - c.X, _s.Height - 1 - c.Y));
                keys[c] = edge + _rng.NextDouble() * 2.5;
            }
            cells.Sort((a, b) => keys[b].CompareTo(keys[a]));
        }

        private void FillHolesByExtendingTails(List<GridPos> cells)
        {
            int maxLength = _s.MaxLength + 2;
            bool changed = true;
            while (changed && Coverage < _s.FillRatio)
            {
                changed = false;
                Shuffle(cells);
                foreach (var hole in cells)
                {
                    if (_owner[Index(hole)] != GridModel.Empty) continue;
                    Shuffle(_dirs);
                    foreach (var d in _dirs)
                    {
                        var tail = hole + d;
                        if (!InBounds(tail)) continue;
                        int id = _owner[Index(tail)];
                        if (id == GridModel.Empty) continue;

                        var arrow = _arrows[id];
                        if (arrow[0] != tail || arrow.Count >= maxLength) continue;
                        if (!CanExtend(id, hole)) continue;

                        arrow.Insert(0, hole);
                        _owner[Index(hole)] = id;
                        _covered++;
                        changed = true;
                        break;
                    }
                }
            }
        }

        // The hole must stay off this arrow's own ray and off the rays of all arrows placed after it.
        private bool CanExtend(int id, GridPos hole)
        {
            for (int other = id; other < _arrows.Count; other++)
            {
                var a = _arrows[other];
                var head = a[a.Count - 1];
                if (IsOnRay(hole, head, head - a[a.Count - 2])) return false;
            }
            return true;
        }

        private float Coverage => _usable > 0 ? (float)_covered / _usable : 1f;

        private bool TryPlaceArrowAt(GridPos head)
        {
            Shuffle(_dirs);
            int target = _rng.Next(_s.MinLength, _s.MaxLength + 1);

            foreach (var dir in _dirs)
            {
                if (!RayIsClear(head, dir)) continue;
                GrowBody(head, dir, target);
                if (_body.Count < _s.MinLength) continue;

                Commit();
                return true;
            }
            return false;
        }

        /// <summary>Fills <see cref="_body"/> head-first by walking away from the head.</summary>
        private void GrowBody(GridPos head, GridPos dir, int target)
        {
            _body.Clear();
            _body.Add(head);

            var current = head;
            var heading = -dir; // grow backwards
            while (_body.Count < target)
            {
                if (!TryStep(current, ref heading, head, dir, out var next)) break;
                _body.Add(next);
                current = next;
            }
        }

        private bool TryStep(GridPos from, ref GridPos heading, GridPos head, GridPos dir, out GridPos next)
        {
            // The first body cell must be directly behind the head, so the head direction is preserved.
            if (_body.Count == 1)
            {
                next = from + heading;
                return IsFree(next, head, dir);
            }

            var left = new GridPos(-heading.Y, heading.X);
            var right = -left;
            bool turnFirst = _rng.NextDouble() < _s.TurnChance;
            bool leftFirst = _rng.Next(2) == 0;
            var a = leftFirst ? left : right;
            var b = leftFirst ? right : left;

            var options = turnFirst ? (a, b, heading) : (heading, a, b);
            if (TryDir(from, options.Item1, head, dir, ref heading, out next)) return true;
            if (TryDir(from, options.Item2, head, dir, ref heading, out next)) return true;
            return TryDir(from, options.Item3, head, dir, ref heading, out next);
        }

        private bool TryDir(GridPos from, GridPos d, GridPos head, GridPos dir, ref GridPos heading, out GridPos next)
        {
            next = from + d;
            if (!IsFree(next, head, dir)) return false;
            heading = d;
            return true;
        }

        private bool IsFree(GridPos p, GridPos head, GridPos dir)
        {
            if (!InBounds(p) || !_mask[Index(p)] || _owner[Index(p)] != GridModel.Empty) return false;
            if (IsOnRay(p, head, dir)) return false;
            return !_body.Contains(p);
        }

        private static bool IsOnRay(GridPos p, GridPos head, GridPos dir)
        {
            var d = p - head;
            if (dir.X != 0) return d.Y == 0 && d.X * dir.X > 0;
            return d.X == 0 && d.Y * dir.Y > 0;
        }

        private bool RayIsClear(GridPos head, GridPos dir)
        {
            for (var p = head + dir; InBounds(p); p += dir)
                if (_owner[Index(p)] != GridModel.Empty) return false;
            return true;
        }

        private void Commit()
        {
            int id = _arrows.Count;
            var arrow = new List<GridPos>(_body.Count);
            for (int i = _body.Count - 1; i >= 0; i--) // tail → head
            {
                arrow.Add(_body[i]);
                _owner[Index(_body[i])] = id;
            }
            _arrows.Add(arrow);
            _covered += arrow.Count;
        }

        private bool InBounds(GridPos p) => p.X >= 0 && p.Y >= 0 && p.X < _s.Width && p.Y < _s.Height;
        private int Index(GridPos p) => p.Y * _s.Width + p.X;

        private void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
