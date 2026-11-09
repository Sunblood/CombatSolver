using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CombatSolver.Engine.Cards;
using CombatSolver.Engine.Common;
using CombatSolver.Engine.Mirrors;
using CombatSolver.Search;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace CombatSolver.HextechAdapter;

/// <summary>
/// Registers all conditional vanilla <see cref="CardModel.OnPlay"/> Harmony patch adapters
/// introduced by HextechRunes in <see cref="AdaptedCardOnPlayMirrors"/>.
/// When the corresponding upgrade rune is NOT owned by any player in combat,
/// <c>isPatchActiveInCombat</c> returns <c>false</c> so CombatSolver falls back to the
/// vanilla <see cref="CardOnPlayMirrors"/> handler and records <c>:0</c> in ContinuationStamp.
/// </summary>
public static class HextechVanillaOnPlayAdapters
{
    public const string HextechHarmonyOwner = "HextechRunes";

    private const BindingFlags AllDeclaredFlags =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    public static void RegisterAll(Assembly hextechAssembly)
    {
        ArgumentNullException.ThrowIfNull(hextechAssembly);

        // 1. Outbreak — HextechCombatHooks+OutbreakPatch (Prefix/Postfix/Finalizer, P=400).
        // OutbreakPatch only sets an ambient guard around Outbreak.OnPlay and does not alter
        // Outbreak's card effect, so isPatchActiveInCombat always returns false to use vanilla mirror + CardSpec.
        RegisterOutbreak(hextechAssembly);

        // 2. SovereignBlade — BigKnifeRune+SovereignBladeOnPlayPatch (Prefix P=200)
        RegisterSovereignBlade(hextechAssembly);

        // 3. BodySlam — BodySlamUpgradeRune+BodySlamPatch (Prefix P=200)
        RegisterBodySlam(hextechAssembly);

        // 4. Compact — CompactUpgradeRune+CompactPatch (Postfix P=400)
        RegisterCompact(hextechAssembly);

        // 5. CrashLanding — CrashLandingUpgradeRune+CrashLandingPatch (Prefix P=200)
        RegisterCrashLanding(hextechAssembly);

        // 6. DecisionsDecisions — DecisionsDecisionsUpgradeRune+DecisionsDecisionsOnPlayPatch (Prefix P=200)
        RegisterDecisionsDecisions(hextechAssembly);

        // 7. FlakCannon — FlakCannonUpgradeRune+FlakCannonPlayPatch (Prefix P=200)
        RegisterFlakCannon(hextechAssembly);

        // 8. GrandFinale — GrandFinaleUpgradeRune+GrandFinalePatch (Prefix P=200)
        RegisterGrandFinale(hextechAssembly);

        // 9. Hang — HangUpgradeRune+HangAllDamagePatch (Prefix P=200)
        RegisterHang(hextechAssembly);

        // 10. HiddenGem — HiddenGemUpgradeRune+HiddenGemPatch (Prefix P=200)
        RegisterHiddenGem(hextechAssembly);

        // 11. BladeOfInk — InkshadowRune+BladeOfInkPatch (Prefix P=200)
        RegisterBladeOfInk(hextechAssembly);

        // 12. Jackpot — JackpotUpgradeRune+JackpotPatch (Prefix P=200)
        RegisterJackpot(hextechAssembly);

        // 13. Neurosurge — NeurosurgeUpgradeRune+OnPlayPatch (Prefix P=200)
        RegisterNeurosurge(hextechAssembly);

        // 14. Nightmare — NightmareUpgradeRune+ImmediateNightmarePatch (Prefix P=400, Postfix P=400)
        RegisterNightmare(hextechAssembly);

        // 15. Reboot — RebootUpgradeRune+RebootSelectDrawPatch (Prefix P=200)
        RegisterReboot(hextechAssembly);

        // 16. Survivor — SurvivorUpgradeRune+SurvivorPatch (Prefix P=200)
        RegisterSurvivor(hextechAssembly);

        // 17. Voltaic — VoltaicUpgradeRune+VoltaicPatch (Prefix P=200)
        RegisterVoltaic(hextechAssembly);

        // 18. WroughtInWar — WroughtInWarUpgradeRune+WroughtInWarPatch (Prefix P=200)
        RegisterWroughtInWar(hextechAssembly);
    }

