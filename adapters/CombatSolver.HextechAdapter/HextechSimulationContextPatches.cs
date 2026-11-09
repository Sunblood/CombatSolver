using System;
using System.Reflection;
using CombatSolver.Search;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace CombatSolver.HextechAdapter;

/// <summary>
/// Pillar 4 — Ambient Simulation Context Redirection.
/// Installs Harmony prefixes gated by <c>CombatPredictionContext.ActiveCombatState is SimulatedCombatState sim</c>
/// on HextechCombatHistoryHelper, HextechRelicBase.EnsureTurnScopedStateCurrent &amp; GetOwnerTurnNumber,
/// HextechMayhemModifier.FindIn, and HextechCombatHooks.GetEnergyCostForCurrentCardPlay.
/// Main-thread live execution (<c>CombatPredictionContext.IsSimulating == false</c>) is 100% untouched.
/// </summary>
public static class HextechSimulationContextPatches
{
    private const BindingFlags AllDeclaredFlags =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static FieldInfo? _turnScopedCombatStateField;
    private static FieldInfo? _turnScopedTurnNumberField;
    private static Type? _mayhemModifierType;

    public static void Apply(Harmony harmony, Assembly hextechAssembly)
    {
        ArgumentNullException.ThrowIfNull(harmony);
        ArgumentNullException.ThrowIfNull(hextechAssembly);

        PatchCombatHistoryHelper(harmony, hextechAssembly);
        PatchRelicBaseTurnScope(harmony, hextechAssembly);
        PatchMayhemModifierFindIn(harmony, hextechAssembly);
        PatchGetEnergyCostForCurrentCardPlay(harmony, hextechAssembly);
    }

    private static void PatchCombatHistoryHelper(Harmony harmony, Assembly hextechAssembly)
    {
        var helperType = HextechRuntimeAutoBinder.ResolveType(hextechAssembly, "HextechRunes.HextechCombatHistoryHelper");
        if (helperType == null)
            return;

        PatchPrefix(
            harmony,
            helperType.GetMethod("CountOwnedAttackCardsPlayed", AllDeclaredFlags, null, new[] { typeof(Player) }, null),
            nameof(PrefixCountOwnedAttackCardsPlayed));

        PatchPrefix(
            harmony,
            helperType.GetMethod("CountOwnedCardsPlayed", AllDeclaredFlags, null, new[] { typeof(Player), typeof(Func<CardModel, bool>) }, null),
            nameof(PrefixCountOwnedCardsPlayed));

        PatchPrefix(
            harmony,
            helperType.GetMethod("CountOwnedAttackCardsPlayedThisTurn", AllDeclaredFlags, null, new[] { typeof(Player), typeof(ICombatState) }, null),
            nameof(PrefixCountOwnedAttackCardsPlayedThisTurn));

        PatchPrefix(
            harmony,
            helperType.GetMethod("CountOwnedCardsDrawn", AllDeclaredFlags, null, new[] { typeof(Player) }, null),
            nameof(PrefixCountOwnedCardsDrawn));
    }

    private static void PatchRelicBaseTurnScope(Harmony harmony, Assembly hextechAssembly)
    {
        var relicBaseType = HextechRuntimeAutoBinder.ResolveType(hextechAssembly, "HextechRunes.HextechRelicBase");
        if (relicBaseType == null)
            return;

        _turnScopedCombatStateField = relicBaseType.GetField("_turnScopedCombatState", AllDeclaredFlags);
        _turnScopedTurnNumberField = relicBaseType.GetField("_turnScopedTurnNumber", AllDeclaredFlags);

        PatchPrefix(
            harmony,
            relicBaseType.GetMethod("EnsureTurnScopedStateCurrent", AllDeclaredFlags, null, new[] { typeof(Action) }, null),
            nameof(PrefixEnsureTurnScopedStateCurrent));

        PatchPrefix(
            harmony,
            relicBaseType.GetMethod("GetOwnerTurnNumber", AllDeclaredFlags, null, new[] { typeof(ICombatState) }, null),
            nameof(PrefixGetOwnerTurnNumber));
    }

    private static void PatchMayhemModifierFindIn(Harmony harmony, Assembly hextechAssembly)
    {
        _mayhemModifierType = HextechRuntimeAutoBinder.ResolveType(hextechAssembly, "HextechRunes.HextechMayhemModifier");
        if (_mayhemModifierType == null)
            return;

        PatchPrefix(
            harmony,
            _mayhemModifierType.GetMethod("FindIn", AllDeclaredFlags, null, new[] { typeof(ICombatState) }, null),
            nameof(PrefixMayhemModifierFindIn));
    }

    private static void PatchGetEnergyCostForCurrentCardPlay(Harmony harmony, Assembly hextechAssembly)
    {
        var hooksType = HextechRuntimeAutoBinder.ResolveType(hextechAssembly, "HextechRunes.HextechCombatHooks");
        if (hooksType == null)
            return;

        PatchPrefix(
            harmony,
            hooksType.GetMethod("GetEnergyCostForCurrentCardPlay", AllDeclaredFlags, null, new[] { typeof(CardModel) }, null),
            nameof(PrefixGetEnergyCostForCurrentCardPlay));
    }

