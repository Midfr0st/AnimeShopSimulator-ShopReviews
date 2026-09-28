using Il2CppProject.Code.Gameplay.AI.Buyer;
using Il2CppProject.Code.Gameplay.Controllers;
using MelonLoader;
using UnityEngine;

namespace WolfShopReviews;

internal sealed class VisitContext
{
    public int BuyerId { get; init; }
    public int Seed { get; init; }
    public int GenderIndex { get; set; } = -1;
    public float VisitStartedAt { get; init; }
    public float QueueStartedAt { get; set; } = -1f;
    public float QueueSeconds { get; set; } = -1f;
    public bool CheckoutCompleted { get; set; }
    public bool MissingStock { get; set; }
    public bool CleanlinessComplaint { get; set; }
    public bool Overcrowded { get; set; }
    public bool AgeCheckSeen { get; set; }
    public bool AgeCheckCorrect { get; set; }
    public bool AgeCheckCompleted { get; set; }
    public bool MangaScenario { get; set; }
    public bool MangaSuccess { get; set; }
    public bool CourierScenario { get; set; }
    public bool CourierSuccess { get; set; }
    public BuyerLeaveReason LastReason { get; set; } = BuyerLeaveReason.Unknown;
    public bool Finalized { get; set; }
    public Dictionary<ReviewFactor, float> Preferences { get; } = new();
}

internal static class ReviewRuntime
{
    private static readonly Dictionary<int, VisitContext> Visits = new();
    private static readonly Dictionary<int, (int BuyerId, bool WrongId)> PendingAgeChecks = new();
    private static readonly Dictionary<int, float> RecentlyFinalized = new();
    private static int _serial;
    private static DirtController? _dirtController;

    public static void Reset()
    {
        Visits.Clear();
        PendingAgeChecks.Clear();
        RecentlyFinalized.Clear();
        _dirtController = null;
        GameStateReader.ResetSceneCache();
    }

    public static void Begin(Buyer? buyer)
    {
        if (!WolfShopReviewsMod.FeatureEnabled || buyer == null)
            return;

        try
        {
            if ((_serial & 63) == 0)
            {
                foreach (var staleId in RecentlyFinalized
                             .Where(pair => Time.unscaledTime - pair.Value >= 30f)
                             .Select(pair => pair.Key)
                             .ToArray())
                    RecentlyFinalized.Remove(staleId);
            }

            var id = buyer.GetInstanceID();
            RecentlyFinalized.Remove(id);
            var day = GameStateReader.GetCurrentDay();
            var seed = HashCode.Combine(id, day, Environment.TickCount, ++_serial);
            var context = new VisitContext
            {
                BuyerId = id,
                Seed = seed,
                GenderIndex = ReadGenderIndex(buyer),
                VisitStartedAt = Time.unscaledTime
            };
            FillPreferences(context, seed);
            Visits[id] = context;
        }
        catch (Exception exception)
        {
            LogDiagnostic($"не удалось начать наблюдение за покупателем: {exception.Message}");
        }
    }

    public static void QueueStarted(Buyer? buyer)
    {
        var context = Ensure(buyer);
        if (context != null && context.QueueStartedAt < 0f)
            context.QueueStartedAt = Time.unscaledTime;
    }

    public static void CheckoutCompleted(Buyer? buyer)
    {
        var context = Ensure(buyer);
        if (context == null)
            return;
        context.CheckoutCompleted = true;
        if (context.QueueStartedAt >= 0f)
            context.QueueSeconds = Math.Max(0f, Time.unscaledTime - context.QueueStartedAt);
    }

    public static void MarkMissingStock(Buyer? buyer)
    {
        var context = Ensure(buyer);
        if (context != null)
            context.MissingStock = true;
    }

    public static void MarkCleanliness(Buyer? buyer)
    {
        var context = Ensure(buyer);
        if (context != null)
            context.CleanlinessComplaint = true;
    }