    private static void RegisterOutbreak(Assembly hextechAssembly)
    {
        var patchType = hextechAssembly.GetType("HextechRunes.HextechCombatHooks+OutbreakPatch", throwOnError: false);
        var target = CardOnPlayMirrors.RequireDeclaredOnPlay(typeof(Outbreak));
        if (patchType == null)
            return;

        var prefix = patchType.GetMethod("Prefix", AllDeclaredFlags);
        var postfix = patchType.GetMethod("Postfix", AllDeclaredFlags);
        var finalizer = patchType.GetMethod("Finalizer", AllDeclaredFlags);
        if (prefix == null || postfix == null || finalizer == null)
            return;

        AdaptedCardOnPlayMirrors.Register<Outbreak>(
            "HextechAdapter.Outbreak.v1",
            target,
            new[]
            {
                CreatePatch(HarmonyPatchType.Prefix, prefix, Priority.Normal),
                CreatePatch(HarmonyPatchType.Postfix, postfix, Priority.Normal),
                CreatePatch(HarmonyPatchType.Finalizer, finalizer, Priority.Normal)
            },
            static (_, context) => CardOnPlayMirrors.ApplyRemainingCardSpec(context.Simulator, context.Card, context.CardPlay),
            isPatchActiveInCombat: static _ => false);
    }

    private static void RegisterSovereignBlade(Assembly hextechAssembly)
    {
        var runeType = HextechRuntimeAutoBinder.ResolveType(hextechAssembly, "HextechRunes.BigKnifeRune");
        var patchType = hextechAssembly.GetType("HextechRunes.BigKnifeRune+SovereignBladeOnPlayPatch", throwOnError: false);
        var prefix = patchType?.GetMethod("Prefix", AllDeclaredFlags);
        if (runeType == null || prefix == null)
            return;

        AdaptedCardOnPlayMirrors.Register<SovereignBlade>(
            "HextechAdapter.SovereignBlade.BigKnifeRune.v1",
            CardOnPlayMirrors.RequireDeclaredOnPlay(typeof(SovereignBlade)),
            new[] { CreatePatch(HarmonyPatchType.Prefix, prefix, Priority.Low) },
            static (model, context) =>
            {
                var sim = context.Simulator;
                var card = context.Card;
                var cardPlay = context.CardPlay;
                var repeatVar = model.DynamicVars["Repeat"];
                int hitCount = Math.Max(1, repeatVar.IntValue);
                if (card.TargetType == TargetType.AllEnemies)
                {
                    sim.ExecuteDamageAllEnemies(
                        card,
                        model.DynamicVars.Damage.BaseValue,
                        ValueProp.Move,
                        hitCount: hitCount,
                        cardPlay: cardPlay);
                }
                else if (cardPlay.Target != null)
                {
                    sim.ExecuteDamageSingleTarget(
                        card,
                        cardPlay.Target,
                        model.DynamicVars.Damage.BaseValue,
                        ValueProp.Move,
                        hitCount: hitCount,
                        cardPlay: cardPlay);
                }
            },
            CreateRelicActivePredicate(runeType));
    }

    private static void RegisterBodySlam(Assembly hextechAssembly)
    {
        var runeType = HextechRuntimeAutoBinder.ResolveType(hextechAssembly, "HextechRunes.BodySlamUpgradeRune");
        var patchType = hextechAssembly.GetType("HextechRunes.BodySlamUpgradeRune+BodySlamPatch", throwOnError: false);
        var prefix = patchType?.GetMethod("Prefix", AllDeclaredFlags);
        if (runeType == null || prefix == null)
            return;

        AdaptedCardOnPlayMirrors.Register<BodySlam>(
            "HextechAdapter.BodySlam.BodySlamUpgradeRune.v1",
            CardOnPlayMirrors.RequireDeclaredOnPlay(typeof(BodySlam)),
            new[] { CreatePatch(HarmonyPatchType.Prefix, prefix, Priority.Low) },
            static (model, context) =>
            {
                context.Simulator.ExecuteDamageAllEnemies(
                    context.Card,
                    model.DynamicVars.CalculatedDamage,
                    ValueProp.Move,
                    hitCount: 1,
                    cardPlay: context.CardPlay);
            },
            CreateRelicActivePredicate(runeType));
    }

