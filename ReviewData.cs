using Il2CppProject.Code.Core.Saves;
using Il2CppProject.Code.Core.Services;
using Il2CppProject.Code.Gameplay.Controllers;
using MelonLoader;
using MelonLoader.Utils;
using Newtonsoft.Json;
using UnityEngine;

namespace WolfShopReviews;

internal sealed class ReviewRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public int Day { get; set; }
    public string ReviewerName { get; set; } = string.Empty;
    public int Stars { get; set; }
    public string Text { get; set; } = string.Empty;
    public string ShopName { get; set; } = string.Empty;
    public List<ReviewFactor> Factors { get; set; } = new();
}

internal sealed class SlotReviewData
{
    public List<ReviewRecord> Reviews { get; set; } = new();
    public long TotalPublished { get; set; }
    public long[] RatingCounts { get; set; } = new long[5];
}

internal sealed class ReviewDatabase
{
    public int Version { get; set; } = 1;
    public Dictionary<string, SlotReviewData> Slots { get; set; } = new(StringComparer.Ordinal);
}

internal sealed class ReviewSettingsData
{
    public int SettingsVersion { get; set; }
    public float PositiveReviewChance { get; set; } = 0.04f;
    public float NeutralReviewChance { get; set; } = 0.025f;
    public float NegativeReviewChance { get; set; } = 0.12f;
    public float SevereNegativeReviewChance { get; set; } = 0.20f;
    public int MaximumStoredReviews { get; set; } = 300;
    public bool DiagnosticLogging { get; set; }
    public bool ShowHudRating { get; set; } = true;
    public bool ShowReviewNotifications { get; set; } = true;
    public bool MinimalReviewNotifications { get; set; }
}

internal static class ReviewSettings
{
    private static string _path = string.Empty;
    public static ReviewSettingsData Value { get; private set; } = new();

    public static void Initialize()
    {
        _path = Path.Combine(MelonEnvironment.UserDataDirectory, "WolfShopReviews.settings.json");
        if (!File.Exists(_path))
        {
            Value.SettingsVersion = 2;
            Save();
            return;
        }

        try
        {
            Value = JsonConvert.DeserializeObject<ReviewSettingsData>(File.ReadAllText(_path)) ?? new ReviewSettingsData();
            var migrated = Value.SettingsVersion < 2;
            if (migrated)
            {
                // Detailed per-customer logging was enabled by default in the first
                // public build and can cause synchronous console/file I/O in a busy shop.
                // Keep it as an opt-in diagnostic switch from now on.
                Value.SettingsVersion = 2;
                Value.DiagnosticLogging = false;
            }
            Normalize();
            if (migrated)
                Save();
        }
        catch (Exception exception)
        {
            MelonLogger.Warning($"Shop Reviews: настройки не прочитаны, используются стандартные: {exception.Message}");
            Value = new ReviewSettingsData();
        }
    }

    public static void Save()
    {
        try
        {
            Normalize();
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temporaryPath = _path + ".tmp";
            File.WriteAllText(temporaryPath, JsonConvert.SerializeObject(Value, Formatting.Indented));
            File.Move(temporaryPath, _path, true);
        }
        catch (Exception exception)
        {
            MelonLogger.Error($"Shop Reviews: настройки не сохранены: {exception}");
        }
    }

    private static void Normalize()
    {
        Value.PositiveReviewChance = Math.Clamp(Value.PositiveReviewChance, 0f, 1f);
        Value.NeutralReviewChance = Math.Clamp(Value.NeutralReviewChance, 0f, 1f);
        Value.NegativeReviewChance = Math.Clamp(Value.NegativeReviewChance, 0f, 1f);
        Value.SevereNegativeReviewChance = Math.Clamp(Value.SevereNegativeReviewChance, 0f, 1f);
        Value.MaximumStoredReviews = Math.Clamp(Value.MaximumStoredReviews, 50, 1000);
    }
}

internal static class ReviewStore
{
    private static string _path = string.Empty;
    private static ReviewDatabase _database = new();
    private static bool _canWrite = true;

    public static void Initialize()
    {
        _path = Path.Combine(MelonEnvironment.UserDataDirectory, "WolfShopReviews.json");
        _canWrite = true;
        Reload();
    }

