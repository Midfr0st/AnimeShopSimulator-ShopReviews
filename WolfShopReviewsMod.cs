using System.Reflection;
using Il2CppProject.Code.Gameplay.UI.Game;
using Il2CppProject.Code.Gameplay.UI.GameMenu;
using Il2CppProject.Code.Gameplay.UI.Menu;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(WolfShopReviews.WolfShopReviewsMod), "Anime Shop: Shop Reviews", "0.4.4", "WolfMods")]

namespace WolfShopReviews;

public sealed class WolfShopReviewsMod : MelonMod
{
    private const string WolfModId = "wolfmod.shop_reviews";
    private const int MaxRegistrationAttempts = 4;
    private const float RetryDelay = 1.5f;
    private const float DependencyErrorDisplaySeconds = 30f;

    private float _nextRegistrationAttempt;
    private float _dependencyErrorUntil;
    private int _registrationAttempts;
    private bool _runtimeInitialized;
    private bool _dependencyFailed;
    private bool _dependencyNotificationStarted;
    private GUIStyle? _dependencyErrorStyle;
    private MenuView? _mainMenuView;
    private float _nextMainMenuLookup;
    private GameView? _gameView;
    private GameMenuView? _gameMenuView;
    private float _nextGameplayUiLookup;

    internal static bool FeatureEnabled { get; private set; }

    public override void OnInitializeMelon()
    {
        FeatureEnabled = false;
        TryInitialize();
    }