    private static void RegisterCompact(Assembly hextechAssembly)
    {
        var runeType = HextechRuntimeAutoBinder.ResolveType(hextechAssembly, "HextechRunes.CompactUpgradeRune");
        var patchType = hextechAssembly.GetType("HextechRunes.CompactUpgradeRune+CompactPatch", throwOnError: false);
        var postfix = patchType?.GetMethod("Postfix", AllDeclaredFlags);
        if (runeType == null || postfix == null)
            return;

        AdaptedCardOnPlayMirrors.Register<Compact>(
            "HextechAdapter.Compact.CompactUpgradeRune.v1",
            CardOnPlayMirrors.RequireDeclaredOnPlay(typeof(Compact)),
            new[] { CreatePatch(HarmonyPatchType.Postfix, postfix, Priority.Normal) },
            static (model, context) =>
            {
                var sim = context.Simulator;
                CardOnPlayMirrors.ApplyRemainingCardSpec(sim, context.Card, context.CardPlay);

                for (int i = 0; i < sim.Hand.Count; i++)
                {
                    var handCard = sim.Hand[i];
                    if (handCard.Id == ModelDb.Card<Fuel>().Id && !handCard.IsUpgraded)
                    {
                        sim.UpgradeCard(handCard);
                    }
                }
            },
            CreateRelicActivePredicate(runeType));
    }

    private static void RegisterCrashLanding(Assembly hextechAssembly)
    {
        var runeType = HextechRuntimeAutoBinder.ResolveType(hextechAssembly, "HextechRunes.CrashLandingUpgradeRune");
        var patchType = hextechAssembly.GetType("HextechRunes.CrashLandingUpgradeRune+CrashLandingPatch", throwOnError: false);
        var prefix = patchType?.GetMethod("Prefix", AllDeclaredFlags);
        if (runeType == null || prefix == null)
            return;

        AdaptedCardOnPlayMirrors.Register<CrashLanding>(
            "HextechAdapter.CrashLanding.CrashLandingUpgradeRune.v1",
            CardOnPlayMirrors.RequireDeclaredOnPlay(typeof(CrashLanding)),
            new[] { CreatePatch(HarmonyPatchType.Prefix, prefix, Priority.Low) },
            static (model, context) =>
            {
                var sim = context.Simulator;
                sim.ExecuteDamageAllEnemies(
                    context.Card,
                    model.DynamicVars.Damage.BaseValue,
                    ValueProp.Move,
                    hitCount: 1,
                    cardPlay: context.CardPlay);

                int slotsToFill = 10 - sim.Hand.Count;
                for (int i = 0; i < slotsToFill; i++)
                {
                    var debris = sim.CreatePredictedCardFromModel(sim.CombatState.CreateCard<Debris>(model.Owner));
                    debris.ApplySingleTurnCostOverride(0);
                    sim.AddCardToPile(debris, PileType.Hand);
                }
            },
            CreateRelicActivePredicate(runeType));
    }

    private static void RegisterDecisionsDecisions(Assembly hextechAssembly)
    {
        var runeType = HextechRuntimeAutoBinder.ResolveType(hextechAssembly, "HextechRunes.DecisionsDecisionsUpgradeRune");
        var patchType = hextechAssembly.GetType("HextechRunes.DecisionsDecisionsUpgradeRune+DecisionsDecisionsOnPlayPatch", throwOnError: false);
        var prefix = patchType?.GetMethod("Prefix", AllDeclaredFlags);
        if (runeType == null || prefix == null)
            return;

        AdaptedCardOnPlayMirrors.Register<DecisionsDecisions>(
            "HextechAdapter.DecisionsDecisions.DecisionsDecisionsUpgradeRune.v1",
            CardOnPlayMirrors.RequireDeclaredOnPlay(typeof(DecisionsDecisions)),
            new[] { CreatePatch(HarmonyPatchType.Prefix, prefix, Priority.Low) },
            static (model, context) =>
            {
                var sim = context.Simulator;
                var card = context.Card;
                sim.DrawCards(card.GetResolvedDynamicVarValue("Cards", model.DynamicVars.Cards.IntValue), fromHandDraw: false);

                int repeat = card.GetResolvedDynamicVarValue("Repeat", model.DynamicVars.Repeat.IntValue);
                if (sim.CaptureSelectionSeedIfExecutionSuspended("DecisionsDecisions.SelectHandCard", "DecisionsDecisions.AutoPlaySelectedCard"))
                    return;

                var candidates = sim.Hand.Where(c => c.CardType is CardType.Skill or CardType.Power).ToList();
                var selected = sim.SelectSingleCardOrBeginInteractiveChoice(
                    new CardSelectorPrefs(CardSelectorPrefs.GenericSelectionPrompt, 1),
                    candidates,
                    card,
                    selectedCard =>
                    {
                        if (selectedCard == null)
                            return;
                        for (int i = 0; i < repeat; i++)
                        {
                            sim.ExecuteAutoPlayCard(selectedCard);
                        }
                    });
            },
            CreateRelicActivePredicate(runeType));
    }