    public static void MarkOvercrowded(Buyer? buyer)
    {
        var context = Ensure(buyer);
        if (context != null)
            context.Overcrowded = true;
    }

    public static void RecordReason(Buyer? buyer, BuyerLeaveReason reason)
    {
        var context = Ensure(buyer);
        if (context != null)
            context.LastReason = reason;
    }

    public static void MarkManga(Buyer? buyer, bool success)
    {
        var context = Ensure(buyer);
        if (context == null)
            return;
        context.MangaScenario = true;
        context.MangaSuccess = success;
    }

    public static void MarkCourier(Buyer? buyer, bool success)
    {
        var context = Ensure(buyer);
        if (context == null)
            return;
        context.CourierScenario = true;
        context.CourierSuccess = success;
    }

    public static void BeginAgeCheck(int registerId, Buyer? buyer, bool wrongId)
    {
        var context = Ensure(buyer);
        if (context == null)
            return;
        context.AgeCheckSeen = true;
        PendingAgeChecks[registerId] = (context.BuyerId, wrongId);
    }

    public static void CompleteAgeCheck(int registerId, bool confirmed)
    {
        if (!PendingAgeChecks.Remove(registerId, out var pending) ||
            !Visits.TryGetValue(pending.BuyerId, out var context))
            return;
        context.AgeCheckCompleted = true;
        context.AgeCheckCorrect = confirmed != pending.WrongId;
    }

    public static void FinalizeVisit(Buyer? buyer, bool boughtSomething, BuyerLeaveReason leaveReason)
    {
        if (!WolfShopReviewsMod.FeatureEnabled || buyer == null)
            return;

        VisitContext? context = null;
        try
        {
            var buyerId = buyer.GetInstanceID();
            if (RecentlyFinalized.TryGetValue(buyerId, out var finalizedAt) &&
                Time.unscaledTime - finalizedAt < 15f)
                return;
            context = Ensure(buyer);
            if (context == null || context.Finalized)
                return;
            context.Finalized = true;
            RecentlyFinalized[context.BuyerId] = Time.unscaledTime;
            context.LastReason = leaveReason;
            if (context.QueueStartedAt >= 0f && context.QueueSeconds < 0f)
                context.QueueSeconds = Math.Max(0f, Time.unscaledTime - context.QueueStartedAt);

            if (leaveReason is BuyerLeaveReason.DayEnded or BuyerLeaveReason.RestoreCleanup)
            {
                if (ReviewSettings.Value.DiagnosticLogging)
                    LogDiagnostic($"NPC {context.BuyerId}: системный уход ({leaveReason}), отзыв не создаётся.");
                return;
            }

            var observations = Evaluate(context, boughtSomething, leaveReason);
            var score = CalculateStars(context, observations, boughtSomething);
            var severe = observations.Any(item => item.Tone <= -2 && item.Importance >= 1.1f);
            var chance = ResolveChance(score, severe);
            var random = new System.Random(context.Seed ^ 0x2B5A7D13);
            var roll = random.NextDouble();
            if (ReviewSettings.Value.DiagnosticLogging)
            {
                LogDiagnostic(
                    $"NPC {context.BuyerId}: {score}★, шанс {chance:P1}, бросок {roll:P1}, " +
                    $"очередь {context.QueueSeconds:0.0} с, причина {leaveReason}.");
            }
            if (roll >= chance)
                return;

            var day = GameStateReader.GetCurrentDay();
            var shopName = GameStateReader.GetShopName();
            var generated = ReviewTextGenerator.Generate(new ReviewDraft
            {
                Seed = context.Seed,
                Stars = score,
                GenderIndex = context.GenderIndex,
                ShopName = shopName,
                Observations = observations
            });
            var stored = ReviewStore.Add(new ReviewRecord
            {
                Day = day,
                ReviewerName = generated.ReviewerName,
                Stars = score,
                Text = generated.Text,
                ShopName = shopName,
                Factors = generated.Factors
            });
            if (stored)
                ReviewHud.OnReviewPublished(score);
            MelonLogger.Msg($"Shop Reviews: новый отзыв — {generated.ReviewerName}, день {day}, {score}★: {generated.Text}");
        }
        catch (Exception exception)
        {
            MelonLogger.Error($"Shop Reviews: ошибка завершения визита: {exception}");
        }
        finally
        {
            if (context != null)
            {
                Visits.Remove(context.BuyerId);
                foreach (var registerId in PendingAgeChecks
                             .Where(pair => pair.Value.BuyerId == context.BuyerId)
                             .Select(pair => pair.Key)
                             .ToArray())
                    PendingAgeChecks.Remove(registerId);
            }
        }
    }