    public override void OnUpdate()
    {
        if (!_runtimeInitialized && !_dependencyFailed && Time.unscaledTime >= _nextRegistrationAttempt)
            TryInitialize();

    }

    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        _mainMenuView = null;
        _nextMainMenuLookup = 0f;
        _gameView = null;
        _gameMenuView = null;
        _nextGameplayUiLookup = 0f;
        ReviewRuntime.Reset();
        ReviewHud.OnSceneChanged();
    }

    public override void OnGUI()
    {
        // This HUD has no interactive controls. Rendering only on Repaint avoids
        // repeating data/style work during Unity's Layout and input IMGUI passes.
        if (Event.current != null && Event.current.type != EventType.Repaint)
            return;

        if (!_dependencyFailed)
        {
            if (_runtimeInitialized && FeatureEnabled && !IsMainMenuVisible() &&
                ShouldDrawGameplayHud(out var gameView))
            {
                ReviewHud.Draw(gameView);
            }
            return;
        }

        if (!IsMainMenuVisible())
            return;

        if (!_dependencyNotificationStarted)
        {
            _dependencyNotificationStarted = true;
            _dependencyErrorUntil = Time.unscaledTime + DependencyErrorDisplaySeconds;
        }

        if (Time.unscaledTime < _dependencyErrorUntil)
            DrawDependencyError();
    }

    public override void OnDeinitializeMelon()
    {
        FeatureEnabled = false;
        ReviewRuntime.Reset();
        ReviewPatches.Uninstall();
        ReviewWindow.Close();
        ReviewHud.Shutdown();
        WolfModBridge.Unregister(WolfModId);
    }

    private void TryInitialize()
    {
        _registrationAttempts++;
        if (!WolfModBridge.TryRegister(
                WolfModId,
                "Отзывы о магазине",
                "0.4.4",
                "Редкие отзывы покупателей на основе реальных событий их визита.",
                SetFeatureEnabled,
                DrawWolfModSettings,
                () => false))
        {
            if (_registrationAttempts < MaxRegistrationAttempts)
            {
                _nextRegistrationAttempt = Time.unscaledTime + RetryDelay;
                return;
            }

            _dependencyFailed = true;
            FeatureEnabled = false;
            LoggerInstance.Error(
                $"Shop Reviews отключён: WolfCore не найден или несовместим. " +
                $"Установите WolfCore.dll и перезапустите игру. {WolfModBridge.LastError}");
            return;
        }

        try
        {
            ReviewSettings.Initialize();
            ReviewStore.Initialize();
            var templateErrors = ReviewTextGenerator.ValidateTemplates();
            if (templateErrors.Count > 0)
                throw new InvalidOperationException(
                    "Проверка текстов отзывов не пройдена: " + string.Join(" | ", templateErrors.Take(8)));
            ReviewPatches.Install();
            _runtimeInitialized = true;
            LoggerInstance.Msg(
                "Shop Reviews 0.4.4 загружен. Терминальная вкладка и игровой HUD подключены.");
        }
        catch
        {
            FeatureEnabled = false;
            ReviewPatches.Uninstall();
            WolfModBridge.Unregister(WolfModId);
            throw;
        }
    }

    private static void SetFeatureEnabled(bool enabled)
    {
        FeatureEnabled = enabled;
        if (!enabled)
        {
            ReviewRuntime.Reset();
            ReviewWindow.Close();
            ReviewHud.ResetTransient();
        }
    }

    private static void DrawWolfModSettings()
    {
        GUILayout.Label("Отзывы создаются только при уходе покупателя и не для каждого визита.");
        GUILayout.Label("Негативные впечатления чаще приводят к отзыву, чем положительные.");
        GUILayout.Space(10f);

        var settings = ReviewSettings.Value;
        var changed = false;
        var positive = DrawChance("Положительный отзыв", settings.PositiveReviewChance);
        var neutral = DrawChance("Нейтральный отзыв", settings.NeutralReviewChance);
        var negative = DrawChance("Негативный отзыв", settings.NegativeReviewChance);
        var severe = DrawChance("Серьёзная проблема", settings.SevereNegativeReviewChance);
        if (positive != settings.PositiveReviewChance ||
            neutral != settings.NeutralReviewChance ||
            negative != settings.NegativeReviewChance ||
            severe != settings.SevereNegativeReviewChance)
        {
            settings.PositiveReviewChance = positive;
            settings.NeutralReviewChance = neutral;
            settings.NegativeReviewChance = negative;
            settings.SevereNegativeReviewChance = severe;
            changed = true;
        }

        GUILayout.Space(8f);
        var showHudRating = GUILayout.Toggle(
            settings.ShowHudRating,
            "Показывать рейтинг магазина под панелями слева");
        var showNotifications = GUILayout.Toggle(
            settings.ShowReviewNotifications,
            "Показывать уведомления о новых отзывах");
        var previousEnabled = GUI.enabled;
        GUI.enabled = previousEnabled && showNotifications;
        var minimalNotifications = GUILayout.Toggle(
            settings.MinimalReviewNotifications,
            "Минималистичные уведомления без оценки (серия отображается как ×N)");
        GUI.enabled = previousEnabled;

        if (showHudRating != settings.ShowHudRating ||
            showNotifications != settings.ShowReviewNotifications ||
            minimalNotifications != settings.MinimalReviewNotifications)
        {
            var notificationModeChanged =
                minimalNotifications != settings.MinimalReviewNotifications;
            settings.ShowHudRating = showHudRating;
            settings.ShowReviewNotifications = showNotifications;
            settings.MinimalReviewNotifications = minimalNotifications;
            if (!showNotifications || notificationModeChanged)
                ReviewHud.ResetTransient();
            changed = true;
        }

        GUILayout.Space(8f);
        var diagnostic = GUILayout.Toggle(settings.DiagnosticLogging, "Подробная диагностика в журнале MelonLoader");
        if (diagnostic != settings.DiagnosticLogging)
        {
            settings.DiagnosticLogging = diagnostic;
            changed = true;
        }

        if (changed)
            ReviewSettings.Save();
    }

    private static float DrawChance(string label, float value)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label($"{label}: {value:P1}", GUILayout.Width(260f));
        var next = GUILayout.HorizontalSlider(value, 0f, 0.50f, GUILayout.Width(260f));
        GUILayout.EndHorizontal();
        next = (float)Math.Round(next, 3);
        return Math.Abs(next - value) < 0.0005f ? value : next;
    }

    private void DrawDependencyError()
    {
        _dependencyErrorStyle ??= new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            wordWrap = true,
            padding = new RectOffset(18, 18, 12, 12)
        };
        _dependencyErrorStyle.normal.textColor = new Color(1f, 0.86f, 0.86f, 1f);

        var width = Math.Min(720f, Screen.width - 48f);
        const float height = 112f;
        var bottomClearance = Math.Max(130f, Screen.height * 0.12f);
        GUI.depth = -20000;
        GUI.Box(
            new Rect(24f, Screen.height - height - bottomClearance, width, height),
            "Shop Reviews отключён\nWolfCore не найден или несовместим. Установите WolfCore.dll и перезапустите игру.",
            _dependencyErrorStyle);
    }

    private bool IsMainMenuVisible()
    {
        try
        {
            if ((_mainMenuView == null || !_mainMenuView) && Time.unscaledTime >= _nextMainMenuLookup)
            {
                _nextMainMenuLookup = Time.unscaledTime + 2f;
                _mainMenuView = UnityEngine.Object.FindObjectOfType<MenuView>();
            }

            return _mainMenuView != null && _mainMenuView.isActiveAndEnabled &&
                   _mainMenuView.gameObject.activeInHierarchy;
        }
        catch
        {
            return false;
        }
    }

    private bool ShouldDrawGameplayHud(out GameView? gameView)
    {
        gameView = null;
        try
        {
            if (Time.timeScale <= 0.001f)
                return false;

            if (Time.unscaledTime >= _nextGameplayUiLookup)
            {
                _nextGameplayUiLookup = Time.unscaledTime + 2f;
                if (_gameView == null || !_gameView)
                    _gameView = Resources.FindObjectsOfTypeAll<GameView>()
                        .FirstOrDefault(view => view != null && view.gameObject.scene.IsValid());
                if (_gameMenuView == null || !_gameMenuView)
                    _gameMenuView = Resources.FindObjectsOfTypeAll<GameMenuView>()
                        .FirstOrDefault(view => view != null && view.gameObject.scene.IsValid());
            }

            if (_gameMenuView != null && _gameMenuView &&
                _gameMenuView.gameObject.activeInHierarchy &&
                ((_gameMenuView._pauseText != null && _gameMenuView._pauseText.activeInHierarchy) ||
                 (_gameMenuView._continueButton != null &&
                  _gameMenuView._continueButton.gameObject.activeInHierarchy)))
            {
                return false;
            }

            if (_gameView == null || !_gameView ||
                !_gameView.gameObject.activeInHierarchy ||
                !_gameView._areUiObjectsEnabled)
            {
                return false;
            }

            // The game toggles these native HUD objects off for all full-screen
            // interactions, including both shop terminals. Follow that lifecycle
            // instead of maintaining a brittle list of terminal implementations.
            if (_gameView._cleanSlider == null ||
                !_gameView._cleanSlider.gameObject.activeInHierarchy)
            {
                return false;
            }

            gameView = _gameView;
            return true;
        }
        catch
        {
            return false;
        }
    }
}

