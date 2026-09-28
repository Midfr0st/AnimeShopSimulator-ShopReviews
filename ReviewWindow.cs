using System.Globalization;
using Il2CppProject.Code.Gameplay.UI.Computer;
using Il2CppTMPro;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;

namespace WolfShopReviews;

internal static class ReviewWindow
{
    private const string RootName = "WolfMod_ShopReviewsSection";
    private const int VisibleReviewCount = 4;

    private static readonly Color PageColor = new(0.955f, 0.958f, 0.985f, 1f);
    private static readonly Color Navy = new(0.16f, 0.18f, 0.34f, 1f);
    private static readonly Color Muted = new(0.38f, 0.40f, 0.52f, 1f);
    private static readonly Color Gold = new(1.00f, 0.68f, 0.08f, 1f);
    private static readonly Color EmptyStar = new(0.73f, 0.74f, 0.79f, 1f);
    private static readonly Color RowA = new(0.90f, 0.91f, 0.94f, 1f);
    private static readonly Color RowB = new(0.94f, 0.945f, 0.965f, 1f);
    private static readonly Color RatingPanel = new(0.88f, 0.91f, 0.96f, 1f);

    private static ComputerWorldView? _view;
    private static GameObject? _panelTemplate;
    private static GameObject? _textTemplate;
    private static GameObject? _buttonTemplate;
    private static GameObject? _root;
    private static Texture2D? _avatarTexture;
    private static Sprite? _avatarSprite;
    private static Texture2D? _starTexture;
    private static Sprite? _starSprite;
    private static int _scrollOffset;
    private static int _reviewCount;
    public static bool IsOpen => _root != null;

    public static void PrepareTemplates(ComputerWorldView view)
    {
        if (_view == view && _textTemplate != null && _buttonTemplate != null)
            return;

        ReleaseTemplates();
        _view = view;
        GameObject? textSource = view._levelText?.gameObject;
        GameObject? buttonSource = null;
        GameObject? panelSource = null;

        foreach (var button in view._content.GetComponentsInChildren<Button>(true))
        {
            if (button == null || button.gameObject == null)
                continue;
            if (buttonSource == null || button.name.Contains("Confirm", StringComparison.OrdinalIgnoreCase))
                buttonSource = button.gameObject;
            if (button.name.Contains("Confirm", StringComparison.OrdinalIgnoreCase))
                break;
        }
        buttonSource ??= view._closeButton?.gameObject;

        foreach (var image in view._content.GetComponentsInChildren<Image>(true))
        {
            if (image != null && image.gameObject != null && image.name == "BG")
            {
                panelSource = image.gameObject;
                break;
            }
        }
        panelSource ??= buttonSource;
        if (textSource == null || buttonSource == null || panelSource == null)
            return;

        _textTemplate = CloneHiddenTemplate(textSource, view._content, "WolfMod_ReviewsTextTemplate");
        _buttonTemplate = CloneHiddenTemplate(buttonSource, view._content, "WolfMod_ReviewsButtonTemplate");
        _panelTemplate = CloneHiddenTemplate(panelSource, view._content, "WolfMod_ReviewsPanelTemplate");
        EnsureGeneratedSprites();
    }

    public static bool Open(ComputerWorldView view)
    {
        Close();
        PrepareTemplates(view);
        try
        {
            if (_textTemplate == null || _buttonTemplate == null)
                throw new InvalidOperationException("в терминале не найдены нативные шаблоны интерфейса");
            _scrollOffset = 0;
            _root = CreateRoot(view);
            _root.SetActive(true);
            Rebuild();
            MelonLogger.Msg("Shop Reviews: нативная страница отзывов открыта внутри терминала.");
            return true;
        }
        catch (Exception exception)
        {
            MelonLogger.Error($"Shop Reviews: страница отзывов не создана: {exception}");
            Close();
            return false;
        }
    }

    public static void Close()
    {
        if (_root != null)
        {
            _root.SetActive(false);
            UnityEngine.Object.Destroy(_root);
        }
        _root = null;
    }

    public static void TickInput()
    {
        if (_root == null || _reviewCount <= VisibleReviewCount)
            return;
        try
        {
            var wheel = Input.mouseScrollDelta.y;
            if (wheel > 0.01f)
                ScrollBy(-1);
            else if (wheel < -0.01f)
                ScrollBy(1);
        }
        catch (Exception exception)
        {
            MelonLogger.Warning($"Shop Reviews: ввод прокрутки недоступен: {exception.Message}");
        }
    }