    private static void RegisterFlakCannon(Assembly hextechAssembly)
    {
        var runeType = HextechRuntimeAutoBinder.ResolveType(hextechAssembly, "HextechRunes.FlakCannonUpgradeRune");
        var patchType = hextechAssembly.GetType("HextechRunes.FlakCannonUpgradeRune+FlakCannonPlayPatch", throwOnError: false);
        var prefix = patchType?.GetMethod("Prefix", AllDeclaredFlags);
        if (runeType == null || prefix == null)
            return;

        AdaptedCardOnPlayMirrors.Register<FlakCannon>(
            "HextechAdapter.FlakCannon.FlakCannonUpgradeRune.v1",
            CardOnPlayMirrors.RequireDeclaredOnPlay(typeof(FlakCannon)),
            new[] { CreatePatch(HarmonyPatchType.Prefix, prefix, Priority.Low) },
            static (model, context) =>
            {
                var sim = context.Simulator;
                var card = context.Card;
                var nonExceptionCards = new List<PredictedCard>();

                void CollectFromPile(IReadOnlyList<PredictedCard> pile)
                {
                    for (int i = 0; i < pile.Count; i++)
                    {
                        var c = pile[i];
                        if (c.CardType == CardType.Status && c.Id != ModelDb.Card<Dazed>().Id)
                        {
                            nonExceptionCards.Add(c);
                        }
                    }
                }

                CollectFromPile(sim.Hand);
                CollectFromPile(sim.DrawPile);
                CollectFromPile(sim.DiscardPile);

                for (int i = 0; i < nonExceptionCards.Count; i++)
                {
                    sim.ExhaustCardFromCurrentPile(nonExceptionCards[i]);
                }

                int dazedCount =
                    sim.Hand.Count(c => c.Id == ModelDb.Card<Dazed>().Id) +
                    sim.DrawPile.Count(c => c.Id == ModelDb.Card<Dazed>().Id) +
                    sim.DiscardPile.Count(c => c.Id == ModelDb.Card<Dazed>().Id);

                int baseRepeats = model.DynamicVars.Repeat.IntValue;
                int totalHits = baseRepeats + nonExceptionCards.Count + dazedCount;
                for (int i = 0; i < totalHits; i++)
                {
                    sim.ExecuteDamageRandomEnemy(
                        card,
                        model.DynamicVars.Damage.BaseValue,
                        ValueProp.Move,
                        cardPlay: context.CardPlay);
                }
            },
            CreateRelicActivePredicate(runeType));
    }

    private static void RegisterGrandFinale(Assembly hextechAssembly)
    {
        var runeType = HextechRuntimeAutoBinder.ResolveType(hextechAssembly, "HextechRunes.GrandFinaleUpgradeRune");
        var patchType = hextechAssembly.GetType("HextechRunes.GrandFinaleUpgradeRune+GrandFinalePatch", throwOnError: false);
        var prefix = patchType?.GetMethod("Prefix", AllDeclaredFlags);
        if (runeType == null || prefix == null)
            return;

        AdaptedCardOnPlayMirrors.Register<GrandFinale>(
            "HextechAdapter.GrandFinale.GrandFinaleUpgradeRune.v1",
            CardOnPlayMirrors.RequireDeclaredOnPlay(typeof(GrandFinale)),
            new[] { CreatePatch(HarmonyPatchType.Prefix, prefix, Priority.Low) },
            static (model, context) =>
            {
                var sim = context.Simulator;
                sim.ExecuteDamageAllEnemies(
                    context.Card,
                    model.DynamicVars.Damage.BaseValue,
                    ValueProp.Move,
                    hitCount: 1,
                    cardPlay: context.CardPlay);

                if (sim.DrawPile.Count == 0 && sim.DiscardPile.Count == 0)
                {
                    sim.ExecuteDamageAllEnemies(
                        context.Card,
                        model.DynamicVars.Damage.BaseValue,
                        ValueProp.Move,
                        hitCount: 1,
                        cardPlay: context.CardPlay);
                }
            },
            CreateRelicActivePredicate(runeType));
    }