    private static VisitContext? Ensure(Buyer? buyer)
    {
        if (!WolfShopReviewsMod.FeatureEnabled || buyer == null)
            return null;
        var id = buyer.GetInstanceID();
        if (Visits.TryGetValue(id, out var context))
            return context;
        Begin(buyer);
        return Visits.TryGetValue(id, out context) ? context : null;
    }

    private static List<ReviewObservation> Evaluate(
        VisitContext context,
        bool boughtSomething,
        BuyerLeaveReason leaveReason)
    {
        var result = new List<ReviewObservation>();

        if (context.CleanlinessComplaint)
            Add(result, context, ReviewFactor.Cleanliness, -2, 1.35f);
        else
        {
            var cleanliness = ReadCleanliness();
            if (cleanliness >= 0f)
            {
                if (cleanliness >= 0.92f)
                    Add(result, context, ReviewFactor.Cleanliness, 2, 0.50f);
                else if (cleanliness >= 0.72f)
                    Add(result, context, ReviewFactor.Cleanliness, 1, 0.35f);
                else if (cleanliness < 0.28f)
                    Add(result, context, ReviewFactor.Cleanliness, -2, 1.18f);
                else if (cleanliness < 0.48f)
                    Add(result, context, ReviewFactor.Cleanliness, -1, 0.84f);
            }
        }

        if (context.QueueSeconds >= 0f)
        {
            if (context.QueueSeconds <= 8f)
                Add(result, context, ReviewFactor.Queue, 2, 0.35f);
            else if (context.QueueSeconds <= 30f)
                Add(result, context, ReviewFactor.Queue, 1, 0.28f);
            else if (context.QueueSeconds >= 120f)
                Add(result, context, ReviewFactor.Queue, -2, 1.25f);
            else if (context.QueueSeconds >= 60f)
                Add(result, context, ReviewFactor.Queue, -1, 0.85f);
        }

        if (context.MissingStock || leaveReason == BuyerLeaveReason.PlannedProductUnavailable)
            Add(result, context, ReviewFactor.Stock, -2, 1.45f);
        else if (boughtSomething)
            Add(result, context, ReviewFactor.Stock, 1, 0.38f);

        if (leaveReason == BuyerLeaveReason.PriceRejected)
            Add(result, context, ReviewFactor.Price, -2, 1.25f);
        else if (leaveReason == BuyerLeaveReason.BudgetTooLow)
            Add(result, context, ReviewFactor.Price, -1, 0.72f);
        else if (boughtSomething)
            Add(result, context, ReviewFactor.Price, 1, 0.28f);

        if (context.AgeCheckSeen)
        {
            if (context.AgeCheckCompleted && context.AgeCheckCorrect)
                Add(result, context, ReviewFactor.AgeCheck, 1, 0.50f);
            else
                Add(result, context, ReviewFactor.AgeCheck, -2, 1.45f);
        }

        if (context.MangaScenario)
            Add(result, context, ReviewFactor.MangaRequest, context.MangaSuccess ? 2 : -2, context.MangaSuccess ? 0.9f : 1.4f);
        if (context.CourierScenario)
            Add(result, context, ReviewFactor.CourierOrder, context.CourierSuccess ? 2 : -2, context.CourierSuccess ? 0.9f : 1.4f);
        if (context.Overcrowded)
            Add(result, context, ReviewFactor.Crowding, -2, 1.25f);

        if (context.CheckoutCompleted || boughtSomething)
            Add(result, context, ReviewFactor.Service, 1, 0.35f);
        else if (leaveReason is not BuyerLeaveReason.Unknown and not BuyerLeaveReason.SpecialBehavior)
            Add(result, context, ReviewFactor.Service, -1, 0.46f);

        if (result.Count == 0)
            Add(result, context, ReviewFactor.General, boughtSomething ? 1 : 0, 0.55f);
        return result;
    }