    private static void PatchPrefix(Harmony harmony, MethodInfo? target, string prefixMethodName)
    {
        if (target == null)
            return;

        var prefix = typeof(HextechSimulationContextPatches).GetMethod(prefixMethodName, BindingFlags.Static | BindingFlags.NonPublic);
        if (prefix != null)
        {
            harmony.Patch(target, prefix: new HarmonyMethod(prefix));
        }
    }

    private static bool PrefixCountOwnedAttackCardsPlayed(Player? owner, ref int __result)
    {
        if (CombatPredictionContext.ActiveCombatState is not SimulatedCombatState sim)
            return true;

        if (owner == null)
        {
            __result = 0;
            return false;
        }

        int count = 0;
        var entries = sim.PredictionHistory.PlayEntries;
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (ReferenceEquals(entry.Owner, owner) && entry.CardType == CardType.Attack)
            {
                count++;
            }
        }

        __result = count;
        return false;
    }

    private static bool PrefixCountOwnedCardsPlayed(Player? owner, Func<CardModel, bool>? matches, ref int __result)
    {
        if (CombatPredictionContext.ActiveCombatState is not SimulatedCombatState sim)
            return true;

        if (owner == null || matches == null)
        {
            __result = 0;
            return false;
        }

        int count = 0;
        var entries = sim.PredictionHistory.PlayEntries;
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (!ReferenceEquals(entry.Owner, owner))
                continue;

            var cardModel = entry.Card?.Preview ?? entry.Card?.Original ?? entry.Model;
            if (cardModel != null && matches(cardModel))
            {
                count++;
            }
        }

        __result = count;
        return false;
    }

    private static bool PrefixCountOwnedAttackCardsPlayedThisTurn(Player? owner, ICombatState? combatState, ref int __result)
    {
        if (CombatPredictionContext.ActiveCombatState is not SimulatedCombatState sim)
            return true;

        if (owner == null)
        {
            __result = 0;
            return false;
        }

        int count = 0;
        var entries = sim.PredictionHistory.PlayEntries;
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry.HappenedThisTurn
                && entry.RoundNumber == sim.RoundNumber
                && entry.CurrentSide == sim.CurrentSide
                && ReferenceEquals(entry.Owner, owner)
                && entry.CardType == CardType.Attack)
            {
                count++;
            }
        }

        __result = count;
        return false;
    }

    private static bool PrefixCountOwnedCardsDrawn(Player? owner, ref int __result)
    {
        if (CombatPredictionContext.ActiveCombatState is not SimulatedCombatState sim)
            return true;

        if (owner == null)
        {
            __result = 0;
            return false;
        }

        int count = 0;
        var entries = sim.PredictionHistory.DrawEntries;
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (ReferenceEquals(entry.Actor?.Player, owner))
            {
                count++;
            }
        }

        __result = count;
        return false;
    }

    private static bool PrefixEnsureTurnScopedStateCurrent(RelicModel __instance, Action resetState)
    {
        if (CombatPredictionContext.ActiveCombatState is not SimulatedCombatState sim)
            return true;

        ArgumentNullException.ThrowIfNull(resetState);

        var currentTurnNumber = sim.RoundNumber;
        if (_turnScopedCombatStateField != null && _turnScopedTurnNumberField != null)
        {
            var recordedTurn = (int)(_turnScopedTurnNumberField.GetValue(__instance) ?? -1);
            if (recordedTurn == currentTurnNumber)
                return false;

            resetState();
            _turnScopedCombatStateField.SetValue(__instance, sim);
            _turnScopedTurnNumberField.SetValue(__instance, currentTurnNumber);
        }

        return false;
    }

    private static bool PrefixGetOwnerTurnNumber(ICombatState combatState, ref int __result)
    {
        if (CombatPredictionContext.ActiveCombatState is not SimulatedCombatState sim)
            return true;

        __result = sim.RoundNumber;
        return false;
    }

    private static bool PrefixMayhemModifierFindIn(ICombatState? combatState, ref object? __result)
    {
        if (CombatPredictionContext.ActiveCombatState is not SimulatedCombatState sim)
            return true;

        if (_mayhemModifierType != null)
        {
            var modifiers = sim.Modifiers;
            for (int i = 0; i < modifiers.Count; i++)
            {
                if (_mayhemModifierType.IsInstanceOfType(modifiers[i]))
                {
                    __result = modifiers[i];
                    return false;
                }
            }
        }

        __result = null;
        return false;
    }

    private static bool PrefixGetEnergyCostForCurrentCardPlay(CardModel card, ref decimal __result)
    {
        if (CombatPredictionContext.ActiveCombatState is not SimulatedCombatState sim)
            return true;

        if (card == null)
        {
            __result = 0m;
            return false;
        }

        if (sim.PredictedState != null && sim.PredictedState.TryGetPredictedCard(card, out var predictedCard) && predictedCard != null)
        {
            __result = Math.Max(0, predictedCard.EnergyCost);
            return false;
        }

        __result = card.EnergyCost.GetWithModifiers(CostModifiers.All);
        return false;
    }
}
