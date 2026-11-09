using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CombatSolver.Engine.Mirrors;
using CombatSolver.Search;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace CombatSolver.HextechAdapter;

[ModInitializer(nameof(Initialize))]
public static class HextechAdapterEntry
{
    public const string ModId = "CombatSolver.HextechAdapter";
    public const string HextechAssemblyName = "HextechRunes";

    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized)
            return;
        _initialized = true;

        var hextechAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => string.Equals(a.GetName().Name, HextechAssemblyName, StringComparison.OrdinalIgnoreCase));

        if (hextechAssembly == null)
        {
            CombatSolver.Entry.Logger.Info($"[{ModId}] HextechRunes assembly not loaded; skipping adapter registration.");
            return;
        }

        InitializeForAssembly(hextechAssembly);
    }

    public static void InitializeForAssembly(Assembly hextechAssembly)
    {
        ArgumentNullException.ThrowIfNull(hextechAssembly);

        var harmony = new Harmony(ModId);
        HextechRuntimeAutoBinder.RegisterAll(hextechAssembly);
        HextechStateAutoBinder.RegisterAll(hextechAssembly);
        HextechSimulationContextPatches.Apply(harmony, hextechAssembly);
        HextechVanillaOnPlayAdapters.RegisterAll(hextechAssembly);

        CombatSolver.Entry.Logger.Info($"[{ModId}] Registered HextechRunes adapter pillars for {hextechAssembly.GetName().Name}.");
    }
}

/// <summary>
/// Pillar 1 — Forwarded Hook Unwrapping &amp; Inertness Auto-Binder.
/// Resolves HextechRelicBase, HextechPowerBase, and HextechModifierBase from the loaded
/// HextechRunes assembly, registers all sealed-override-to-virtual hook forwarders via
/// ThirdPartyMirrorRegistration.RegisterForwardedHook, and registers all concrete HextechRunes
/// PowerModel subclasses with PowerDynamicVarWarmup.RegisterAdaptedCanonicalPower.
/// </summary>
public static class HextechRuntimeAutoBinder
{
    private const BindingFlags DeclaredInstanceFlags =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    public static void RegisterAll(Assembly hextechAssembly)
    {
        ArgumentNullException.ThrowIfNull(hextechAssembly);

        RegisterRelicBaseForwarders(hextechAssembly);
        RegisterPowerBaseForwarders(hextechAssembly);
        RegisterModifierBaseForwarders(hextechAssembly);
        RegisterAdaptedCanonicalPowers(hextechAssembly);
    }

    private static void RegisterRelicBaseForwarders(Assembly hextechAssembly)
    {
        var relicBase = ResolveType(hextechAssembly, "HextechRunes.HextechRelicBase");
        if (relicBase == null)
            return;

        // BeforeSideTurnStart(PlayerChoiceContext, CombatSide, IReadOnlyList<Creature>, ICombatState)
        //   -> BeforeSideTurnStart(PlayerChoiceContext, CombatSide, ICombatState)
        RegisterForwarderIfPresent(
            relicBase,
            "BeforeSideTurnStart",
            new[] { typeof(PlayerChoiceContext), typeof(CombatSide), typeof(IReadOnlyList<Creature>), typeof(ICombatState) },
            "BeforeSideTurnStart",
            new[] { typeof(PlayerChoiceContext), typeof(CombatSide), typeof(ICombatState) });

        // AfterSideTurnStart(CombatSide, IReadOnlyList<Creature>, ICombatState)
        //   -> AfterSideTurnStart(CombatSide, ICombatState)
        RegisterForwarderIfPresent(
            relicBase,
            "AfterSideTurnStart",
            new[] { typeof(CombatSide), typeof(IReadOnlyList<Creature>), typeof(ICombatState) },
            "AfterSideTurnStart",
            new[] { typeof(CombatSide), typeof(ICombatState) });

        // BeforeSideTurnEnd(PlayerChoiceContext, CombatSide, IEnumerable<Creature>)
        //   -> BeforeTurnEnd(PlayerChoiceContext, CombatSide)
        RegisterForwarderIfPresent(
            relicBase,
            "BeforeSideTurnEnd",
            new[] { typeof(PlayerChoiceContext), typeof(CombatSide), typeof(IEnumerable<Creature>) },
            "BeforeTurnEnd",
            new[] { typeof(PlayerChoiceContext), typeof(CombatSide) });

        // AfterSideTurnEnd(PlayerChoiceContext, CombatSide, IEnumerable<Creature>)
        //   -> AfterTurnEnd(PlayerChoiceContext, CombatSide)
        RegisterForwarderIfPresent(
            relicBase,
            "AfterSideTurnEnd",
            new[] { typeof(PlayerChoiceContext), typeof(CombatSide), typeof(IEnumerable<Creature>) },
            "AfterTurnEnd",
            new[] { typeof(PlayerChoiceContext), typeof(CombatSide) });

        // ModifyDamageAdditive(Creature?, decimal, ValueProp, Creature?, CardModel?, CardPlay?)
        //   -> ModifyDamageAdditiveCompat(Creature?, decimal, ValueProp, Creature?, CardModel?)
        RegisterForwarderIfPresent(
            relicBase,
            "ModifyDamageAdditive",
            new[] { typeof(Creature), typeof(decimal), typeof(ValueProp), typeof(Creature), typeof(CardModel), typeof(CardPlay) },
            "ModifyDamageAdditiveCompat",
            new[] { typeof(Creature), typeof(decimal), typeof(ValueProp), typeof(Creature), typeof(CardModel) });

        // ModifyDamageMultiplicative(Creature?, decimal, ValueProp, Creature?, CardModel?, CardPlay?)
        //   -> ModifyDamageMultiplicativeCompat(Creature?, decimal, ValueProp, Creature?, CardModel?)
        RegisterForwarderIfPresent(
            relicBase,
            "ModifyDamageMultiplicative",
            new[] { typeof(Creature), typeof(decimal), typeof(ValueProp), typeof(Creature), typeof(CardModel), typeof(CardPlay) },
            "ModifyDamageMultiplicativeCompat",
            new[] { typeof(Creature), typeof(decimal), typeof(ValueProp), typeof(Creature), typeof(CardModel) });

        // ModifyCardPlayResultLocation(CardModel, bool, ResourceInfo, CardLocation)
        //   -> ModifyCardPlayResultPileTypeAndPositionCompat / ModifyCardPlayResultLocationCompat
        RegisterForwarderByNameFallback(
            relicBase,
            "ModifyCardPlayResultLocation",
            new[] { "ModifyCardPlayResultPileTypeAndPositionCompat", "ModifyCardPlayResultLocationCompat" });

        // AfterModifyingCardPlayResultLocation(CardModel, CardLocation)
        //   -> AfterModifyingCardPlayResultPileOrPositionCompat / AfterModifyingCardPlayResultLocationCompat
        RegisterForwarderByNameFallback(
            relicBase,
            "AfterModifyingCardPlayResultLocation",
            new[] { "AfterModifyingCardPlayResultPileOrPositionCompat", "AfterModifyingCardPlayResultLocationCompat" });
    }

