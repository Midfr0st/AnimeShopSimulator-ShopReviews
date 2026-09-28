using System.Globalization;
using Il2CppProject.Code.Gameplay.UI.Game;
using UnityEngine;

namespace WolfShopReviews;

internal sealed class ReviewHudNotification
{
    public int Stars { get; init; }
    public int Count { get; set; } = 1;
    public bool Minimal { get; init; }
    public float CreatedAt { get; set; }
    public float ExpiresAt { get; set; }
    public float CurrentY { get; set; } = float.NaN;
}

internal static class ReviewHud
{
    private const float NotificationLifetime = 6.0f;
    private const float NotificationFadeDuration = 1.15f;
    private const int MaximumDetailedNotifications = 6;

    private static readonly List<ReviewHudNotification> Notifications = new();
    private static readonly Vector3[] QuestCorners = new Vector3[4];
    private static Texture2D? _panelTexture;
    private static GUIStyle? _panelStyle;
    private static GUIStyle? _titleStyle;
    private static GUIStyle? _ratingStyle;
    private static GUIStyle? _starStyle;
    private static GUIStyle? _notificationTextStyle;
    private static GUIStyle? _notificationRatingStyle;
    private static float _styleScale;
    private static bool _ratingDirty = true;
    private static double _averageRating;
    private static long _ratingCount;
    private static Canvas? _questCanvas;
    private static int _questPanelInstanceId;

    public static void OnReviewPublished(int stars)
    {
        _ratingDirty = true;
        if (!WolfShopReviewsMod.FeatureEnabled || !ReviewSettings.Value.ShowReviewNotifications)
            return;

        var now = Time.unscaledTime;
        if (ReviewSettings.Value.MinimalReviewNotifications)
        {
            var current = Notifications.FirstOrDefault(item => item.Minimal && item.ExpiresAt > now);
            if (current != null)
            {
                current.Count++;
                current.CreatedAt = now;
                current.ExpiresAt = now + NotificationLifetime;
                return;
            }

            Notifications.Clear();
            Notifications.Add(new ReviewHudNotification
            {
                Stars = 0,
                Minimal = true,
                CreatedAt = now,
                ExpiresAt = now + NotificationLifetime
            });
            return;
        }

        Notifications.Insert(0, new ReviewHudNotification
        {
            Stars = Math.Clamp(stars, 1, 5),
            Minimal = false,
            CreatedAt = now,
            ExpiresAt = now + NotificationLifetime
        });
        if (Notifications.Count > MaximumDetailedNotifications)
            Notifications.RemoveRange(MaximumDetailedNotifications, Notifications.Count - MaximumDetailedNotifications);
    }

    public static void Draw(GameView? gameView)
    {
        var settings = ReviewSettings.Value;
        if (!settings.ShowHudRating && (!settings.ShowReviewNotifications || Notifications.Count == 0))
            return;

        var scale = Math.Clamp(Math.Min(Screen.width / 1920f, Screen.height / 1080f), 0.72f, 1.65f);
        EnsureStyles(scale);

        GUI.depth = -14000;
        if (settings.ShowHudRating)
            DrawRating(scale, ResolveRatingTop(gameView, scale));
        if (settings.ShowReviewNotifications)
            DrawNotifications(scale);
    }

    public static void ResetTransient()
    {
        Notifications.Clear();
        _ratingDirty = true;
    }

    public static void OnSceneChanged()
    {
        ResetTransient();
        _averageRating = 0d;
        _ratingCount = 0L;
        _questCanvas = null;
        _questPanelInstanceId = 0;
    }

    public static void Shutdown()
    {
        ResetTransient();
        if (_panelTexture != null)
            UnityEngine.Object.Destroy(_panelTexture);
        _panelTexture = null;
        _panelStyle = null;
        _titleStyle = null;
        _ratingStyle = null;
        _starStyle = null;
        _notificationTextStyle = null;
        _notificationRatingStyle = null;
        _styleScale = 0f;
    }