    private static int CalculateStars(
        VisitContext context,
        IReadOnlyList<ReviewObservation> observations,
        bool boughtSomething)
    {
        // An ordinary successful visit should land around four stars. Five stars
        // are reserved for several genuinely strong impressions, while one real
        // failure must not be drowned out by routine positives such as "the buyer
        // paid" or "the shop was reasonably clean".
        var score = 3.00f + (boughtSomething ? 0.18f : -0.12f);
        foreach (var observation in observations)
        {
            var perTone = observation.Tone > 0 ? 0.25f : 0.62f;
            score += observation.Tone * perTone * observation.Importance;
        }

        var noise = (float)(new System.Random(context.Seed ^ 0x6D1F3A27).NextDouble() * 0.54 - 0.27);
        score += noise;

        var hasSevereProblem = observations.Any(item => item.Tone <= -2 && item.Importance >= 0.8f);
        var hasProblem = observations.Any(item => item.Tone < 0 && item.Importance >= 0.45f);
        if (hasSevereProblem)
            score = Math.Min(score, 2.45f);
        else if (hasProblem)
            score = Math.Min(score, 3.45f);

        return Math.Clamp((int)MathF.Round(score, MidpointRounding.AwayFromZero), 1, 5);
    }

    private static float ResolveChance(int stars, bool severe)
    {
        if (stars >= 4)
            return ReviewSettings.Value.PositiveReviewChance;
        if (stars == 3)
            return ReviewSettings.Value.NeutralReviewChance;
        return severe
            ? ReviewSettings.Value.SevereNegativeReviewChance
            : ReviewSettings.Value.NegativeReviewChance;
    }

    private static void Add(
        ICollection<ReviewObservation> target,
        VisitContext context,
        ReviewFactor factor,
        int tone,
        float importance)
    {
        var preference = context.Preferences.TryGetValue(factor, out var value) ? value : 1f;
        target.Add(new ReviewObservation
        {
            Factor = factor,
            Tone = Math.Clamp(tone, -2, 2),
            Importance = importance * preference
        });
    }

    private static void FillPreferences(VisitContext context, int seed)
    {
        var random = new System.Random(seed ^ 0x46A2195B);
        foreach (var factor in Enum.GetValues<ReviewFactor>())
        {
            var baseValue = 0.68f + (float)random.NextDouble() * 0.76f;
            if (random.NextDouble() < 0.14)
                baseValue *= 0.42f;
            else if (random.NextDouble() < 0.16)
                baseValue *= 1.38f;
            context.Preferences[factor] = Math.Clamp(baseValue, 0.25f, 1.8f);
        }
    }

    private static int ReadGenderIndex(Buyer buyer)
    {
        try
        {
            return buyer.IsMan ? 0 : 1;
        }
        catch
        {
            return -1;
        }
    }

    private static float ReadCleanliness()
    {
        try
        {
            if (_dirtController == null || !_dirtController)
                _dirtController = UnityEngine.Object.FindObjectOfType<DirtController>();
            if (_dirtController == null)
                return -1f;
            var value = _dirtController.Cleanliness;
            if (value > 1.01f)
                value /= 100f;
            return Math.Clamp(value, 0f, 1f);
        }
        catch
        {
            return -1f;
        }
    }

    private static void LogDiagnostic(string message)
    {
        if (ReviewSettings.Value.DiagnosticLogging)
            MelonLogger.Msg($"Shop Reviews [диагностика]: {message}");
    }
}
