using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Arrows.Core;
using Arrows.Model;
using Arrows.Model.Generation;
using Arrows.Model.Serialization;
using Arrows.View;
using UnityEditor;
using UnityEngine;

namespace Arrows.Editor
{
    /// <summary>
    /// Tools/Arrows/Level Generator: tune generator settings, preview the result with its
    /// solver stats, save it as JSON, load/repair existing levels, or rebuild the whole campaign.
    /// Edit mode allows drawing arrows by hand: drag from tail to head, right-click deletes,
    /// shift-click reverses.
    /// </summary>
    public sealed class LevelGeneratorWindow : EditorWindow
    {
        private const string LevelsFolder = "Assets/_Project/Levels";

        [SerializeField] private GeneratorSettings _settings = new GeneratorSettings { Candidates = 8 };
        [SerializeField] private int _campaignSeed = 1000;
        [SerializeField] private bool _showOrder;
        [SerializeField] private bool _highlightOpen = true;
        [SerializeField] private bool _editMode;

        private LevelData _level;
        private SolveResult _solution;
        private float _coverage;
        private string _status = "";
        private Vector2 _scroll;
        private Color[] _palette;
        private readonly List<GridPos> _drawing = new List<GridPos>();
        private float _cell;
        private Vector2 _origin;

        [MenuItem("Tools/Arrows/Level Generator", priority = 10)]
        public static void Open() => GetWindow<LevelGeneratorWindow>("Level Generator").minSize = new Vector2(760, 520);