    private static void RegisterHang(Assembly hextechAssembly)
    {
        var runeType = HextechRuntimeAutoBinder.ResolveType(hextechAssembly, "HextechRunes.HangUpgradeRune");
        var patchType = hextechAssembly.GetType("HextechRunes.HangUpgradeRune+HangAllDamagePatch", throwOnError: false);
        var prefix = patchType?.GetMethod("Prefix", AllDeclaredFlags);
        if (runeType == null || prefix == null)
            return;

        AdaptedCardOnPlayMirrors.Register<Hang>(
            "HextechAdapter.Hang.HangUpgradeRune.v1",
            CardOnPlayMirrors.RequireDeclaredOnPlay(typeof(Hang)),
            new[] { CreatePatch(HarmonyPatchType.Prefix, prefix, Priority.Low) },
            static (_, context) => CardOnPlayMirrors.ApplyRemainingCardSpec(context.Simulator, context.Card, context.CardPlay),
            CreateRelicActivePredicate(runeType));
    }

    private static void RegisterHiddenGem(Assembly hextechAssembly)
    {
        var runeType = HextechRuntimeAutoBinder.ResolveType(hextechAssembly, "HextechRunes.HiddenGemUpgradeRune");
        var patchType = hextechAssembly.GetType("HextechRunes.HiddenGemUpgradeRune+HiddenGemPatch", throwOnError: false);
        var prefix = patchType?.GetMethod("Prefix", AllDeclaredFlags);
        if (runeType == null || prefix == null)
            return;

        AdaptedCardOnPlayMirrors.Register<HiddenGem>(
            "HextechAdapter.HiddenGem.HiddenGemUpgradeRune.v1",
            CardOnPlayMirrors.RequireDeclaredOnPlay(typeof(HiddenGem)),
            new[] { CreatePatch(HarmonyPatchType.Prefix, prefix, Priority.Low) },
            static (model, context) =>
            {
                var sim = context.Simulator;
                sim.GainBlockOnPlayer(model.DynamicVars.Block, context.Card, context.CardPlay);

                var candidates = new List<PredictedCard>();
                void AddCandidates(IReadOnlyList<PredictedCard> pile)
                {
                    for (int i = 0; i < pile.Count; i++)
                    {
                        var c = pile[i];
                        if (c.CardType is not (CardType.Status or CardType.Curse or CardType.Quest))
                        {
                            candidates.Add(c);
                        }
                    }
                }

                AddCandidates(sim.DrawPile);
                AddCandidates(sim.Hand);
                AddCandidates(sim.DiscardPile);

                if (candidates.Count == 0)
                    return;

                var targetCard = sim.ChoiceRng.NextItem(candidates);
                if (targetCard != null)
                {
                    int replayAmount = context.Card.GetResolvedDynamicVarValue("Replay", model.DynamicVars.Replay.IntValue);
                    targetCard.AddBaseReplayCount(replayAmount);
                }
            },
            CreateRelicActivePredicate(runeType));
    }

