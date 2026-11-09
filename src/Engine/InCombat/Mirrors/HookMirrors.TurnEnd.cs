using CombatSolver.Engine.Common;
using CombatSolver.Engine.InCombat.Mirrors.Hooks.TurnEnd;
using CombatSolver.Engine.InCombat.Simulation;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace CombatSolver.Engine.InCombat.Mirrors;

internal static partial class HookMirrors
{
    // The late pass takes a fresh listener snapshot after the regular pass, as Hook.AfterSideTurnEnd does.
    public static bool AfterSideTurnEndLate(
        CombatPredictionSimulator simulator, CombatSide side, IReadOnlyList<Creature> participants)
    {
        AfterSideTurnEndLateMirrors.Seal();
        if (simulator.HasPendingChoice)
            return false;

        CardHookReceiver? first = null;
        List<CardHookReceiver>? remaining = null;
        foreach (AbstractModel listener in IterateCombatHookListeners(simulator, MirroredHookMask.AfterSideTurnEndLate))
        {
            PredictedCard? card = listener is CardModel model
                ? simulator.State.GetPlayerCombatState(model.Owner).FindCard(model)
                : null;
            var receiver = new CardHookReceiver(listener, card);
            // A lone vanilla late hook needs no heap-allocated receiver collection.
            if (first is null)
                first = receiver;
            else
                (remaining ??= []).Add(receiver);
        }
        if (first is null)
            return true;

        var context = new AfterSideTurnEndLateMirrorContext
        {
            Simulator = simulator,
            Side = side,
            Participants = participants
        };
        // Membership stays fixed even if a callback changes the roster or card previews.
        // Terminal checks belong to the phase boundary, not between already-selected listeners.
        AfterSideTurnEndLateMirrors.Invoke(first.Value.Current, context);
        if (simulator.HasPendingChoice)
            return false;
        if (remaining is null)
            return true;
        foreach (CardHookReceiver receiver in remaining)
        {
            AfterSideTurnEndLateMirrors.Invoke(receiver.Current, context);
            if (simulator.HasPendingChoice)
                return false;
        }
        return true;
    }

    public static bool AfterSideTurnEndExternal(
        CombatPredictionSimulator simulator, CombatSide side, IReadOnlyList<Creature> participants)
    {
        AfterSideTurnEndMirrors.Seal();
        if (simulator.HasPendingChoice)
            return false;

        bool hasExternal = AfterSideTurnEndMirrors.HasExternalRegistrations;
        if (!hasExternal)
        {
            foreach (AbstractModel listener in IterateCombatHookListeners(simulator, MirroredHookMask.AfterSideTurnEndLate))
            {
                if (listener.GetType().Assembly != typeof(AbstractModel).Assembly
                    && AfterSideTurnEndMirrors.HasOverride(listener))
                {
                    hasExternal = true;
                    break;
                }
            }
        }
        if (!hasExternal)
            return true;

        List<CardHookReceiver>? receivers = null;
        foreach (AbstractModel listener in IterateCombatHookListeners(simulator, MirroredHookMask.AfterSideTurnEndLate))
        {
            if (listener.GetType().Assembly == typeof(AbstractModel).Assembly)
                continue;
            PredictedCard? card = listener is CardModel model
                ? simulator.State.GetPlayerCombatState(model.Owner).FindCard(model)
                : null;
            (receivers ??= []).Add(new(listener, card));
        }
        if (receivers is null)
            return true;

        var context = new AfterSideTurnEndMirrorContext
        {
            Simulator = simulator,
            Side = side,
            Participants = participants
        };
        foreach (CardHookReceiver receiver in receivers)
        {
            AfterSideTurnEndMirrors.Invoke(receiver.Current, context);
            if (simulator.HasPendingChoice)
                return false;
        }
        return true;
    }
}
