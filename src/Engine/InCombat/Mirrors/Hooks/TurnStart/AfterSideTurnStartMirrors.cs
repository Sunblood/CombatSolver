using CombatSolver.Engine.Common;
using CombatSolver.Engine.Common.Mirrors;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace CombatSolver.Engine.InCombat.Mirrors.Hooks.TurnStart;

using Registry = MethodMirrorRegistry<AbstractModel, AfterSideTurnStartMirrorContext>;

internal static partial class AfterSideTurnStartMirrors
{
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

    public static void Register<TModel>(Action<TModel, AfterSideTurnStartMirrorContext> handler)
        where TModel : AbstractModel
    {
        ArgumentNullException.ThrowIfNull(handler);
        if (typeof(TModel).IsAbstract)
            throw new ArgumentException("Turn-phase mirrors require a concrete runtime model type.");
        lock (RegistrationLock)
        {
            if (_sealed)
                throw new InvalidOperationException("Turn-phase mirrors must be registered before root capture or dispatch.");
            Registry.Register(handler);
            Volatile.Write(ref _hasExternalRegistrations, true);
        }
    }

    public static void Register(
        Type modelType,
        Action<AbstractModel, AfterSideTurnStartMirrorContext> handler)
    {
        ArgumentNullException.ThrowIfNull(modelType);
        ArgumentNullException.ThrowIfNull(handler);
        if (modelType.IsAbstract)
            throw new ArgumentException("Turn-phase mirrors require a concrete runtime model type.", nameof(modelType));
        lock (RegistrationLock)
        {
            if (_sealed)
                throw new InvalidOperationException("Turn-phase mirrors must be registered before root capture or dispatch.");
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
                throw new InvalidOperationException("Turn-phase mirrors must be registered before root capture or dispatch.");
            Registry.RegisterIgnored(modelType);
            Volatile.Write(ref _hasExternalRegistrations, true);
        }
    }

    internal static bool HasOverride(AbstractModel model)
        => Registry.ResolveDispatchKind(model) != MirrorDispatchKind.NotOverridden;

    internal static void Invoke(AbstractModel listener, AfterSideTurnStartMirrorContext context)
    {
        Seal();
        if (Registry.Invoke(listener, context).Kind == MirrorDispatchKind.Unsupported)
        {
            throw PredictionUnsupportedException.ForContent(
                $"No AfterSideTurnStart mirror is registered for {listener.GetType().FullName}.",
                listener.GetType());
        }
    }

    private static partial void RegisterVanilla(Registry registry);

    private static Registry CreateRegistry()
    {
        var registry = new Registry(MirrorMethodSpec.Hook(
            nameof(AbstractModel.AfterSideTurnStart),
            [typeof(CombatSide), typeof(IReadOnlyList<Creature>), typeof(ICombatState)]));
        RegisterVanilla(registry);
        return registry;
    }
}

internal sealed class AfterSideTurnStartMirrorContext : CombatMirrorContext
{
    public required CombatSide Side { get; init; }
    public required IReadOnlyList<Creature> Participants { get; init; }

    public SimulatedCombatState Combat => CombatState as SimulatedCombatState
        ?? throw new InvalidOperationException("AfterSideTurnStart requires branch combat state.");
}