    private static GameObject CreateRoot(ComputerWorldView view)
    {
        var template = _panelTemplate ?? _buttonTemplate;
        if (template == null)
            throw new InvalidOperationException("не найден фон нативного раздела");
        var root = UnityEngine.Object.Instantiate(template, view._content, false);
        root.name = RootName;
        root.hideFlags = HideFlags.DontSave;
        root.SetActive(false);
        ClearChildren(root.transform);
        DisableInteractive(root, true);
        Stretch(root.GetComponent<RectTransform>()!, Vector2.zero, Vector2.one);
        var image = root.GetComponent<Image>();
        if (image != null)
        {
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.color = PageColor;
            image.raycastTarget = true;
        }
        root.transform.SetAsLastSibling();
        return root;
    }

    private static void Rebuild()
    {
        if (_root == null)
            return;
        ClearChildren(_root.transform);

        var snapshot = ReviewStore.Snapshot();
        var reviews = snapshot.Reviews
            .OrderByDescending(review => review.Day)
            .ThenByDescending(review => review.Id, StringComparer.Ordinal)
            .ToList();
        _reviewCount = reviews.Count;
        _scrollOffset = Math.Clamp(_scrollOffset, 0, Math.Max(0, reviews.Count - VisibleReviewCount));

        var totalRatings = snapshot.RatingCounts.Sum();
        var weightedRatings = snapshot.RatingCounts.Select((count, index) => count * (index + 1L)).Sum();
        var average = totalRatings > 0 ? (double)weightedRatings / totalRatings : 0d;
        var roundedStars = totalRatings > 0 ? Math.Clamp((int)Math.Round(average, MidpointRounding.AwayFromZero), 1, 5) : 0;
        var shopName = GameStateReader.GetShopName();

        CreateText(_root.transform, "Title", "ОТЗЫВЫ ПОКУПАТЕЛЕЙ",
            new Vector2(0.03f, 0.825f), new Vector2(0.60f, 0.89f), 42f,
            FontStyles.Bold, TextAlignmentOptions.MidlineLeft, Navy);
        CreateText(_root.transform, "Subtitle",
            string.IsNullOrWhiteSpace(shopName)
                ? $"Опубликовано отзывов: {totalRatings}"
                : $"Магазин «{shopName}» • опубликовано отзывов: {totalRatings}",
            new Vector2(0.03f, 0.780f), new Vector2(0.64f, 0.825f), 25f,
            FontStyles.Normal, TextAlignmentOptions.MidlineLeft, Muted);

        var ratingPanel = CreatePanel(_root.transform, "RatingSummary", RatingPanel);
        Stretch(ratingPanel.GetComponent<RectTransform>()!, new Vector2(0.65f, 0.79f), new Vector2(0.96f, 0.895f));
        CreateText(ratingPanel.transform, "RatingValue",
            $"РЕЙТИНГ  {FormatRussianDecimal(average)} / 5",
            new Vector2(0.03f, 0.48f), new Vector2(0.97f, 0.94f), 32f,
            FontStyles.Bold, TextAlignmentOptions.Center, Navy);
        CreateStars(ratingPanel.transform, roundedStars,
            new Vector2(0.25f, 0.06f), new Vector2(0.75f, 0.49f));

        if (reviews.Count == 0)
        {
            CreateText(_root.transform, "Empty", "Покупатели пока не оставили ни одного отзыва.",
                new Vector2(0.08f, 0.35f), new Vector2(0.92f, 0.60f), 36f,
                FontStyles.Bold, TextAlignmentOptions.Center, Muted);
            return;
        }

        var visibleCount = Math.Min(VisibleReviewCount, reviews.Count - _scrollOffset);
        for (var index = 0; index < visibleCount; index++)
        {
            var reviewIndex = _scrollOffset + index;
            var top = 0.755f - index * 0.158f;
            CreateReviewCard(reviews[reviewIndex], reviewIndex, top - 0.150f, top);
        }
        CreateScrollIndicator(reviews.Count);
    }

    private static void CreateReviewCard(ReviewRecord review, int index, float bottom, float top)
    {
        if (_root == null)
            return;
        var card = CreatePanel(_root.transform, $"Review_{review.Id}", index % 2 == 0 ? RowA : RowB);
        Stretch(card.GetComponent<RectTransform>()!, new Vector2(0.03f, bottom), new Vector2(0.955f, top));

        CreateSprite(card.transform, "Avatar", _avatarSprite, new Vector2(0.016f, 0.12f), new Vector2(0.092f, 0.88f), Color.white);
        CreateText(card.transform, "Name", review.ReviewerName,
            new Vector2(0.105f, 0.64f), new Vector2(0.47f, 0.94f), 30f,
            FontStyles.Bold, TextAlignmentOptions.MidlineLeft, Navy);
        CreateText(card.transform, "Day", $"День {review.Day}",
            new Vector2(0.105f, 0.43f), new Vector2(0.32f, 0.64f), 22f,
            FontStyles.Normal, TextAlignmentOptions.MidlineLeft, Muted);
        CreateStars(card.transform, Math.Clamp(review.Stars, 1, 5),
            new Vector2(0.76f, 0.60f), new Vector2(0.96f, 0.91f));
        CreateText(card.transform, "ReviewText", review.Text,
            new Vector2(0.105f, 0.035f), new Vector2(0.96f, 0.43f), 25f,
            FontStyles.Normal, TextAlignmentOptions.TopLeft, Navy);
    }