    private static void DrawRating(float scale, float top)
    {
        // Rating changes only when a review is published (or a scene/save is
        // changed), so polling the store every second only wastes frame time.
        if (_ratingDirty)
        {
            if (!ReviewStore.TryGetRatingSummary(out _averageRating, out _ratingCount))
            {
                _averageRating = 0d;
                _ratingCount = 0L;
            }

            _ratingDirty = false;
        }

        var rect = new Rect(30f * scale, top, 250f * scale, 72f * scale);
        GUI.Box(rect, GUIContent.none, _panelStyle!);
        GUI.Label(
            new Rect(rect.x + 14f * scale, rect.y + 8f * scale, rect.width - 28f * scale, 18f * scale),
            "РЕЙТИНГ МАГАЗИНА",
            _titleStyle!);
        GUI.Label(
            new Rect(rect.x + 14f * scale, rect.y + 26f * scale, 42f * scale, 37f * scale),
            "★",
            _starStyle!);
        var value = _averageRating.ToString("0.00", CultureInfo.CurrentCulture);
        GUI.Label(
            new Rect(rect.x + 53f * scale, rect.y + 27f * scale, 122f * scale, 34f * scale),
            $"{value} / 5",
            _ratingStyle!);
        GUI.Label(
            new Rect(rect.x + 169f * scale, rect.y + 34f * scale, 68f * scale, 22f * scale),
            _ratingCount == 1 ? "1 отзыв" : $"{_ratingCount} отз.",
            _titleStyle!);
    }

    private static float ResolveRatingTop(GameView? gameView, float scale)
    {
        var top = 138f * scale;
        try
        {
            var quest = gameView?._questPanel;
            if (quest == null || !quest.gameObject.activeInHierarchy)
                return top;

            quest.GetWorldCorners(QuestCorners);
            var questId = quest.GetInstanceID();
            if (_questCanvas == null || !_questCanvas || _questPanelInstanceId != questId)
            {
                _questCanvas = quest.GetComponentInParent<Canvas>();
                _questPanelInstanceId = questId;
            }
            var canvas = _questCanvas;
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            var minX = float.MaxValue;
            var minY = float.MaxValue;
            var maxX = float.MinValue;
            var maxY = float.MinValue;
            foreach (var corner in QuestCorners)
            {
                var screen = RectTransformUtility.WorldToScreenPoint(camera, corner);
                minX = Math.Min(minX, screen.x);
                minY = Math.Min(minY, screen.y);
                maxX = Math.Max(maxX, screen.x);
                maxY = Math.Max(maxY, screen.y);
            }

            var ratingRight = 280f * scale;
            if (maxX >= 0f && minX <= ratingRight)
            {
                var questBottomInGui = Screen.height - minY;
                top = Math.Max(top, questBottomInGui + 12f * scale);
            }
        }
        catch
        {
            // A quest can disappear while Unity is repainting the HUD. The normal
            // position remains a safe fallback for that frame.
        }

        return Math.Min(top, Screen.height - 84f * scale);
    }

    private static void DrawNotifications(float scale)
    {
        var now = Time.unscaledTime;
        Notifications.RemoveAll(item => item.ExpiresAt <= now);
        if (Notifications.Count == 0)
            return;

        var width = 330f * scale;
        var height = 52f * scale;
        var gap = 8f * scale;
        var x = 30f * scale;
        var baseY = Screen.height - 150f * scale - height;
        var updatePosition = Event.current == null || Event.current.type == EventType.Repaint;
        var smoothing = 1f - Mathf.Exp(-16f * Math.Max(0.001f, Time.unscaledDeltaTime));

        for (var index = 0; index < Notifications.Count; index++)
        {
            var notification = Notifications[index];
            var targetY = baseY - index * (height + gap);
            if (float.IsNaN(notification.CurrentY))
                notification.CurrentY = targetY + 34f * scale;
            if (updatePosition)
                notification.CurrentY = Mathf.Lerp(notification.CurrentY, targetY, smoothing);

            var fadeStartsAt = notification.ExpiresAt - NotificationFadeDuration;
            var alpha = now <= fadeStartsAt
                ? 1f
                : Mathf.Clamp01((notification.ExpiresAt - now) / NotificationFadeDuration);
            var oldColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            var rect = new Rect(x, notification.CurrentY, width, height);
            GUI.Box(rect, GUIContent.none, _panelStyle!);

            var suffix = notification.Minimal && notification.Count > 1
                ? $"  ×{notification.Count}"
                : string.Empty;
            GUI.Label(
                new Rect(rect.x + 16f * scale, rect.y + 8f * scale,
                    notification.Minimal ? rect.width - 32f * scale : rect.width - 106f * scale,
                    rect.height - 16f * scale),
                "Оставлен новый отзыв" + suffix,
                _notificationTextStyle!);
            if (!notification.Minimal)
            {
                GUI.Label(
                    new Rect(rect.x + rect.width - 94f * scale, rect.y + 8f * scale,
                        78f * scale, rect.height - 16f * scale),
                    $"★ {notification.Stars}/5",
                    _notificationRatingStyle!);
            }
            GUI.color = oldColor;
        }
    }

