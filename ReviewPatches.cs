using HarmonyLib;
using Il2CppFishNet.Connection;
using Il2CppProject.Code.Gameplay.AI.Buyer;
using Il2CppProject.Code.Gameplay.Configs;
using Il2CppProject.Code.Gameplay.Interactions.CashRegister;
using MelonLoader;
using UnityEngine;

namespace WolfShopReviews;

internal static class ReviewPatches
{
    private const string HarmonyId = "wolfmod.shop_reviews";
    private static readonly HarmonyLib.Harmony Harmony = new(HarmonyId);
    private static int _installedCount;

    public static void Install()
    {
        Patch(typeof(Buyer), "Initialize", new[] { typeof(Vector3) }, postfix: nameof(BuyerInitializedPostfix));
        Patch(typeof(Buyer), "EnterCashRegister", new[] { typeof(CashRegister) }, prefix: nameof(EnterCashRegisterPrefix));
        Patch(typeof(Buyer), "HandleOnCashRegisterComplete", Type.EmptyTypes, postfix: nameof(CheckoutCompletedPostfix));
        Patch(typeof(Buyer), "StartEmptyShelvesExit", Type.EmptyTypes, prefix: nameof(EmptyShelvesPrefix));
        Patch(typeof(Buyer), "StartCleanlinessReactionAndExit", new[] { typeof(ECleanlinessZone) }, prefix: nameof(CleanlinessPrefix));
        Patch(typeof(Buyer), "StartStoreOvercrowdedReactionAndExit", Type.EmptyTypes, prefix: nameof(OvercrowdedPrefix));
        Patch(typeof(Buyer), "RecordLeaveReason", new[] { typeof(BuyerLeaveReason) }, prefix: nameof(RecordReasonPrefix));
        Patch(
            typeof(Buyer),
            "CompleteAndExit",
            new[] { typeof(bool), typeof(BuyerLeaveReason) },
            prefix: nameof(CompleteAndExitPrefix));

        Patch(typeof(BuyerManga), "CompleteOrderSuccess", Type.EmptyTypes, prefix: nameof(MangaSuccessPrefix));
        Patch(
            typeof(BuyerManga),
            "FailAndExit",
            new[] { typeof(bool), typeof(BuyerLeaveReason) },
            prefix: nameof(MangaFailPrefix));
        Patch(
            typeof(BuyerCourier),
            "CompleteOrderSuccess",
            new[] { typeof(NetworkConnection) },
            prefix: nameof(CourierSuccessPrefix));
        Patch(typeof(BuyerCourier), "FailAndExit", new[] { typeof(bool) }, prefix: nameof(CourierFailPrefix));

        Patch(
            typeof(CashRegister),
            "Begin18IdCheck",
            new[] { typeof(BuyerCashRegister), typeof(bool) },
            postfix: nameof(BeginAgeCheckPostfix));
        Patch(
            typeof(CashRegister),
            "Id18CompleteServer",
            new[] { typeof(bool), typeof(NetworkConnection) },
            prefix: nameof(CompleteAgeCheckPrefix));

        if (_installedCount < 8)
            throw new InvalidOperationException($"Подключено слишком мало событий покупателей: {_installedCount}.");
        MelonLogger.Msg($"Shop Reviews: подключено событий игры: {_installedCount}.");
    }

    public static void Uninstall()
    {
        try
        {
            Harmony.UnpatchSelf();
        }
        catch
        {
            // MelonLoader may already be shutting down.
        }
        _installedCount = 0;
    }

    private static void Patch(Type type, string methodName, Type[] parameters, string? prefix = null, string? postfix = null)
    {
        var original = AccessTools.Method(type, methodName, parameters);
        if (original == null)
        {
            MelonLogger.Warning($"Shop Reviews: событие {type.Name}.{methodName} не найдено и будет пропущено.");
            return;
        }

        var prefixMethod = prefix == null ? null : new HarmonyMethod(typeof(ReviewPatches), prefix);
        var postfixMethod = postfix == null ? null : new HarmonyMethod(typeof(ReviewPatches), postfix);
        Harmony.Patch(original, prefixMethod, postfixMethod);
        _installedCount++;
    }

    private static void BuyerInitializedPostfix(Buyer __instance) => ReviewRuntime.Begin(__instance);
    private static void EnterCashRegisterPrefix(Buyer __instance) => ReviewRuntime.QueueStarted(__instance);
    private static void CheckoutCompletedPostfix(Buyer __instance)
    {
        // Since game 1.0.5 a successful checkout exits through ExitStore directly
        // and no longer reaches CompleteAndExit. Finalize here so ordinary purchases
        // participate in the review roll. ReviewRuntime suppresses duplicate exits.
        ReviewRuntime.CheckoutCompleted(__instance);
        ReviewRuntime.FinalizeVisit(__instance, true, BuyerLeaveReason.Unknown);
    }
    private static void EmptyShelvesPrefix(Buyer __instance) => ReviewRuntime.MarkMissingStock(__instance);
    private static void CleanlinessPrefix(Buyer __instance) => ReviewRuntime.MarkCleanliness(__instance);
    private static void OvercrowdedPrefix(Buyer __instance) => ReviewRuntime.MarkOvercrowded(__instance);

    private static void RecordReasonPrefix(Buyer __instance, BuyerLeaveReason leaveReason) =>
        ReviewRuntime.RecordReason(__instance, leaveReason);

    private static void CompleteAndExitPrefix(
        Buyer __instance,
        bool isBoughtSomething,
        BuyerLeaveReason leaveReason) =>
        ReviewRuntime.FinalizeVisit(__instance, isBoughtSomething, leaveReason);

    private static void MangaSuccessPrefix(BuyerManga __instance) =>
        ReviewRuntime.MarkManga(__instance.Buyer, true);

    private static void MangaFailPrefix(BuyerManga __instance) =>
        ReviewRuntime.MarkManga(__instance.Buyer, false);

    private static void CourierSuccessPrefix(BuyerCourier __instance) =>
        ReviewRuntime.MarkCourier(__instance._buyer, true);

    private static void CourierFailPrefix(BuyerCourier __instance) =>
        ReviewRuntime.MarkCourier(__instance._buyer, false);

    private static void BeginAgeCheckPostfix(CashRegister __instance)
    {
        var buyerProducts = __instance.ActiveBuyerProducts;
        ReviewRuntime.BeginAgeCheck(
            __instance.GetInstanceID(),
            buyerProducts?.Buyer,
            __instance._isWrongId);
    }

    private static void CompleteAgeCheckPrefix(CashRegister __instance, bool confirm) =>
        ReviewRuntime.CompleteAgeCheck(__instance.GetInstanceID(), confirm);
}