    private static void CreateScrollIndicator(int count)
    {
        if (_root == null || count <= VisibleReviewCount)
            return;
        const float bottom = 0.12f;
        const float top = 0.755f;
        var track = CreatePanel(_root.transform, "ReviewScrollTrack", EmptyStar);
        Stretch(track.GetComponent<RectTransform>()!, new Vector2(0.966f, bottom), new Vector2(0.974f, top));

        var maximumOffset = count - VisibleReviewCount;
        var height = Math.Max(0.075f, (top - bottom) * VisibleReviewCount / count);
        var normalized = (float)_scrollOffset / maximumOffset;
        var handleTop = top - normalized * ((top - bottom) - height);
        var handle = CreatePanel(_root.transform, "ReviewScrollHandle", Navy);
        Stretch(handle.GetComponent<RectTransform>()!,
            new Vector2(0.965f, handleTop - height), new Vector2(0.975f, handleTop));
    }

    private static void ScrollBy(int amount)
    {
        var maximum = Math.Max(0, _reviewCount - VisibleReviewCount);
        var next = Math.Clamp(_scrollOffset + amount, 0, maximum);
        if (next == _scrollOffset)
            return;
        _scrollOffset = next;
        Rebuild();
    }

    private static string FormatRussianDecimal(double value) =>
        value.ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ',');

    private static GameObject CreatePanel(Transform parent, string name, Color color)
    {
        if (_buttonTemplate == null)
            throw new InvalidOperationException("шаблон панели отсутствует");
        var panel = UnityEngine.Object.Instantiate(_buttonTemplate, parent, false);
        panel.name = name;
        panel.SetActive(false);
        ClearChildren(panel.transform);
        DisableInteractive(panel, true);
        var image = panel.GetComponent<Image>();
        if (image != null)
        {
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;
        }
        panel.SetActive(true);
        return panel;
    }

    private static TextMeshProUGUI CreateText(
        Transform parent,
        string name,
        string value,
        Vector2 anchorMin,
        Vector2 anchorMax,
        float fontSize,
        FontStyles style,
        TextAlignmentOptions alignment,
        Color color)
    {
        if (_textTemplate == null)
            throw new InvalidOperationException("шаблон текста отсутствует");
        var textObject = UnityEngine.Object.Instantiate(_textTemplate, parent, false);
        textObject.name = name;
        textObject.SetActive(false);
        ClearChildren(textObject.transform);
        var rect = textObject.GetComponent<RectTransform>()!;
        Stretch(rect, anchorMin, anchorMax, new Vector2(3f, 2f), new Vector2(-3f, -2f));
        var text = textObject.GetComponent<TextMeshProUGUI>()!;
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.enableAutoSizing = false;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        textObject.SetActive(true);
        return text;
    }

    private static void CreateStars(Transform parent, int filled, Vector2 anchorMin, Vector2 anchorMax)
    {
        if (_starSprite == null)
            return;
        var width = (anchorMax.x - anchorMin.x) / 5f;
        for (var index = 0; index < 5; index++)
        {
            var left = anchorMin.x + index * width;
            CreateSprite(parent, $"Star_{index}", _starSprite,
                new Vector2(left, anchorMin.y), new Vector2(left + width, anchorMax.y),
                index < filled ? Gold : EmptyStar);
        }
    }

    private static void CreateSprite(
        Transform parent,
        string name,
        Sprite? sprite,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Color color)
    {
        if (_buttonTemplate == null || sprite == null)
            return;
        var icon = UnityEngine.Object.Instantiate(_buttonTemplate, parent, false);
        icon.name = name;
        icon.SetActive(false);
        ClearChildren(icon.transform);
        DisableInteractive(icon, true);
        Stretch(icon.GetComponent<RectTransform>()!, anchorMin, anchorMax, new Vector2(2f, 2f), new Vector2(-2f, -2f));
        var image = icon.GetComponent<Image>();
        if (image != null)
        {
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = color;
            image.raycastTarget = false;
        }
        icon.SetActive(true);
    }

    private static void EnsureGeneratedSprites()
    {
        _avatarSprite ??= GenerateAvatarSprite();
        _starSprite ??= GenerateStarSprite();
    }

    private static Sprite GenerateAvatarSprite()
    {
        const int size = 96;
        var texture = NewTexture("WolfMod_ReviewsAvatarTexture", size);
        var background = new Color(0.70f, 0.83f, 0.94f, 1f);
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var nx = ((x + 0.5f) / size) * 2f - 1f;
                var ny = ((y + 0.5f) / size) * 2f - 1f;
                var color = nx * nx + ny * ny <= 0.94f * 0.94f ? background : Color.clear;
                var head = nx * nx + (ny - 0.28f) * (ny - 0.28f) <= 0.20f * 0.20f;
                var shoulders = nx * nx / (0.48f * 0.48f) + (ny + 0.39f) * (ny + 0.39f) / (0.34f * 0.34f) <= 1f;
                if (head || shoulders)
                    color = Navy;
                texture.SetPixel(x, y, color);
            }
        texture.Apply(false, false);
        _avatarTexture = texture;
        return MakeSprite(texture, "WolfMod_ReviewsAvatarSprite");
    }

    private static Sprite GenerateStarSprite()
    {
        const int size = 96;
        var texture = NewTexture("WolfMod_ReviewsStarTexture", size);
        var points = new Vector2[10];
        for (var index = 0; index < points.Length; index++)
        {
            var angle = MathF.PI / 2f + index * MathF.PI / 5f;
            var radius = index % 2 == 0 ? 0.44f : 0.20f;
            points[index] = new Vector2(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius);
        }
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var point = new Vector2((x + 0.5f) / size - 0.5f, (y + 0.5f) / size - 0.5f);
                texture.SetPixel(x, y, IsInsidePolygon(point, points) ? Color.white : Color.clear);
            }
        texture.Apply(false, false);
        _starTexture = texture;
        return MakeSprite(texture, "WolfMod_ReviewsStarSprite");
    }

    private static bool IsInsidePolygon(Vector2 point, IReadOnlyList<Vector2> polygon)
    {
        var inside = false;
        for (int current = 0, previous = polygon.Count - 1; current < polygon.Count; previous = current++)
        {
            var a = polygon[current];
            var b = polygon[previous];
            if ((a.y > point.y) != (b.y > point.y) &&
                point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                inside = !inside;
        }
        return inside;
    }

    private static Texture2D NewTexture(string name, int size) => new(size, size, TextureFormat.RGBA32, false)
    {
        name = name,
        hideFlags = HideFlags.DontSave,
        filterMode = FilterMode.Bilinear,
        wrapMode = TextureWrapMode.Clamp
    };

    private static Sprite MakeSprite(Texture2D texture, string name)
    {
        var sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = name;
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }

    private static void DisableInteractive(GameObject gameObject, bool keepImage)
    {
        var button = gameObject.GetComponent<Button>();
        if (button != null)
        {
            button.onClick = new Button.ButtonClickedEvent();
            button.interactable = false;
            button.enabled = false;
        }
        var image = gameObject.GetComponent<Image>();
        if (image != null && !keepImage)
            image.enabled = false;
    }

    private static void ClearChildren(Transform parent)
    {
        for (var index = parent.childCount - 1; index >= 0; index--)
        {
            var child = parent.GetChild(index);
            if (child == null || child.gameObject == null)
                continue;
            child.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(child.gameObject);
        }
    }

    private static GameObject CloneHiddenTemplate(GameObject source, Transform parent, string name)
    {
        var clone = UnityEngine.Object.Instantiate(source, parent, false);
        clone.name = name;
        clone.hideFlags = HideFlags.DontSave;
        clone.SetActive(false);
        return clone;
    }

    public static void ReleaseTemplates()
    {
        Close();
        foreach (var template in new[] { _panelTemplate, _textTemplate, _buttonTemplate })
        {
            if (template == null)
                continue;
            template.SetActive(false);
            UnityEngine.Object.Destroy(template);
        }
        _panelTemplate = null;
        _textTemplate = null;
        _buttonTemplate = null;
        _view = null;
        if (_avatarSprite != null)
            UnityEngine.Object.Destroy(_avatarSprite);
        if (_avatarTexture != null)
            UnityEngine.Object.Destroy(_avatarTexture);
        if (_starSprite != null)
            UnityEngine.Object.Destroy(_starSprite);
        if (_starTexture != null)
            UnityEngine.Object.Destroy(_starTexture);
        _avatarSprite = null;
        _avatarTexture = null;
        _starSprite = null;
        _starTexture = null;
    }

    private static void Stretch(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2? offsetMin = null,
        Vector2? offsetMax = null)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = offsetMin ?? Vector2.zero;
        rect.offsetMax = offsetMax ?? Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }
}