    private static void RegisterPowerBaseForwarders(Assembly hextechAssembly)
    {
        var powerBase = ResolveType(hextechAssembly, "HextechRunes.HextechPowerBase");
        if (powerBase == null)
            return;

        // BeforeSideTurnStart(PlayerChoiceContext, CombatSide, IReadOnlyList<Creature>, ICombatState)
        //   -> BeforeSideTurnStart(PlayerChoiceContext, CombatSide, ICombatState)
        RegisterForwarderIfPresent(
            powerBase,
            "BeforeSideTurnStart",
            new[] { typeof(PlayerChoiceContext), typeof(CombatSide), typeof(IReadOnlyList<Creature>), typeof(ICombatState) },
            "BeforeSideTurnStart",
            new[] { typeof(PlayerChoiceContext), typeof(CombatSide), typeof(ICombatState) });

        // AfterSideTurnStart(CombatSide, IReadOnlyList<Creature>, ICombatState)
        //   -> AfterSideTurnStart(CombatSide, ICombatState)
        RegisterForwarderIfPresent(
            powerBase,
            "AfterSideTurnStart",
            new[] { typeof(CombatSide), typeof(IReadOnlyList<Creature>), typeof(ICombatState) },
            "AfterSideTurnStart",
            new[] { typeof(CombatSide), typeof(ICombatState) });

        // BeforeSideTurnEnd(PlayerChoiceContext, CombatSide, IEnumerable<Creature>)
        //   -> BeforeTurnEnd(PlayerChoiceContext, CombatSide)
        RegisterForwarderIfPresent(
            powerBase,
            "BeforeSideTurnEnd",
            new[] { typeof(PlayerChoiceContext), typeof(CombatSide), typeof(IEnumerable<Creature>) },
            "BeforeTurnEnd",
            new[] { typeof(PlayerChoiceContext), typeof(CombatSide) });

        // AfterSideTurnEnd(PlayerChoiceContext, CombatSide, IEnumerable<Creature>)
        //   -> AfterTurnEnd(PlayerChoiceContext, CombatSide)
        RegisterForwarderIfPresent(
            powerBase,
            "AfterSideTurnEnd",
            new[] { typeof(PlayerChoiceContext), typeof(CombatSide), typeof(IEnumerable<Creature>) },
            "AfterTurnEnd",
            new[] { typeof(PlayerChoiceContext), typeof(CombatSide) });

        // ModifyDamageAdditive (if declared on HextechPowerBase) -> ModifyDamageAdditiveCompat
        RegisterForwarderIfPresent(
            powerBase,
            "ModifyDamageAdditive",
            new[] { typeof(Creature), typeof(decimal), typeof(ValueProp), typeof(Creature), typeof(CardModel), typeof(CardPlay) },
            "ModifyDamageAdditiveCompat",
            new[] { typeof(Creature), typeof(decimal), typeof(ValueProp), typeof(Creature), typeof(CardModel) });

        // ModifyDamageMultiplicative(Creature?, decimal, ValueProp, Creature?, CardModel?, CardPlay?)
        //   -> ModifyDamageMultiplicativeCompat(Creature?, decimal, ValueProp, Creature?, CardModel?)
        RegisterForwarderIfPresent(
            powerBase,
            "ModifyDamageMultiplicative",
            new[] { typeof(Creature), typeof(decimal), typeof(ValueProp), typeof(Creature), typeof(CardModel), typeof(CardPlay) },
            "ModifyDamageMultiplicativeCompat",
            new[] { typeof(Creature), typeof(decimal), typeof(ValueProp), typeof(Creature), typeof(CardModel) });
    }

