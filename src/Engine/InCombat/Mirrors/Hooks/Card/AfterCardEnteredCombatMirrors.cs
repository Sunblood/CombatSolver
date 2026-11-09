using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Afflictions;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using CombatSolver.Engine.Common;
using CombatSolver.Engine.Common.Mirrors;

namespace CombatSolver.Engine.InCombat.Mirrors.Hooks.Card;

using Registry = MethodMirrorRegistry<AbstractModel, AfterCardEnteredCombatMirrorContext>;

internal static class AfterCardEnteredCombatMirrors
{
    private static readonly MirrorMethodSpec AfterCardEnteredCombat = MirrorMethodSpec.Hook(
        nameof(AbstractModel.AfterCardEnteredCombat),
        [typeof(CardModel)]);

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

    public static void Register<TModel>(Action<TModel, AfterCardEnteredCombatMirrorContext> handler)
        where TModel : AbstractModel
    {
        ArgumentNullException.ThrowIfNull(handler);
        if (typeof(TModel).IsAbstract)
            throw new ArgumentException("AfterCardEnteredCombat mirrors require a concrete runtime model type.");
        lock (RegistrationLock)
        {
            if (_sealed)
                throw new InvalidOperationException("AfterCardEnteredCombat mirrors must be registered before root capture or dispatch.");
            Registry.Register(handler);
            Volatile.Write(ref _hasExternalRegistrations, true);
        }
    }

    public static void Register(
        Type modelType,
        Action<AbstractModel, AfterCardEnteredCombatMirrorContext> handler)
    {
        ArgumentNullException.ThrowIfNull(modelType);
        ArgumentNullException.ThrowIfNull(handler);
        if (modelType.IsAbstract)
            throw new ArgumentException("AfterCardEnteredCombat mirrors require a concrete runtime model type.", nameof(modelType));
        lock (RegistrationLock)
        {
            if (_sealed)
                throw new InvalidOperationException("AfterCardEnteredCombat mirrors must be registered before root capture or dispatch.");
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
                throw new InvalidOperationException("AfterCardEnteredCombat mirrors must be registered before root capture or dispatch.");
            Registry.RegisterIgnored(modelType);
            Volatile.Write(ref _hasExternalRegistrations, true);
        }
    }

    internal static bool HasOverride(AbstractModel model)
        => Registry.ResolveDispatchKind(model) != MirrorDispatchKind.NotOverridden;

    internal static void Invoke(AbstractModel listener, AfterCardEnteredCombatMirrorContext context)
    {
        Seal();
        using var dispatch = context.Simulator.BeginExecutionDispatch();
        Registry.Invoke(listener, context);
    }

    private static Registry CreateRegistry()
    {
        var registry = new Registry(AfterCardEnteredCombat);

        registry.Register<PhantomBladesPower>(static (power, context) =>
            PhantomBladesPowerMirrors.AfterCardEnteredCombat(power, context.Card));
        registry.Register<GhostSeed>(static (relic, context) =>
        {
            if (!relic.IsMelted)
                GhostSeedMirrors.AfterCardEnteredCombat(relic, context.Card);
        });

        // Vanilla card, power, and affliction entry adjustments are settled in SimulatedCombatState.AfterCardEnteredCombat
        // and NormalizeCardAfflictions / NormalizePowerCardState.
        registry.Register<Flatten>(static (_, _) => { });
        registry.Register<BansheesCry>(static (_, _) => { });
        registry.Register<Pinpoint>(static (_, _) => { });
        registry.Register<Stomp>(static (_, _) => { });
        registry.Register<Hexed>(static (_, _) => { });
        registry.Register<GalvanicPower>(static (_, _) => { });
        registry.Register<HexPower>(static (_, _) => { });
        registry.Register<RingingPower>(static (_, _) => { });
        registry.Register<SmoggyPower>(static (_, _) => { });
        registry.Register<SwordSagePower>(static (_, _) => { });
        registry.Register<TangledPower>(static (_, _) => { });
        registry.Register<VitalSparkPower>(static (_, _) => { });

        registry.RegisterIgnored<Midnight>();

        return registry;
    }
}

internal sealed class AfterCardEnteredCombatMirrorContext : CombatCardMirrorContext
{
    public SimulatedCombatState Combat => CombatState as SimulatedCombatState
        ?? throw new InvalidOperationException("AfterCardEnteredCombat requires branch combat state.");
}
