using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Powers.Mocks;
using MegaCrit.Sts2.Core.Models.Relics;
using CombatSolver.Engine.Common;
using CombatSolver.Engine.Common.Mirrors;
using CombatSolver.Engine.InCombat.Simulation;

namespace CombatSolver.Engine.InCombat.Mirrors.Hooks.Power;

using TryModifyRegistry = MethodMirrorRegistry<AbstractModel, ModifyPowerAmountReceivedMirrorContext, ModifyPowerAmountReceivedResult>;
using AfterModifyingRegistry = MethodMirrorRegistry<AbstractModel, AfterModifyingPowerAmountReceivedMirrorContext>;

internal readonly record struct ModifyPowerAmountReceivedResult(
    bool Modified,
    decimal Amount);

internal static class ModifyPowerAmountReceivedMirrors
{
    private static readonly MirrorMethodSpec TryModifyPowerAmountReceivedMethod = MirrorMethodSpec.Hook(
        nameof(AbstractModel.TryModifyPowerAmountReceived),
        [typeof(PowerModel), typeof(Creature), typeof(decimal), typeof(Creature), typeof(decimal).MakeByRefType()]);

    private static readonly MirrorMethodSpec AfterModifyingPowerAmountReceivedMethod = MirrorMethodSpec.Hook(
        nameof(AbstractModel.AfterModifyingPowerAmountReceived),
        [typeof(PowerModel)]);

    private static readonly TryModifyRegistry TryRegistry = CreateTryModifyRegistry();
    private static readonly AfterModifyingRegistry AfterRegistry = CreateAfterModifyingRegistry();
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

    public static void Register<TModel>(
        Func<TModel, ModifyPowerAmountReceivedMirrorContext, ModifyPowerAmountReceivedResult> tryModifyHandler,
        Action<TModel, AfterModifyingPowerAmountReceivedMirrorContext>? afterModifyingHandler = null)
        where TModel : AbstractModel
    {
        ArgumentNullException.ThrowIfNull(tryModifyHandler);
        Type modelType = typeof(TModel);
        if (modelType.IsAbstract)
            throw new ArgumentException("ModifyPowerAmountReceived mirrors require a concrete runtime model type.");
        lock (RegistrationLock)
        {
            if (_sealed)
                throw new InvalidOperationException("ModifyPowerAmountReceived mirrors must be registered before root capture or dispatch.");
            TryRegistry.Register(tryModifyHandler);
            if (afterModifyingHandler is not null)
                AfterRegistry.Register(afterModifyingHandler);
            else if (AfterRegistry.OverridesMethod(modelType))
                AfterRegistry.RegisterIgnored<TModel>();
            Volatile.Write(ref _hasExternalRegistrations, true);
        }
    }

    public static void Register(
        Type modelType,
        Func<AbstractModel, ModifyPowerAmountReceivedMirrorContext, ModifyPowerAmountReceivedResult> tryModifyHandler,
        Action<AbstractModel, AfterModifyingPowerAmountReceivedMirrorContext>? afterModifyingHandler = null)
    {
        ArgumentNullException.ThrowIfNull(modelType);
        ArgumentNullException.ThrowIfNull(tryModifyHandler);
        if (modelType.IsAbstract)
            throw new ArgumentException("ModifyPowerAmountReceived mirrors require a concrete runtime model type.", nameof(modelType));
        lock (RegistrationLock)
        {
            if (_sealed)
                throw new InvalidOperationException("ModifyPowerAmountReceived mirrors must be registered before root capture or dispatch.");
            ThirdPartyMirrorRegistration.RegisterResult(TryRegistry, modelType, tryModifyHandler);
            if (afterModifyingHandler is not null)
                ThirdPartyMirrorRegistration.Register(AfterRegistry, modelType, afterModifyingHandler);
            else if (AfterRegistry.OverridesMethod(modelType))
                AfterRegistry.RegisterIgnored(modelType);
            Volatile.Write(ref _hasExternalRegistrations, true);
        }
    }

    public static void RegisterIgnored(Type modelType)
    {
        ArgumentNullException.ThrowIfNull(modelType);
        lock (RegistrationLock)
        {
            if (_sealed)
                throw new InvalidOperationException("ModifyPowerAmountReceived mirrors must be registered before root capture or dispatch.");
            bool overridesTry = TryRegistry.OverridesMethod(modelType);
            bool overridesAfter = AfterRegistry.OverridesMethod(modelType);
            if (!overridesTry && !overridesAfter)
            {
                throw new InvalidOperationException(
                    $"{modelType.FullName} overrides neither TryModifyPowerAmountReceived nor AfterModifyingPowerAmountReceived, so it cannot be ignored.");
            }
            if (overridesTry)
                TryRegistry.ValidateIgnoredRegistration(modelType);
            if (overridesAfter)
                AfterRegistry.ValidateIgnoredRegistration(modelType);
            if (overridesTry)
                TryRegistry.RegisterIgnored(modelType);
            if (overridesAfter)
                AfterRegistry.RegisterIgnored(modelType);
            Volatile.Write(ref _hasExternalRegistrations, true);
        }
    }