    private static void RegisterModifierBaseForwarders(Assembly hextechAssembly)
    {
        var modifierBase = ResolveType(hextechAssembly, "HextechRunes.HextechModifierBase");
        if (modifierBase == null)
            return;

        RegisterForwarderIfPresent(
            modifierBase,
            "ModifyDamageAdditive",
            new[] { typeof(Creature), typeof(decimal), typeof(ValueProp), typeof(Creature), typeof(CardModel), typeof(CardPlay) },
            "ModifyDamageAdditiveCompat",
            new[] { typeof(Creature), typeof(decimal), typeof(ValueProp), typeof(Creature), typeof(CardModel) });

        RegisterForwarderIfPresent(
            modifierBase,
            "ModifyDamageMultiplicative",
            new[] { typeof(Creature), typeof(decimal), typeof(ValueProp), typeof(Creature), typeof(CardModel), typeof(CardPlay) },
            "ModifyDamageMultiplicativeCompat",
            new[] { typeof(Creature), typeof(decimal), typeof(ValueProp), typeof(Creature), typeof(CardModel) });

        RegisterForwarderByNameFallback(
            modifierBase,
            "ModifyCardPlayResultLocation",
            new[] { "ModifyCardPlayResultPileTypeAndPositionCompat", "ModifyCardPlayResultLocationCompat" });

        RegisterForwarderByNameFallback(
            modifierBase,
            "AfterModifyingCardPlayResultLocation",
            new[] { "AfterModifyingCardPlayResultPileOrPositionCompat", "AfterModifyingCardPlayResultLocationCompat" });
    }

    private static void RegisterAdaptedCanonicalPowers(Assembly hextechAssembly)
    {
        foreach (var type in GetLoadableTypes(hextechAssembly))
        {
            if (type is { IsClass: true, IsAbstract: false } && typeof(PowerModel).IsAssignableFrom(type))
            {
                PowerDynamicVarWarmup.RegisterAdaptedCanonicalPower(type);
            }
        }
    }

    private static void RegisterForwarderIfPresent(
        Type wrapperBaseType,
        string nativeMethodName,
        Type[] nativeParamTypes,
        string forwardedMethodName,
        Type[] forwardedParamTypes)
    {
        var nativeMethod = wrapperBaseType.GetMethod(nativeMethodName, DeclaredInstanceFlags, null, nativeParamTypes, null);
        var forwardedMethod = wrapperBaseType.GetMethod(forwardedMethodName, DeclaredInstanceFlags, null, forwardedParamTypes, null);

        if (nativeMethod != null && forwardedMethod != null)
        {
            ThirdPartyMirrorRegistration.RegisterForwardedHook(wrapperBaseType, nativeMethod, forwardedMethod);
        }
    }

    private static void RegisterForwarderByNameFallback(
        Type wrapperBaseType,
        string nativeMethodName,
        IReadOnlyList<string> candidateForwardedNames)
    {
        var nativeMethod = wrapperBaseType.GetMethods(DeclaredInstanceFlags)
            .FirstOrDefault(m => m.Name == nativeMethodName && m.GetBaseDefinition() != m);
        if (nativeMethod == null)
            return;

        foreach (var candidateName in candidateForwardedNames)
        {
            var forwardedMethod = wrapperBaseType.GetMethods(DeclaredInstanceFlags)
                .FirstOrDefault(m => m.Name == candidateName && m.IsVirtual);
            if (forwardedMethod != null)
            {
                ThirdPartyMirrorRegistration.RegisterForwardedHook(wrapperBaseType, nativeMethod, forwardedMethod);
                return;
            }
        }
    }

    internal static Type? ResolveType(Assembly assembly, string fullName)
    {
        var direct = assembly.GetType(fullName, throwOnError: false, ignoreCase: false);
        if (direct != null)
            return direct;

        var shortName = fullName.Contains('.')
            ? fullName[(fullName.LastIndexOf('.') + 1)..]
            : fullName;

        return GetLoadableTypes(assembly).FirstOrDefault(t => t.FullName == fullName || t.Name == shortName);
    }

    internal static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t != null)!;
        }
    }
}
