using System.IO;
using System.Linq;
using Arrows.Core;
using Arrows.Core.Audio;
using Arrows.UI;
using Arrows.Utils;
using Arrows.View;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Arrows.Editor
{
    /// <summary>
    /// One-click project setup: creates the config assets, the level database and a fully wired
    /// Game scene (board, camera, input, all UI screens), so nothing is assembled by hand.
    /// Re-running it regenerates the scene; assets such as GameConfig are kept.
    /// </summary>
    public static class GameSceneBuilder
    {
        private const string Root = "Assets/_Project";
        private const string ScenePath = Root + "/Scenes/Game.unity";
        private const string ConfigPath = Root + "/GameConfig.asset";
        private const string DatabasePath = Root + "/Levels/LevelDatabase.asset";
        private const string LevelsFolder = Root + "/Levels";
        private const string MaterialPath = Root + "/Art/ArrowSprite.mat";
        private const string AudioLibraryPath = Root + "/Audio/AudioLibrary.asset";

        private static readonly Color Ink = new Color(0.17f, 0.18f, 0.22f);
        private static readonly Color Green = new Color(0.30f, 0.72f, 0.48f);
        private static readonly Color Orange = new Color(0.96f, 0.58f, 0.20f);
        private static readonly Color Neutral = new Color(1f, 1f, 1f, 0.95f);
        private static readonly Color Dim = new Color(0.08f, 0.09f, 0.12f, 0.55f);

        private static Sprite _rounded;

        [MenuItem("Tools/Arrows/Setup Game Scene", priority = 0)]
        public static void SetupGameScene()
        {
            if (!EnsureTextMeshProResources()) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var config = GetOrCreateConfig();
            _rounded = SpriteBaker.GetOrBake(ProceduralSprites.Shape.RoundedRect);
            var heart = SpriteBaker.GetOrBake(ProceduralSprites.Shape.Heart);

            var game = BuildScene(config, heart);
            ConfigurePlayer();

            var check = new SerializedObject(game).FindProperty("_config").objectReferenceValue;
            if (check == null) Debug.LogError("[Arrows] GameConfig was not assigned to GameManager. Please report this.");
            else Debug.Log($"[Arrows] Game scene created at {ScenePath}. Press Play.");
        }

        [MenuItem("Tools/Arrows/Refresh Level Database", priority = 1)]
        public static LevelDatabase RefreshLevelDatabase()
        {
            var db = LoadOrCreate<LevelDatabase>(DatabasePath);
            db.Levels = AssetDatabase.FindAssets("t:TextAsset", new[] { LevelsFolder })
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .Where(path => path.EndsWith(".json"))
                .OrderBy(path => Path.GetFileName(path), System.StringComparer.Ordinal)
                .Select(path => AssetDatabase.LoadAssetAtPath<TextAsset>(path))
                .ToList();
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Arrows] Level database refreshed: {db.Levels.Count} levels.");
            return db;
        }

        [MenuItem("Tools/Arrows/Reset Progress", priority = 20)]
        public static void ResetProgress()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Debug.Log("[Arrows] Progress reset (PlayerPrefs cleared).");
        }

        // ---- Assets ----

        private static bool EnsureTextMeshProResources()
        {
            if (Resources.Load<TMP_Settings>("TMP Settings") != null) return true;

            TMP_PackageResourceImporter.ImportResources(true, false, false);
            EditorUtility.DisplayDialog("Arrows setup",
                "TextMeshPro Essential Resources are being imported.\n\n" +
                "When the import finishes, run Tools > Arrows > Setup Game Scene again.", "OK");
            return false;
        }

        private static GameConfig GetOrCreateConfig()
        {
            LoadOrCreate<GameConfig>(ConfigPath);
            var db = RefreshLevelDatabase();

            // Reload from disk so the scene references the persisted asset.
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            config.Levels = db;
            if (config.Theme.SpriteMaterial == null) config.Theme.SpriteMaterial = GetOrCreateSpriteMaterial();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            return config;
        }

        private static Material GetOrCreateSpriteMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat != null) return mat;

            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
            mat = new Material(shader) { name = "ArrowSprite" };
            AssetDatabase.CreateAsset(mat, MaterialPath);
            return mat;
        }

        private static AudioLibrary GetOrCreateAudioLibrary()
        {
            if (!AssetDatabase.IsValidFolder(Root + "/Audio")) AssetDatabase.CreateFolder(Root, "Audio");
            var library = LoadOrCreate<AudioLibrary>(AudioLibraryPath);
            library.EnsureAllIds();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            return library;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            return asset;
        }

        private static void ConfigurePlayer()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        }

        // ---- Scene ----

        private static GameManager BuildScene(GameConfig config, Sprite heart)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = config.Theme.BackgroundColor;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 50f;
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            var cameraController = camGo.AddComponent<CameraController>();

            var board = new GameObject("Board").AddComponent<BoardView>();

            var systems = new GameObject("GameManager");
            var input = systems.AddComponent<InputHandler>();
            var game = systems.AddComponent<GameManager>();

            Assign(game, "_config", config);
            Assign(game, "_board", board);
            Assign(game, "_camera", cameraController);
            Assign(game, "_input", input);

            var audioGo = new GameObject("Audio");
            var audio = audioGo.AddComponent<AudioManager>();
            Assign(audio, "_library", GetOrCreateAudioLibrary());

            var feedback = systems.AddComponent<FeedbackController>();
            Assign(feedback, "_game", game);
            Assign(feedback, "_audio", audio);
            Assign(feedback, "_camera", cameraController);
            Assign(feedback, "_config", config);

            camGo.AddComponent<AudioListener>();

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            BuildUI(game, config, heart);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            return game;
        }

        private static void BuildUI(GameManager game, GameConfig config, Sprite heart)
        {
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            var ui = canvasGo.AddComponent<UIManager>();
            Assign(ui, "_game", game);

            // Full-screen layers. Backgrounds ignore the safe area; content respects it.
            var safe = Stretch(NewRect("SafeArea", canvasGo.transform));
            safe.gameObject.AddComponent<SafeArea>();

            // HUD
            var hudPanel = NewPanel<UIPanel>("HUD", safe, null);
            var hud = hudPanel.gameObject.AddComponent<HudView>();
            var levelText = NewText("LevelText", hudPanel.transform, "Level 1", 64, FontStyles.Bold);
            Place(levelText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(480f, 90f));

            var hearts = Place(NewRect("Hearts", hudPanel.transform), new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(400f, 70f));
            var row = hearts.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 14f;
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = row.childControlHeight = false;
            row.childForceExpandWidth = row.childForceExpandHeight = false;

            var settingsButton = NewButton("SettingsButton", hudPanel.transform, "Settings", Neutral, 40, out _);
            Place(settingsButton, new Vector2(0f, 1f), new Vector2(40f, -50f), new Vector2(250f, 110f));
            var restartButton = NewButton("RestartButton", hudPanel.transform, "Restart", Neutral, 40, out _);
            Place(restartButton, new Vector2(1f, 1f), new Vector2(-40f, -50f), new Vector2(250f, 110f));
            var hintButton = NewButton("HintButton", hudPanel.transform, "Hint", Orange, 54, out var hintLabel);
            hintLabel.color = Color.white;
            Place(hintButton, new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(360f, 140f));

            Assign(hud, "_levelText", levelText);
            Assign(hud, "_heartsRoot", hearts);
            Assign(hud, "_heartSprite", heart);

            // Main menu (semi-transparent so the level shows behind it)
            var bg = config.Theme.BackgroundColor;
            var menu = NewPanel<UIPanel>("Menu", canvasGo.transform, new Color(bg.r, bg.g, bg.b, 0.9f));
            var menuContent = Stretch(NewRect("Content", menu.transform));
            menuContent.gameObject.AddComponent<SafeArea>();
            var title = NewText("Title", menuContent, "ARROWS", 170, FontStyles.Bold);
            Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 420f), new Vector2(1000f, 220f));
            var subtitle = NewText("Subtitle", menuContent, "Puzzle Escape", 64, FontStyles.Normal);
            subtitle.color = new Color(Ink.r, Ink.g, Ink.b, 0.6f);
            Place(subtitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 290f), new Vector2(1000f, 100f));
            var playButton = NewButton("PlayButton", menuContent, "PLAY", Green, 90, out var playLabel);
            playLabel.color = Color.white;
            Place(playButton, new Vector2(0.5f, 0.5f), new Vector2(0f, -260f), new Vector2(620f, 200f));
            var menuLevel = NewText("LevelText", menuContent, "Level 1", 56, FontStyles.Bold);
            menuLevel.color = new Color(Ink.r, Ink.g, Ink.b, 0.7f);
            Place(menuLevel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -430f), new Vector2(600f, 90f));

            // Win
            var win = NewPanel<UIPanel>("WinPanel", canvasGo.transform, Dim);
            var winCard = NewCard(win, new Vector2(860f, 640f));
            var winTitle = NewText("Title", winCard, "Level 1\nComplete!", 92, FontStyles.Bold);
            Place(winTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(780f, 260f));
            var nextButton = NewButton("NextButton", winCard, "NEXT", Green, 72, out var nextLabel);
            nextLabel.color = Color.white;
            Place(nextButton, new Vector2(0.5f, 0f), new Vector2(0f, 80f), new Vector2(520f, 170f));

            // Fail
            var fail = NewPanel<UIPanel>("FailPanel", canvasGo.transform, Dim);
            var failCard = NewCard(fail, new Vector2(860f, 640f));
            var failTitle = NewText("Title", failCard, "Out of Lives", 92, FontStyles.Bold);
            Place(failTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(780f, 130f));
            var failSub = NewText("Subtitle", failCard, "Don't give up!", 54, FontStyles.Normal);
            failSub.color = new Color(Ink.r, Ink.g, Ink.b, 0.6f);
            Place(failSub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -220f), new Vector2(780f, 90f));
            var retryButton = NewButton("RetryButton", failCard, "RETRY", Orange, 72, out var retryLabel);
            retryLabel.color = Color.white;
            Place(retryButton, new Vector2(0.5f, 0f), new Vector2(0f, 80f), new Vector2(520f, 170f));

            // Settings
            var settings = NewPanel<SettingsPanel>("SettingsPanel", canvasGo.transform, Dim);
            var settingsCard = NewCard(settings, new Vector2(820f, 820f));
            var settingsTitle = NewText("Title", settingsCard, "Settings", 84, FontStyles.Bold);
            Place(settingsTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(700f, 120f));
            var soundButton = NewButton("SoundButton", settingsCard, "Sound: ON", Green, 54, out var soundLabel);
            soundLabel.color = Color.white;
            Place(soundButton, new Vector2(0.5f, 1f), new Vector2(0f, -240f), new Vector2(600f, 140f));
            var hapticsButton = NewButton("VibrationButton", settingsCard, "Vibration: ON", Green, 54, out var hapticsLabel);
            hapticsLabel.color = Color.white;
            Place(hapticsButton, new Vector2(0.5f, 1f), new Vector2(0f, -410f), new Vector2(600f, 140f));
            var closeButton = NewButton("CloseButton", settingsCard, "Close", new Color(0.9f, 0.9f, 0.92f), 54, out _);
            Place(closeButton, new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(400f, 130f));

            Assign(settings, "_soundButton", soundButton.GetComponent<Button>());
            Assign(settings, "_soundLabel", soundLabel);
            Assign(settings, "_hapticsButton", hapticsButton.GetComponent<Button>());
            Assign(settings, "_hapticsLabel", hapticsLabel);
            Assign(settings, "_closeButton", closeButton.GetComponent<Button>());

            Assign(ui, "_menuPanel", menu);
            Assign(ui, "_menuLevelText", menuLevel);
            Assign(ui, "_playButton", playButton.GetComponent<Button>());
            Assign(ui, "_hudPanel", hudPanel);
            Assign(ui, "_hud", hud);
            Assign(ui, "_settingsButton", settingsButton.GetComponent<Button>());
            Assign(ui, "_restartButton", restartButton.GetComponent<Button>());
            Assign(ui, "_hintButton", hintButton.GetComponent<Button>());
            Assign(ui, "_winPanel", win);
            Assign(ui, "_winTitle", winTitle);
            Assign(ui, "_nextButton", nextButton.GetComponent<Button>());
            Assign(ui, "_failPanel", fail);
            Assign(ui, "_retryButton", retryButton.GetComponent<Button>());
            Assign(ui, "_settingsPanel", settings);
        }

        // ---- UI helpers ----

        private static T NewPanel<T>(string name, Transform parent, Color? background) where T : UIPanel
        {
            var rt = Stretch(NewRect(name, parent));
            rt.gameObject.AddComponent<CanvasGroup>();
            if (background.HasValue)
            {
                var image = rt.gameObject.AddComponent<Image>();
                image.color = background.Value; // also blocks input to the board
            }
            return rt.gameObject.AddComponent<T>();
        }

        private static RectTransform NewCard(UIPanel panel, Vector2 size)
        {
            var card = Place(NewRect("Card", panel.transform), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            var image = card.gameObject.AddComponent<Image>();
            image.sprite = _rounded;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
            Assign(panel, "_content", card);
            return card;
        }

        private static RectTransform NewButton(string name, Transform parent, string label, Color color, float fontSize, out TextMeshProUGUI text)
        {
            var rt = NewRect(name, parent);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = _rounded;
            image.type = Image.Type.Sliced;
            image.color = color;
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f);
            button.colors = colors;
            rt.gameObject.AddComponent<ButtonPressScale>();

            text = NewText("Label", rt, label, fontSize, FontStyles.Bold);
            Stretch(text.rectTransform);
            return rt;
        }

        private static TextMeshProUGUI NewText(string name, Transform parent, string text, float size, FontStyles style)
        {
            var rt = NewRect(name, parent);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = TMP_Settings.defaultFontAsset;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Ink;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        private static RectTransform Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        /// <summary>Anchors and pivots at the same normalized point, then offsets and sizes.</summary>
        private static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        private static void Assign(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError($"[Arrows] Field '{field}' not found on {target.GetType().Name}.");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