    internal static bool HasOverride(AbstractModel model)
        => TryRegistry.ResolveDispatchKind(model) != MirrorDispatchKind.NotOverridden
            || AfterRegistry.ResolveDispatchKind(model) != MirrorDispatchKind.NotOverridden;

    internal static bool TryModify(
        AbstractModel listener,
        ModifyPowerAmountReceivedMirrorContext context,
        out decimal modifiedAmount)
    {
        Seal();
        MirrorDispatchResult<ModifyPowerAmountReceivedResult> result = TryRegistry.Invoke(
            listener,
            context,
            new ModifyPowerAmountReceivedResult(false, context.Amount));
        if (result.Kind == MirrorDispatchKind.Unsupported)
        {
            throw PredictionUnsupportedException.ForContent(
                $"No TryModifyPowerAmountReceived mirror is registered for {listener.GetType().FullName}.",
                listener.GetType());
        }
        modifiedAmount = result.Value.Modified ? result.Value.Amount : context.Amount;
        return result.Value.Modified;
    }

    internal static void AfterModifying(
        AbstractModel listener,
        AfterModifyingPowerAmountReceivedMirrorContext context)
    {
        Seal();
        if (AfterRegistry.Invoke(listener, context).Kind == MirrorDispatchKind.Unsupported)
        {
            throw PredictionUnsupportedException.ForContent(
                $"No AfterModifyingPowerAmountReceived mirror is registered for {listener.GetType().FullName}.",
                listener.GetType());
        }
    }

    private static TryModifyRegistry CreateTryModifyRegistry()
    {
        var registry = new TryModifyRegistry(TryModifyPowerAmountReceivedMethod);
        registry.Register<ArtifactPower>(static (_, context) => new ModifyPowerAmountReceivedResult(false, context.Amount));
        registry.Register<RuinedHelmet>(static (_, context) => new ModifyPowerAmountReceivedResult(false, context.Amount));
        registry.RegisterIgnored<MockRevivePower>();
        return registry;
    }

    private static AfterModifyingRegistry CreateAfterModifyingRegistry()
    {
        var registry = new AfterModifyingRegistry(AfterModifyingPowerAmountReceivedMethod);
        registry.RegisterIgnored<ArtifactPower>();
        registry.RegisterIgnored<RuinedHelmet>();
        return registry;
    }
}

internal sealed class ModifyPowerAmountReceivedMirrorContext : IMethodMirrorContext<AbstractModel>
{
    public required SimulatedCombatState Combat { get; init; }
    public required PowerModel Power { get; init; }
    public required Creature Target { get; init; }
    public required Creature? Applier { get; init; }
    public required decimal Amount { get; set; }
    public required CardModel? CardSource { get; init; }
    public CombatPredictionSimulator? Simulator => CombatPredictionContext.ActiveSimulation;
    public bool TargetIsPlayer => Target.IsPlayer;

    PredictionTrace.TraceScope IMethodMirrorContext<AbstractModel>.PushDispatchSource(
        AbstractModel receiver,
        MirrorMethodSpec method)
        => Simulator is { } simulator ? simulator.PushMethodSource(receiver, method) : default;

    void IMethodMirrorContext<AbstractModel>.RecordMethodNotMirroredRisk()
        => Simulator?.History.RecordRisk(PredictionRiskReason.MethodNotMirrored);

    void IMethodMirrorContext<AbstractModel>.RecordMethodMirrorIncompleteRisk()
        => Simulator?.History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);
}

internal sealed class AfterModifyingPowerAmountReceivedMirrorContext : IMethodMirrorContext<AbstractModel>
{
    public required SimulatedCombatState Combat { get; init; }
    public required PowerModel Power { get; init; }
    public required Creature Target { get; init; }
    public required Creature? Applier { get; init; }
    public required decimal ModifiedAmount { get; init; }
    public CombatPredictionSimulator? Simulator => CombatPredictionContext.ActiveSimulation;

    PredictionTrace.TraceScope IMethodMirrorContext<AbstractModel>.PushDispatchSource(
        AbstractModel receiver,
        MirrorMethodSpec method)
        => Simulator is { } simulator ? simulator.PushMethodSource(receiver, method) : default;

    void IMethodMirrorContext<AbstractModel>.RecordMethodNotMirroredRisk()
        => Simulator?.History.RecordRisk(PredictionRiskReason.MethodNotMirrored);

    void IMethodMirrorContext<AbstractModel>.RecordMethodMirrorIncompleteRisk()
        => Simulator?.History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);
}