        private void OnEnable()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Project/GameConfig.asset");
            _palette = config != null ? config.Theme.Palette : new BoardTheme().Palette;
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll, GUILayout.Width(320)))
                {
                    _scroll = scroll.scrollPosition;
                    DrawSettings();
                    EditorGUILayout.Space(8);
                    DrawActions();
                    EditorGUILayout.Space(8);
                    DrawStats();
                    EditorGUILayout.Space(8);
                    DrawCampaign();
                }

                var previewRect = GUILayoutUtility.GetRect(10, 10, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                ComputeLayout(previewRect);
                if (_editMode && _level != null) HandleEditInput(previewRect);
                if (Event.current.type == EventType.Repaint) DrawPreview(previewRect);
            }
        }

        // ---- Left column ----

        private void DrawSettings()
        {
            EditorGUILayout.LabelField("Generator", EditorStyles.boldLabel);
            var s = _settings;
            s.Width = EditorGUILayout.IntSlider("Width", s.Width, 2, 40);
            s.Height = EditorGUILayout.IntSlider("Height", s.Height, 2, 40);
            s.Shape = (ShapeKind)EditorGUILayout.EnumPopup("Shape", s.Shape);
            s.FillRatio = EditorGUILayout.Slider("Fill Ratio", s.FillRatio, 0.05f, 1f);
            s.MinLength = EditorGUILayout.IntSlider("Min Length", s.MinLength, 2, 20);
            s.MaxLength = EditorGUILayout.IntSlider("Max Length", Mathf.Max(s.MaxLength, s.MinLength), s.MinLength, 30);
            s.TurnChance = EditorGUILayout.Slider("Turn Chance", s.TurnChance, 0f, 1f);

            using (new EditorGUILayout.HorizontalScope())
            {
                s.Seed = EditorGUILayout.IntField("Seed", s.Seed);
                if (GUILayout.Button("Random", GUILayout.Width(64))) s.Seed = Random.Range(1, 1_000_000);
            }

            s.Candidates = EditorGUILayout.IntSlider(new GUIContent("Candidates", "Generate this many and keep the best match."), s.Candidates, 1, 50);
            s.TargetDifficulty = EditorGUILayout.FloatField(new GUIContent("Target Difficulty", "0 = hardest candidate."), s.TargetDifficulty);
        }

        private void DrawActions()
        {
            if (GUILayout.Button("Generate", GUILayout.Height(32))) Generate();

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_level == null))
                {
                    if (GUILayout.Button("Save As…")) Save();
                    if (GUILayout.Button("Repair")) Repair();
                    if (GUILayout.Button("Copy JSON")) EditorGUIUtility.systemCopyBuffer = LevelSerializer.ToJson(_level);
                }
                if (GUILayout.Button("Load…")) Load();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("New Empty"))
                {
                    SetLevel(new LevelData { Width = _settings.Width, Height = _settings.Height }, "Empty level. Drag in the preview to draw arrows.");
                    _editMode = true;
                }
                _editMode = GUILayout.Toggle(_editMode, "Edit Mode", "Button");
            }
            if (_editMode)
                EditorGUILayout.HelpBox("Drag: draw an arrow (tail to head)\nRight-click: delete arrow\nShift + click: reverse arrow", MessageType.None);

            _highlightOpen = EditorGUILayout.Toggle("Highlight Open Arrows", _highlightOpen);
            _showOrder = EditorGUILayout.Toggle("Show Solution Order", _showOrder);
        }

        private void DrawStats()
        {
            EditorGUILayout.LabelField("Result", EditorStyles.boldLabel);
            if (_level == null || _solution == null)
            {
                EditorGUILayout.HelpBox("Press Generate or Load a level.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Size", $"{_level.Width} x {_level.Height}");
            EditorGUILayout.LabelField("Arrows", _level.Arrows.Count.ToString());
            EditorGUILayout.LabelField("Coverage", _coverage.ToString("P0"));
            EditorGUILayout.LabelField("Solvable", _solution.IsSolvable ? "Yes" : $"NO ({_solution.StuckArrows.Count} stuck)");
            EditorGUILayout.LabelField("Avg Open Arrows", _solution.AverageOpenArrows.ToString("F2"));
            EditorGUILayout.LabelField("Min Open Arrows", _solution.MinOpenArrows.ToString());
            EditorGUILayout.LabelField("Difficulty Score", _solution.DifficultyScore.ToString("F2"));
            if (!string.IsNullOrEmpty(_status)) EditorGUILayout.HelpBox(_status, MessageType.None);
        }

        private void DrawCampaign()
        {
            EditorGUILayout.LabelField("Campaign", EditorStyles.boldLabel);
            _campaignSeed = EditorGUILayout.IntField("Base Seed", _campaignSeed);
            EditorGUILayout.HelpBox($"Generates {Campaign.Count} levels (3 tutorials + tiers sorted easy → hard) into {LevelsFolder} and refreshes the level database.", MessageType.None);
            if (GUILayout.Button($"Generate {Campaign.Count}-Level Campaign", GUILayout.Height(28)))
            {
                if (EditorUtility.DisplayDialog("Generate campaign",
                        $"This replaces every level_*.json in {LevelsFolder}. Continue?", "Generate", "Cancel"))
                    GenerateCampaign(_campaignSeed);
            }
        }

        // ---- Actions ----

        private void Generate()
        {
            var result = LevelGenerator.Generate(_settings);
            SetLevel(result.Data, $"Seed {_settings.Seed}: {result.Data.Arrows.Count} arrows.");
        }

        private void SetLevel(LevelData data, string status)
        {
            _level = data;
            var errors = LevelValidator.Validate(data);
            if (errors.Count > 0)
            {
                _solution = null;
                _status = string.Join("\n", errors.Take(8));
                Repaint();
                return;
            }

            _solution = LevelSolver.Solve(LevelModel.FromData(data));
            _coverage = ComputeCoverage(data);
            _status = status;
            Repaint();
        }

        private static float ComputeCoverage(LevelData data)
        {
            int cells = data.Arrows.Sum(a => a.Cells.Count);
            return (float)cells / (data.Width * data.Height);
        }

        private void Save()
        {
            string path = EditorUtility.SaveFilePanelInProject("Save level", "level_new", "json", "Save level JSON", LevelsFolder);
            if (string.IsNullOrEmpty(path)) return;
            File.WriteAllText(path, LevelSerializer.ToJson(_level));
            AssetDatabase.ImportAsset(path);
            GameSceneBuilder.RefreshLevelDatabase();
            _status = $"Saved {path}";
        }

        private void Load()
        {
            string path = EditorUtility.OpenFilePanel("Load level", LevelsFolder, "json");
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                SetLevel(LevelSerializer.FromJson(File.ReadAllText(path)), $"Loaded {Path.GetFileName(path)}");
            }
            catch (System.Exception e)
            {
                _status = $"Load failed: {e.Message}";
            }
        }

        private void Repair()
        {
            var report = LevelRepair.Repair(_level);
            SetLevel(_level, report.Changed ? $"Repaired: {report}." : "Already solvable, nothing to repair.");
        }

        /// <summary>Writes the campaign as level_001.json … level_030.json.</summary>
        public static void GenerateCampaign(int baseSeed)
        {
            try
            {
                var levels = Campaign.Generate(baseSeed, (i, n) =>
                    EditorUtility.DisplayProgressBar("Generating campaign", $"Level {i + 1}/{n}", (float)i / n));

                foreach (string old in Directory.GetFiles(LevelsFolder, "level_*.json"))
                    AssetDatabase.DeleteAsset(old.Replace('\\', '/'));

                var log = new StringBuilder("[Arrows] Campaign generated:\n");
                for (int i = 0; i < levels.Count; i++)
                {
                    var l = levels[i];
                    string path = $"{LevelsFolder}/level_{i + 1:000}.json";
                    File.WriteAllText(path, LevelSerializer.ToJson(l.Data));
                    log.AppendLine($"{i + 1,2}. {l.Data.Width}x{l.Data.Height} {l.Settings.Shape,-9} arrows {l.Data.Arrows.Count,3}  " +
                                   $"coverage {l.Coverage:P0}  avgOpen {l.Solution.AverageOpenArrows:F2}  difficulty {l.Difficulty:F2}");
                }

                AssetDatabase.Refresh();
                GameSceneBuilder.RefreshLevelDatabase();
                Debug.Log(log.ToString());
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [MenuItem("Tools/Arrows/Generate Campaign (default seed)", priority = 11)]
        private static void GenerateCampaignMenu()
        {
            if (EditorUtility.DisplayDialog("Generate campaign", $"Replace every level_*.json in {LevelsFolder}?", "Generate", "Cancel"))
                GenerateCampaign(1000);
        }

        // ---- Edit mode ----

        private void HandleEditInput(Rect area)
        {
            var e = Event.current;
            switch (e.type)
            {
                case EventType.MouseDown when area.Contains(e.mousePosition) && TryCellAt(e.mousePosition, out var cell):
                {
                    int owner = OwnerOf(cell);
                    if (e.button == 1 && owner >= 0)
                    {
                        _level.Arrows.RemoveAt(owner);
                        SetLevel(_level, "Arrow removed.");
                    }
                    else if (e.button == 0 && e.shift && owner >= 0)
                    {
                        _level.Arrows[owner].Cells.Reverse();
                        SetLevel(_level, "Arrow reversed.");
                    }
                    else if (e.button == 0 && owner < 0)
                    {
                        _drawing.Clear();
                        _drawing.Add(cell);
                    }
                    e.Use();
                    break;
                }
                case EventType.MouseDrag when _drawing.Count > 0:
                    if (TryCellAt(e.mousePosition, out var target)) ExtendDrawing(target);
                    Repaint();
                    e.Use();
                    break;
                case EventType.MouseUp when _drawing.Count > 0:
                    if (_drawing.Count >= 2)
                    {
                        _level.Arrows.Add(new ArrowData(_drawing));
                        SetLevel(_level, $"Arrow added ({_drawing.Count} cells).");
                    }
                    _drawing.Clear();
                    Repaint();
                    e.Use();
                    break;
            }
        }

        // Walks towards the target one cell at a time, so fast drags along a row or column keep every cell.
        private void ExtendDrawing(GridPos target)
        {
            for (int guard = 0; guard < 64; guard++)
            {
                var last = _drawing[_drawing.Count - 1];
                if (last == target) return;

                if (_drawing.Count >= 2 && _drawing[_drawing.Count - 2] == target)
                {
                    _drawing.RemoveAt(_drawing.Count - 1); // backtrack
                    return;
                }

                var delta = target - last;
                var step = Mathf.Abs(delta.X) >= Mathf.Abs(delta.Y)
                    ? new GridPos(System.Math.Sign(delta.X), 0)
                    : new GridPos(0, System.Math.Sign(delta.Y));
                var next = last + step;
                if (OwnerOf(next) >= 0 || _drawing.Contains(next)) return;
                _drawing.Add(next);
            }
        }

        private int OwnerOf(GridPos cell)
        {
            for (int i = 0; i < _level.Arrows.Count; i++)
                if (_level.Arrows[i].Cells.Contains(cell)) return i;
            return -1;
        }

        private bool TryCellAt(Vector2 mouse, out GridPos cell)
        {
            cell = default;
            if (_level == null || _cell <= 0f) return false;
            int x = Mathf.FloorToInt((mouse.x - _origin.x) / _cell);
            int y = _level.Height - 1 - Mathf.FloorToInt((mouse.y - _origin.y) / _cell);
            if (x < 0 || y < 0 || x >= _level.Width || y >= _level.Height) return false;
            cell = new GridPos(x, y);
            return true;
        }

        // ---- Preview ----

        private void ComputeLayout(Rect area)
        {
            if (_level == null) return;
            const float margin = 16f;
            _cell = Mathf.Min((area.width - margin * 2) / _level.Width, (area.height - margin * 2) / _level.Height);
            _origin = new Vector2(
                area.x + (area.width - _cell * _level.Width) * 0.5f,
                area.y + (area.height - _cell * _level.Height) * 0.5f);
        }

        private void DrawPreview(Rect area)
        {
            EditorGUI.DrawRect(area, new Color(0.965f, 0.957f, 0.937f));
            if (_level == null) return;

            float cell = _cell;
            var origin = _origin;

            // Grid y points up; GUI y points down.
            Vector2 ToGui(float x, float y) => new Vector2(origin.x + (x + 0.5f) * cell, origin.y + (_level.Height - 0.5f - y) * cell);

            var dot = new Color(0f, 0f, 0f, 0.12f);
            float dotSize = Mathf.Max(1.5f, cell * 0.12f);
            for (int y = 0; y < _level.Height; y++)
            for (int x = 0; x < _level.Width; x++)
            {
                var p = ToGui(x, y);
                EditorGUI.DrawRect(new Rect(p.x - dotSize * 0.5f, p.y - dotSize * 0.5f, dotSize, dotSize), dot);
            }

            HashSet<int> open = null;
            Dictionary<int, int> order = null;
            if (LevelValidator.Validate(_level).Count == 0)
            {
                var model = LevelModel.FromData(_level);
                if (_highlightOpen)
                {
                    var ids = new List<int>();
                    LevelSolver.CollectOpen(model, ids);
                    open = new HashSet<int>(ids);
                }
                if (_showOrder && _solution != null)
                {
                    order = new Dictionary<int, int>();
                    for (int i = 0; i < _solution.Order.Count; i++) order[_solution.Order[i]] = i + 1;
                }
            }

            float width = Mathf.Max(2f, cell * 0.3f);
            var points = new List<Vector3>();
            for (int id = 0; id < _level.Arrows.Count; id++)
            {
                var cells = _level.Arrows[id].Cells;
                if (cells.Count < 2) continue;

                var color = _palette.Length > 0 ? _palette[id % _palette.Length] : Color.black;
                if (open != null && !open.Contains(id)) color = Color.Lerp(color, Color.white, 0.55f);
                if (_solution != null && _solution.StuckArrows.Contains(id)) color = Color.red;

                points.Clear();
                foreach (var c in cells) points.Add(ToGui(c.X, c.Y));
                Handles.color = color;
                Handles.DrawAAPolyLine(width, points.ToArray());

                var head = cells[cells.Count - 1];
                var dir = head - cells[cells.Count - 2];
                Vector2 h = ToGui(head.X, head.Y);
                var f = new Vector2(dir.X, -dir.Y);
                var side = new Vector2(-f.y, f.x);
                Handles.DrawAAConvexPolygon(
                    h + f * cell * 0.45f,
                    h + side * cell * 0.3f - f * cell * 0.05f,
                    h - side * cell * 0.3f - f * cell * 0.05f);

                if (order != null && order.TryGetValue(id, out int n) && cell >= 14f)
                {
                    var style = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter };
                    style.normal.textColor = Color.black;
                    GUI.Label(new Rect(h.x - 15, h.y - cell * 0.5f - 14, 30, 14), n.ToString(), style);
                }
            }

            if (_drawing.Count > 0)
            {
                points.Clear();
                foreach (var c in _drawing) points.Add(ToGui(c.X, c.Y));
                if (points.Count == 1) points.Add(points[0] + Vector3.right * 0.1f);
                Handles.color = new Color(1f, 0.75f, 0.1f);
                Handles.DrawAAPolyLine(width, points.ToArray());
            }
        }
    }
}