    public static bool Add(ReviewRecord review)
    {
        if (!_canWrite)
            return false;
        var slotKey = GameStateReader.GetSlotKey();
        if (string.IsNullOrWhiteSpace(slotKey))
        {
            MelonLogger.Warning("Shop Reviews: отзыв не записан — активный слот сохранения не определён.");
            return false;
        }

        if (!_database.Slots.TryGetValue(slotKey, out var slot))
        {
            slot = new SlotReviewData();
            _database.Slots[slotKey] = slot;
        }

        slot.Reviews.Add(review);
        slot.TotalPublished++;
        slot.RatingCounts[Math.Clamp(review.Stars, 1, 5) - 1]++;
        var excess = slot.Reviews.Count - ReviewSettings.Value.MaximumStoredReviews;
        if (excess > 0)
            slot.Reviews.RemoveRange(0, excess);
        Save();
        return true;
    }

    public static bool TryGetRatingSummary(out double average, out long totalRatings)
    {
        average = 0d;
        totalRatings = 0L;
        var slotKey = GameStateReader.GetSlotKey();
        if (string.IsNullOrWhiteSpace(slotKey) ||
            !_database.Slots.TryGetValue(slotKey, out var slot) ||
            slot.RatingCounts == null)
        {
            return false;
        }

        long weighted = 0L;
        for (var index = 0; index < Math.Min(5, slot.RatingCounts.Length); index++)
        {
            var count = Math.Max(0L, slot.RatingCounts[index]);
            totalRatings += count;
            weighted += count * (index + 1L);
        }

        if (totalRatings <= 0)
            return false;

        average = (double)weighted / totalRatings;
        return true;
    }

    public static SlotReviewData Snapshot()
    {
        var slotKey = GameStateReader.GetSlotKey();
        if (!_database.Slots.TryGetValue(slotKey, out var slot))
            return new SlotReviewData();
        return new SlotReviewData
        {
            Reviews = slot.Reviews.Select(Clone).ToList(),
            TotalPublished = slot.TotalPublished,
            RatingCounts = slot.RatingCounts.ToArray()
        };
    }

    private static ReviewRecord Clone(ReviewRecord source) => new()
    {
        Id = source.Id,
        Day = source.Day,
        ReviewerName = source.ReviewerName,
        Stars = source.Stars,
        Text = source.Text,
        ShopName = source.ShopName,
        Factors = source.Factors.ToList()
    };

    private static void Reload()
    {
        if (!File.Exists(_path))
        {
            _database = new ReviewDatabase();
            return;
        }

        try
        {
            _database = JsonConvert.DeserializeObject<ReviewDatabase>(File.ReadAllText(_path)) ?? new ReviewDatabase();
            _database.Slots ??= new Dictionary<string, SlotReviewData>(StringComparer.Ordinal);
            foreach (var slot in _database.Slots.Values)
            {
                slot.Reviews ??= new List<ReviewRecord>();
                slot.RatingCounts ??= new long[5];
                if (slot.RatingCounts.Length != 5)
                    slot.RatingCounts = new long[5];
            }
        }
        catch (Exception exception)
        {
            MelonLogger.Error($"Shop Reviews: файл отзывов не прочитан; запись отключена во избежание потери данных: {exception}");
            _database = new ReviewDatabase();
            _canWrite = false;
        }
    }

    private static void Save()
    {
        if (!_canWrite)
            return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temporaryPath = _path + ".tmp";
            File.WriteAllText(temporaryPath, JsonConvert.SerializeObject(_database, Formatting.Indented));
            File.Move(temporaryPath, _path, true);
        }
        catch (Exception exception)
        {
            MelonLogger.Error($"Shop Reviews: данные не сохранены: {exception}");
        }
    }
}

internal static class GameStateReader
{
    private static TimeController? _timeController;

    public static void ResetSceneCache()
    {
        _timeController = null;
    }

    public static string GetSlotKey()
    {
        try
        {
            var saveService = AllServices.Get<SaveService>();
            return saveService != null && saveService.SlotNumberActive >= 0
                ? $"slot_{saveService.SlotNumberActive}"
                : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    public static int GetCurrentDay()
    {
        try
        {
            if (_timeController == null || !_timeController)
                _timeController = UnityEngine.Object.FindObjectOfType<TimeController>();
            if (_timeController != null)
                return Math.Max(1, _timeController.GetCurrentDayNumber());
        }
        catch
        {
            // The controller can disappear during a scene transition.
        }
        return 1;
    }

    public static string GetShopName()
    {
        try
        {
            var saveService = AllServices.Get<SaveService>();
            var text = saveService?.sessionSave?.ShopNameSaveData?.Text;
            return SanitizeShopName(text);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string SanitizeShopName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        var cleaned = string.Join(" ", value.Replace('\r', ' ').Replace('\n', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
        cleaned = cleaned.Replace('«', '"').Replace('»', '"');
        return cleaned.Length <= 60 ? cleaned : cleaned[..60];
    }
}
