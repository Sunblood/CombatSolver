using System;
using MegaCrit.Sts2.Core.Combat;

namespace CombatSolver.Engine.InCombat.Simulation;

internal static class CombatPredictionContext
{
    [ThreadStatic]
    private static CombatPredictionSimulator? _activeSimulator;

    [ThreadStatic]
    private static ICombatState? _activeCombatState;

    public static CombatPredictionSimulator? ActiveSimulation => _activeSimulator;

    public static ICombatState? ActiveCombatState => _activeSimulator?.State.CombatState ?? _activeCombatState;

    public static bool IsSimulating => _activeSimulator is not null || _activeCombatState is not null;

    internal static Scope Enter(CombatPredictionSimulator? simulator)
    {
        return new Scope(simulator, simulator?.State.CombatState);
    }

    internal static Scope Enter(ICombatState? combatState)
    {
        return new Scope(_activeSimulator, combatState);
    }

    internal readonly struct Scope : IDisposable
    {
        private readonly CombatPredictionSimulator? _previousSimulator;
        private readonly ICombatState? _previousCombatState;

        internal Scope(CombatPredictionSimulator? simulator, ICombatState? combatState)
        {
            _previousSimulator = _activeSimulator;
            _previousCombatState = _activeCombatState;
            _activeSimulator = simulator;
            _activeCombatState = combatState;
        }

        public void Dispose()
        {
            _activeSimulator = _previousSimulator;
            _activeCombatState = _previousCombatState;
        }
    }
}