internal static class WolfModBridge
{
    private const string RegistryTypeName = "WolfCore.WolfModRegistry";
    public static bool Registered { get; private set; }
    public static string LastError { get; private set; } = string.Empty;

    public static bool TryRegister(
        string id,
        string displayName,
        string version,
        string description,
        Action<bool> onEnabledChanged,
        Action drawSettings,
        Func<bool> isCapturingInput)
    {
        if (Registered)
            return true;
        var registry = FindRegistryType();
        if (registry == null)
        {
            LastError = "Тип WolfCore.WolfModRegistry пока не найден.";
            return false;
        }

        try
        {
            var register = registry.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(method =>
                    method.Name == "Register" &&
                    method.GetParameters().Length == 8 &&
                    method.GetParameters()[0].ParameterType == typeof(string));
            var registerTerminal = registry.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(method =>
                    method.Name == "RegisterTerminalPage" &&
                    method.GetParameters().Length == 8);
            if (register == null || registerTerminal == null)
            {
                LastError = "Установленная версия WolfCore не поддерживает централизованные вкладки терминала.";
                return false;
            }
            using var stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("WolfShopReviews.Assets.reviews-tile.png");
            if (stream == null)
            {
                LastError = "В DLL отсутствует изображение плитки отзывов.";
                return false;
            }
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            register.Invoke(null, new object?[]
            {
                id,
                displayName,
                version,
                description,
                onEnabledChanged,
                drawSettings,
                isCapturingInput,
                true
            });
            registerTerminal.Invoke(null, new object?[]
            {
                id,
                "wolfmod.shop_reviews.terminal",
                "ОТЗЫВЫ",
                memory.ToArray(),
                new Func<Il2CppProject.Code.Gameplay.UI.Computer.ComputerWorldView, bool>(ReviewWindow.Open),
                new Action(ReviewWindow.Close),
                new Func<bool>(() => ReviewWindow.IsOpen),
                new Action(ReviewWindow.TickInput)
            });
            Registered = true;
            LastError = string.Empty;
            MelonLogger.Msg("Shop Reviews: подключён к WolfCore.");
            return true;
        }
        catch (Exception exception)
        {
            LastError = exception.GetBaseException().Message;
            return false;
        }
    }

    public static void Unregister(string id)
    {
        if (!Registered)
            return;
        try
        {
            FindRegistryType()?.GetMethod(
                "Unregister",
                BindingFlags.Public | BindingFlags.Static)?.Invoke(null, new object?[] { id });
        }
        catch
        {
            // The core may already be shutting down.
        }
        Registered = false;
    }

    private static Type? FindRegistryType()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (string.Equals(assembly.GetName().Name, "WolfCore", StringComparison.Ordinal))
                return assembly.GetType(RegistryTypeName, false);
        }
        return null;
    }
}