    private static void EnsureStyles(float scale)
    {
        if (_panelStyle != null && Math.Abs(_styleScale - scale) < 0.01f)
            return;

        _styleScale = scale;
        _panelTexture ??= CreateRoundedTexture(
            "WolfMod_ReviewsHudPanel",
            new Color(0.018f, 0.035f, 0.055f, 0.88f),
            new Color(0.10f, 0.48f, 0.67f, 0.95f));

        _panelStyle = new GUIStyle(GUI.skin.box)
        {
            border = new RectOffset(12, 12, 12, 12),
            padding = new RectOffset(0, 0, 0, 0)
        };
        _panelStyle.normal.background = _panelTexture;

        _titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = Math.Max(10, (int)Math.Round(12f * scale)),
            fontStyle = FontStyle.Bold,
            clipping = TextClipping.Clip
        };
        _titleStyle.normal.textColor = new Color(0.76f, 0.86f, 0.93f, 1f);

        _ratingStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = Math.Max(17, (int)Math.Round(24f * scale)),
            fontStyle = FontStyle.Bold,
            clipping = TextClipping.Clip
        };
        _ratingStyle.normal.textColor = Color.white;

        _starStyle = new GUIStyle(_ratingStyle)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = Math.Max(23, (int)Math.Round(32f * scale))
        };
        _starStyle.normal.textColor = new Color(1f, 0.67f, 0.06f, 1f);

        _notificationTextStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = Math.Max(13, (int)Math.Round(17f * scale)),
            fontStyle = FontStyle.Bold,
            clipping = TextClipping.Clip
        };
        _notificationTextStyle.normal.textColor = Color.white;

        _notificationRatingStyle = new GUIStyle(_notificationTextStyle)
        {
            alignment = TextAnchor.MiddleRight
        };
        _notificationRatingStyle.normal.textColor = new Color(1f, 0.72f, 0.12f, 1f);
    }

    private static Texture2D CreateRoundedTexture(string name, Color fill, Color border)
    {
        const int size = 64;
        const int borderWidth = 2;
        const float outerRadius = 11f;
        const float innerRadius = 9f;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = name,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var px = x + 0.5f;
                var py = y + 0.5f;
                if (!InsideRoundedRect(px, py, 0f, size, outerRadius))
                {
                    texture.SetPixel(x, y, Color.clear);
                    continue;
                }

                var inside = InsideRoundedRect(
                    px,
                    py,
                    borderWidth,
                    size - borderWidth,
                    innerRadius);
                texture.SetPixel(x, y, inside ? fill : border);
            }
        }
        texture.Apply();
        return texture;
    }

    private static bool InsideRoundedRect(float x, float y, float minimum, float maximum, float radius)
    {
        var nearestX = Math.Clamp(x, minimum + radius, maximum - radius);
        var nearestY = Math.Clamp(y, minimum + radius, maximum - radius);
        var dx = x - nearestX;
        var dy = y - nearestY;
        return dx * dx + dy * dy <= radius * radius;
    }
}
