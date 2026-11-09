using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models.Relics;
using CombatSolver.Engine.InCombat.Simulation;

namespace CombatSolver;

internal static class TurnStartRelicSupport
{
    public static bool TriggerBeforeSideTurnStart(
        CombatPredictionSimulator simulator,
        SimulatedCombatState combat,
        IReadOnlyList<Creature> participants)
        => combat.PrepareRelicsBeforeSideTurnStart(simulator, participants);

    public static void TriggerAfterEnergyReset(
        CombatPredictionSimulator simulator,
        SimulatedCombatState combat,
        Player player)
    {
        combat.TriggerRelicsAfterEnergyReset(simulator, player);
        if (simulator.HasPendingChoice)
            return;

        CombatSolver.Engine.InCombat.Mirrors.Hooks.Resources.AfterEnergyResetMirrors.Seal();
        CombatSolver.Engine.InCombat.Mirrors.Hooks.Resources.AfterEnergyResetMirrorContext? context = null;
        foreach (MegaCrit.Sts2.Core.Models.AbstractModel listener in
                 ((CombatSolver.Engine.Common.ICombatPredictionHookListenerSource)combat)
                     .MirroredHookListeners)
        {
            if (listener is MegaCrit.Sts2.Core.Models.PowerModel
                || listener.GetType().Assembly == typeof(MegaCrit.Sts2.Core.Models.AbstractModel).Assembly)
            {
                continue;
            }
            if (!CombatSolver.Engine.InCombat.Mirrors.Hooks.Resources.AfterEnergyResetMirrors.HasOverride(listener))
                continue;
            context ??= new CombatSolver.Engine.InCombat.Mirrors.Hooks.Resources.AfterEnergyResetMirrorContext
            {
                Simulator = simulator,
                Player = player,
            };
            CombatSolver.Engine.InCombat.Mirrors.Hooks.Resources.AfterEnergyResetMirrors.Invoke(listener, context);
            if (simulator.HasPendingChoice)
                return;
        }
    }

    public static bool TriggerAfterPlayerTurnStart(
        CombatPredictionSimulator simulator,
        SimulatedCombatState combat,
        Player player,
        TurnStartChoiceCursor choices)
        => combat.TriggerRelicsAfterPlayerTurnStart(simulator, player, choices);

    public static bool TriggerAfterSideTurnStart(
        CombatPredictionSimulator simulator,
        SimulatedCombatState combat,
        CombatSide side,
        IReadOnlyList<Creature> participants)
        => combat.TriggerRelicsAfterSideTurnStart(simulator, side, participants);

    public static bool TriggerAfterSideTurnEnd(
        CombatPredictionSimulator simulator,
        SimulatedCombatState combat,
        IReadOnlyList<Creature> participants,
        int etherealExhaustCount)
        => combat.CompleteRelicsAfterSideTurnEnd(simulator, participants, etherealExhaustCount);

    public static void TriggerBeforeSideTurnEnd(
        CombatPredictionSimulator simulator,
        SimulatedCombatState combat,
        IReadOnlyList<Creature> participants)
        => combat.PrepareRelicsBeforeSideTurnEnd(simulator, participants);


    public static void TriggerAfterEnergyResetLate(
        CombatPredictionSimulator simulator,
        SimulatedCombatState combat,
        Player player)
    {
        CombatSolver.Engine.InCombat.Mirrors.Hooks.Resources.AfterEnergyResetLateMirrors.Seal();
        foreach (BoundPhylactery relic in combat.RelicsOf(player)
                     .OfType<BoundPhylactery>()
                     .Where(static relic => !relic.IsMelted))
        {
            if (combat.GetPlayerTurnNumber(player) != 1)
                combat.SummonOsty(simulator, player, relic.DynamicVars.Summon.IntValue);
            if (simulator.HasPendingChoice)
                return;
        }

        CombatSolver.Engine.InCombat.Mirrors.Hooks.Resources.AfterEnergyResetLateMirrorContext? context = null;
        foreach (MegaCrit.Sts2.Core.Models.AbstractModel listener in
                 ((CombatSolver.Engine.Common.ICombatPredictionHookListenerSource)combat)
                     .MirroredHookListeners)
        {
            if (listener.GetType().Assembly == typeof(MegaCrit.Sts2.Core.Models.AbstractModel).Assembly
                && !CombatSolver.Engine.InCombat.Mirrors.Hooks.Resources.AfterEnergyResetLateMirrors.HasExternalRegistrations)
            {
                continue;
            }
            if (listener is BoundPhylactery
                || !CombatSolver.Engine.InCombat.Mirrors.Hooks.Resources.AfterEnergyResetLateMirrors.HasOverride(listener))
            {
                continue;
            }
            context ??= new CombatSolver.Engine.InCombat.Mirrors.Hooks.Resources.AfterEnergyResetLateMirrorContext
            {
                Simulator = simulator,
                Player = player,
            };
            CombatSolver.Engine.InCombat.Mirrors.Hooks.Resources.AfterEnergyResetLateMirrors.Invoke(listener, context);
            if (simulator.HasPendingChoice)
                return;
        }
    }
}
