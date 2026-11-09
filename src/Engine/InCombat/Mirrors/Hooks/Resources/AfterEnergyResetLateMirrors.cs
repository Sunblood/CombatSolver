using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using CombatSolver.Engine.Common;
using CombatSolver.Engine.Common.Mirrors;
using CombatSolver.Engine.InCombat.Simulation;

namespace CombatSolver.Engine.InCombat.Mirrors.Hooks.Resources;

using Registry = MethodMirrorRegistry<AbstractModel, AfterEnergyResetLateMirrorContext>;

internal static class AfterEnergyResetLateMirrors
{
    private static readonly MirrorMethodSpec AfterEnergyResetLate = MirrorMethodSpec.Hook(
        nameof(AbstractModel.AfterEnergyResetLate),
        [typeof(Player)]);

    private static readonly Registry Registry = CreateRegistry();
    private static readonly object RegistrationLock = new();
    private static bool _sealed;
    private static bool _hasExternalRegistrations;

    internal static bool HasExternalRegistrations => Volatile.Read(ref _hasExternalRegistrations);

    internal static void Seal()
    {
        if (Volatile.Read(ref _sealed))
            return;
        lock (RegistrationLock)
            Volatile.Write(ref _sealed, true);
    }

    public static void Register<TModel>(Action<TModel, AfterEnergyResetLateMirrorContext> handler)
        where TModel : AbstractModel
    {
        ArgumentNullException.ThrowIfNull(handler);
        if (typeof(TModel).IsAbstract)
            throw new ArgumentException("AfterEnergyResetLate mirrors require a concrete runtime model type.");
        lock (RegistrationLock)
        {
            if (_sealed)
                throw new InvalidOperationException("AfterEnergyResetLate mirrors must be registered before root capture or dispatch.");
            Registry.Register(handler);
            Volatile.Write(ref _hasExternalRegistrations, true);
        }
    }

    public static void Register(
        Type modelType,
        Action<AbstractModel, AfterEnergyResetLateMirrorContext> handler)
    {
        ArgumentNullException.ThrowIfNull(modelType);
        ArgumentNullException.ThrowIfNull(handler);
        if (modelType.IsAbstract)
            throw new ArgumentException("AfterEnergyResetLate mirrors require a concrete runtime model type.", nameof(modelType));
        lock (RegistrationLock)
        {
            if (_sealed)
                throw new InvalidOperationException("AfterEnergyResetLate mirrors must be registered before root capture or dispatch.");
            ThirdPartyMirrorRegistration.Register(Registry, modelType, handler);
            Volatile.Write(ref _hasExternalRegistrations, true);
        }
    }

    public static void RegisterIgnored(Type modelType)
    {
        ArgumentNullException.ThrowIfNull(modelType);
        lock (RegistrationLock)
        {
            if (_sealed)
                throw new InvalidOperationException("AfterEnergyResetLate mirrors must be registered before root capture or dispatch.");
            Registry.RegisterIgnored(modelType);
            Volatile.Write(ref _hasExternalRegistrations, true);
        }
    }

    internal static bool HasOverride(AbstractModel model)
        => Registry.ResolveDispatchKind(model) != MirrorDispatchKind.NotOverridden;

    public static void Invoke(AbstractModel listener, AfterEnergyResetLateMirrorContext context)
    {
        Seal();
        Registry.Invoke(listener, context);
    }

    private static Registry CreateRegistry()
    {
        var registry = new Registry(AfterEnergyResetLate);
        registry.Register<BoundPhylactery>(static (relic, context) =>
        {
            if (relic.IsMelted || !ReferenceEquals(relic.Owner, context.Player))
                return;
            if (context.Combat.GetPlayerTurnNumber(context.Player) != 1)
                context.Combat.SummonOsty(context.Simulator, context.Player, relic.DynamicVars.Summon.IntValue);
        });
        return registry;
    }
}

internal sealed class AfterEnergyResetLateMirrorContext : CombatMirrorContext
{
    public required Player Player { get; init; }

    public SimulatedCombatState Combat => CombatState as SimulatedCombatState
        ?? throw new InvalidOperationException("AfterEnergyResetLate requires branch combat state.");
}