    private static void RegisterBladeOfInk(Assembly hextechAssembly)
    {
        var runeType = HextechRuntimeAutoBinder.ResolveType(hextechAssembly, "HextechRunes.InkshadowRune");
        var patchType = hextechAssembly.GetType("HextechRunes.InkshadowRune+BladeOfInkPatch", throwOnError: false);
        var prefix = patchType?.GetMethod("Prefix", AllDeclaredFlags);
        var powerType = HextechRuntimeAutoBinder.ResolveType(hextechAssembly, "HextechRunes.InkshadowPower");
        if (runeType == null || prefix == null || powerType == null)
            return;

        var applyPowerMethod = typeof(HextechVanillaOnPlayAdapters)
            .GetMethod(nameof(ApplyPlayerPowerGeneric), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(powerType);

        AdaptedCardOnPlayMirrors.Register<BladeOfInk>(
            "HextechAdapter.BladeOfInk.InkshadowRune.v1",
            CardOnPlayMirrors.RequireDeclaredOnPlay(typeof(BladeOfInk)),
            new[] { CreatePatch(HarmonyPatchType.Prefix, prefix, Priority.Low) },
            (model, context) =>
            {
                decimal amount = model.DynamicVars["Strength"].BaseValue;
                applyPowerMethod.Invoke(null, new object[] { context.Simulator, amount });
            },
            CreateRelicActivePredicate(runeType));
    }

    private static void RegisterJackpot(Assembly hextechAssembly)
    {
        var runeType = HextechRuntimeAutoBinder.ResolveType(hextechAssembly, "HextechRunes.JackpotUpgradeRune");
        var patchType = hextechAssembly.GetType("HextechRunes.JackpotUpgradeRune+JackpotPatch", throwOnError: false);
        var prefix = patchType?.GetMethod("Prefix", AllDeclaredFlags);
        if (runeType == null || prefix == null)
            return;

        AdaptedCardOnPlayMirrors.Register<Jackpot>(
            "HextechAdapter.Jackpot.JackpotUpgradeRune.v1",
            CardOnPlayMirrors.RequireDeclaredOnPlay(typeof(Jackpot)),
            new[] { CreatePatch(HarmonyPatchType.Prefix, prefix, Priority.Low) },
            static (model, context) =>
            {
                var sim = context.Simulator;
                var card = context.Card;
                if (context.CardPlay.Target != null)
                {
                    sim.ExecuteDamageSingleTarget(
                        card,
                        context.CardPlay.Target,
                        model.DynamicVars.Damage.BaseValue,
                        ValueProp.Move,
                        hitCount: 1,
                        cardPlay: context.CardPlay);
                }

                int count = card.GetResolvedDynamicVarValue("Cards", model.DynamicVars.Cards.IntValue);
                var pool = model.Owner.Character.CardPool.GetUnlockedCards(
                    model.Owner.UnlockState,
                    model.Owner.RunState.CardMultiplayerConstraint)
                    .Where(c => c.CanonicalEnergyCost >= 3 && !c.EnergyCost.CostsX)
                    .ToList();

                for (int i = 0; i < count && pool.Count > 0; i++)
                {
                    var picked = model.Owner.RunState.Rng.CombatCardGeneration.NextItem(pool);
                    if (picked == null)
                        continue;

                    var generated = sim.CreatePredictedCardFromModel(sim.CombatState.CreateCard(picked, model.Owner));
                    if (card.IsUpgraded)
                    {
                        sim.UpgradeCard(generated);
                    }
                    generated.ApplySingleTurnCostOverride(0);
                    sim.AddCardToPile(generated, PileType.Hand);
                }
            },
            CreateRelicActivePredicate(runeType));
    }

    private static void RegisterNeurosurge(Assembly hextechAssembly)
    {
        var runeType = HextechRuntimeAutoBinder.ResolveType(hextechAssembly, "HextechRunes.NeurosurgeUpgradeRune");
        var patchType = hextechAssembly.GetType("HextechRunes.NeurosurgeUpgradeRune+OnPlayPatch", throwOnError: false);
        var prefix = patchType?.GetMethod("Prefix", AllDeclaredFlags);
        if (runeType == null || prefix == null)
            return;

        AdaptedCardOnPlayMirrors.Register<Neurosurge>(
            "HextechAdapter.Neurosurge.NeurosurgeUpgradeRune.v1",
            CardOnPlayMirrors.RequireDeclaredOnPlay(typeof(Neurosurge)),
            new[] { CreatePatch(HarmonyPatchType.Prefix, prefix, Priority.Low) },
            static (model, context) =>
            {
                var sim = context.Simulator;
                var card = context.Card;
                sim.GainEnergy(card.GetResolvedDynamicVarValue("Energy", model.DynamicVars.Energy.IntValue));
                sim.DrawCards(card.GetResolvedDynamicVarValue("Cards", model.DynamicVars.Cards.IntValue), fromHandDraw: false);

                decimal doomAmount = model.DynamicVars["Power"].BaseValue;
                var enemies = sim.Enemies;
                for (int i = 0; i < enemies.Count; i++)
                {
                    if (enemies[i].IsAlive)
                    {
                        sim.ApplyPowerToEnemy<NeurosurgePower>(enemies[i], doomAmount);
                    }
                }
            },
            CreateRelicActivePredicate(runeType));
    }

    private static void RegisterNightmare(Assembly hextechAssembly)
    {
        var runeType = HextechRuntimeAutoBinder.ResolveType(hextechAssembly, "HextechRunes.NightmareUpgradeRune");
        var patchType = hextechAssembly.GetType("HextechRunes.NightmareUpgradeRune+ImmediateNightmarePatch", throwOnError: false);
        var prefix = patchType?.GetMethod("Prefix", AllDeclaredFlags);
        var postfix = patchType?.GetMethod("Postfix", AllDeclaredFlags);
        if (runeType == null || prefix == null || postfix == null)
            return;

        AdaptedCardOnPlayMirrors.Register<Nightmare>(
            "HextechAdapter.Nightmare.NightmareUpgradeRune.v1",
            CardOnPlayMirrors.RequireDeclaredOnPlay(typeof(Nightmare)),
            new[]
            {
                CreatePatch(HarmonyPatchType.Prefix, prefix, Priority.Normal),
                CreatePatch(HarmonyPatchType.Postfix, postfix, Priority.Normal)
            },
            static (model, context) =>
            {
                var sim = context.Simulator;
                var card = context.Card;
                if (sim.CaptureSelectionSeedIfExecutionSuspended("Nightmare.SelectHandCard", "Nightmare.AddCopiesToHand"))
                    return;

                sim.SelectSingleCardOrBeginInteractiveChoice(
                    new CardSelectorPrefs(CardSelectorPrefs.GenericSelectionPrompt, 1),
                    sim.Hand.ToList(),
                    card,
                    selectedCard =>
                    {
                        if (selectedCard == null)
                            return;

                        for (int i = 0; i < 3; i++)
                        {
                            var clone = sim.ClonePredictedCard(selectedCard);
                            sim.AddCardToPile(clone, PileType.Hand);
                        }
                    });
            },
            CreateRelicActivePredicate(runeType));
    }

    private static void RegisterReboot(Assembly hextechAssembly)
    {
        var runeType = HextechRuntimeAutoBinder.ResolveType(hextechAssembly, "HextechRunes.RebootUpgradeRune");
        var patchType = hextechAssembly.GetType("HextechRunes.RebootUpgradeRune+RebootSelectDrawPatch", throwOnError: false);
        var prefix = patchType?.GetMethod("Prefix", AllDeclaredFlags);
        if (runeType == null || prefix == null)
            return;

        AdaptedCardOnPlayMirrors.Register<Reboot>(
            "HextechAdapter.Reboot.RebootUpgradeRune.v1",
            CardOnPlayMirrors.RequireDeclaredOnPlay(typeof(Reboot)),
            new[] { CreatePatch(HarmonyPatchType.Prefix, prefix, Priority.Low) },
            static (model, context) =>
            {
                var sim = context.Simulator;
                var card = context.Card;
                var handSnapshot = sim.Hand.ToList();
                for (int i = 0; i < handSnapshot.Count; i++)
                {
                    sim.MoveCardToDrawPile(handSnapshot[i], randomPosition: false);
                }
                sim.ShuffleDrawPileIfNeeded(force: true);

                int drawCount = Math.Min(
                    card.GetResolvedDynamicVarValue("Cards", model.DynamicVars.Cards.IntValue),
                    sim.DrawPile.Count);
                if (drawCount <= 0)
                    return;

                if (sim.CaptureSelectionSeedIfExecutionSuspended("Reboot.SelectDrawCards", "Reboot.MoveSelectedToHand"))
                    return;

                sim.SelectCardsOrBeginInteractiveChoice(
                    new CardSelectorPrefs(CardSelectorPrefs.GenericSelectionPrompt, drawCount, drawCount),
                    sim.DrawPile.ToList(),
                    card,
                    selectedCards =>
                    {
                        foreach (var selected in selectedCards)
                        {
                            sim.MoveCardToHand(selected);
                        }
                    });
            },
            CreateRelicActivePredicate(runeType));
    }

    private static void RegisterSurvivor(Assembly hextechAssembly)
    {
        var runeType = HextechRuntimeAutoBinder.ResolveType(hextechAssembly, "HextechRunes.SurvivorUpgradeRune");
        var patchType = hextechAssembly.GetType("HextechRunes.SurvivorUpgradeRune+SurvivorPatch", throwOnError: false);
        var prefix = patchType?.GetMethod("Prefix", AllDeclaredFlags);
        if (runeType == null || prefix == null)
            return;

        AdaptedCardOnPlayMirrors.Register<Survivor>(
            "HextechAdapter.Survivor.SurvivorUpgradeRune.v1",
            CardOnPlayMirrors.RequireDeclaredOnPlay(typeof(Survivor)),
            new[] { CreatePatch(HarmonyPatchType.Prefix, prefix, Priority.Low) },
            static (model, context) =>
            {
                var sim = context.Simulator;
                var card = context.Card;
                sim.GainBlockOnPlayer(model.DynamicVars.Block, card, context.CardPlay);

                if (sim.CaptureSelectionSeedIfExecutionSuspended("Survivor.SelectDiscard", "Survivor.DiscardAndRetain"))
                    return;

                sim.SelectSingleCardOrBeginInteractiveChoice(
                    new CardSelectorPrefs(CardSelectorPrefs.DiscardSelectionPrompt, 1),
                    sim.Hand.ToList(),
                    card,
                    selectedCard =>
                    {
                        if (selectedCard != null)
                        {
                            sim.DiscardCardFromCurrentPile(selectedCard);
                        }

                        for (int i = 0; i < sim.Hand.Count; i++)
                        {
                            sim.Hand[i].GiveSingleTurnRetain();
                        }
                    });
            },
            CreateRelicActivePredicate(runeType));
    }

    private static void RegisterVoltaic(Assembly hextechAssembly)
    {
        var runeType = HextechRuntimeAutoBinder.ResolveType(hextechAssembly, "HextechRunes.VoltaicUpgradeRune");
        var patchType = hextechAssembly.GetType("HextechRunes.VoltaicUpgradeRune+VoltaicPatch", throwOnError: false);
        var prefix = patchType?.GetMethod("Prefix", AllDeclaredFlags);
        if (runeType == null || prefix == null)
            return;

        AdaptedCardOnPlayMirrors.Register<Voltaic>(
            "HextechAdapter.Voltaic.VoltaicUpgradeRune.v1",
            CardOnPlayMirrors.RequireDeclaredOnPlay(typeof(Voltaic)),
            new[] { CreatePatch(HarmonyPatchType.Prefix, prefix, Priority.Low) },
            static (model, context) =>
            {
                var sim = context.Simulator;
                int count = context.Card.GetResolvedDynamicVarValue(
                    "CalculationExtra",
                    (int)model.DynamicVars.CalculationExtra.BaseValue);
                for (int i = 0; i < count; i++)
                {
                    sim.ChannelOrb("Lightning");
                }
            },
            CreateRelicActivePredicate(runeType));
    }

    private static void RegisterWroughtInWar(Assembly hextechAssembly)
    {
        var runeType = HextechRuntimeAutoBinder.ResolveType(hextechAssembly, "HextechRunes.WroughtInWarUpgradeRune");
        var patchType = hextechAssembly.GetType("HextechRunes.WroughtInWarUpgradeRune+WroughtInWarPatch", throwOnError: false);
        var prefix = patchType?.GetMethod("Prefix", AllDeclaredFlags);
        if (runeType == null || prefix == null)
            return;

        AdaptedCardOnPlayMirrors.Register<WroughtInWar>(
            "HextechAdapter.WroughtInWar.WroughtInWarUpgradeRune.v1",
            CardOnPlayMirrors.RequireDeclaredOnPlay(typeof(WroughtInWar)),
            new[] { CreatePatch(HarmonyPatchType.Prefix, prefix, Priority.Low) },
            static (model, context) =>
            {
                var sim = context.Simulator;
                var card = context.Card;
                if (context.CardPlay.Target != null)
                {
                    sim.ExecuteDamageSingleTarget(
                        card,
                        context.CardPlay.Target,
                        model.DynamicVars.Damage.BaseValue,
                        ValueProp.Move,
                        hitCount: 1,
                        cardPlay: context.CardPlay);
                }

                for (int i = 0; i < sim.Hand.Count; i++)
                {
                    sim.UpgradeCard(sim.Hand[i]);
                }
            },
            CreateRelicActivePredicate(runeType));
    }

    private static void ApplyPlayerPowerGeneric<TPower>(CombatPredictionSimulator sim, decimal amount)
        where TPower : PowerModel
    {
        sim.ApplyPowerToPlayer<TPower>(amount);
    }

    private static AdaptedOnPlayPatch CreatePatch(HarmonyPatchType kind, MethodInfo method, int priority) =>
        new(
            kind,
            method,
            HextechHarmonyOwner,
            priority,
            Array.Empty<string>(),
            Array.Empty<string>());

    private static Func<CombatState, bool> CreateRelicActivePredicate(Type relicType)
    {
        return combatState =>
        {
            if (combatState?.Players == null)
                return false;

            for (int p = 0; p < combatState.Players.Count; p++)
            {
                var relics = combatState.Players[p]?.Relics;
                if (relics == null)
                    continue;

                for (int r = 0; r < relics.Count; r++)
                {
                    if (relicType.IsInstanceOfType(relics[r]))
                        return true;
                }
            }

            return false;
        };
    }
}
