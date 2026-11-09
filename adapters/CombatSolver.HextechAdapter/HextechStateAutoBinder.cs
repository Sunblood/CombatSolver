using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CombatSolver.Engine.Common;
using CombatSolver.Engine.Mirrors;
using CombatSolver.Search;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace CombatSolver.HextechAdapter;

/// <summary>
/// Pillar 2 — Reflection/Manifest State &amp; Continuation Auto-Binder.
/// Auto-binds mutable per-instance relic fields (_turnScopedTurnNumber, _procsThisTurn,
/// _cardsDrawnThisCombat, _triggeredThisTurn, and [SavedProperty] properties) into
/// ModelPredictionStateMirrors and PowerHiddenStateMirrors, and binds
/// HextechMayhemModifier.CombatTracking using HextechMayhemCombatTrackingSerializer.
/// </summary>
public static class HextechStateAutoBinder
{
    private const BindingFlags AllInstanceDeclaredFlags =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private static readonly string[] KnownRelicMutableFieldNames =
    {
        "_turnScopedTurnNumber",
        "_procsThisTurn",
        "_cardsDrawnThisCombat",
        "_triggeredThisTurn"
    };

    private static readonly MethodInfo RegisterRelicGenericMethod =
        typeof(HextechStateAutoBinder).GetMethod(nameof(RegisterConcreteRelicState), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo RegisterPowerFieldGenericMethod =
        typeof(HextechStateAutoBinder).GetMethod(nameof(RegisterPowerFieldAccessor), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo RegisterModifierGenericMethod =
        typeof(HextechStateAutoBinder).GetMethod(nameof(RegisterMayhemModifierTyped), BindingFlags.NonPublic | BindingFlags.Static)!;

    public static void RegisterAll(Assembly hextechAssembly)
    {
        ArgumentNullException.ThrowIfNull(hextechAssembly);

        foreach (var type in HextechRuntimeAutoBinder.GetLoadableTypes(hextechAssembly))
        {
            if (type is not { IsClass: true, IsAbstract: false })
                continue;

            if (typeof(RelicModel).IsAssignableFrom(type))
            {
                TryRegisterRelicType(type);
            }
            else if (typeof(PowerModel).IsAssignableFrom(type))
            {
                TryRegisterPowerType(type);
            }
        }

        TryRegisterMayhemModifier(hextechAssembly);
    }

    private static void TryRegisterRelicType(Type relicType)
    {
        var accessors = CollectRelicStateAccessors(relicType);
        if (accessors.Count == 0)
            return;

        RegisterRelicGenericMethod
            .MakeGenericMethod(relicType)
            .Invoke(null, new object[] { accessors });
    }

    private static void RegisterConcreteRelicState<TRelic>(IReadOnlyList<MemberStateAccessor> accessors)
        where TRelic : RelicModel
    {
        var schema = $"HextechAdapter.Relic.{typeof(TRelic).Name}.v1";
        ModelPredictionStateMirrors.RegisterRelic<TRelic, ReflectedValueBagState>(
            schema,
            (sim, relic) => CaptureBag(relic, accessors),
            (relic, ref writer) => WriteLiveBag(relic, accessors, ref writer),
            (state, ref writer) => state.WriteTo(ref writer));
    }

    private static List<MemberStateAccessor> CollectRelicStateAccessors(Type relicType)
    {
        var accessors = new List<MemberStateAccessor>();
        var seenNames = new HashSet<string>(StringComparer.Ordinal);

        for (var current = relicType;
             current != null && current != typeof(RelicModel) && current != typeof(AbstractModel) && current != typeof(object);
             current = current.BaseType)
        {
            foreach (var field in current.GetFields(AllInstanceDeclaredFlags))
            {
                if (field.IsStatic || field.IsInitOnly)
                    continue;

                if (!IsSupportedValueType(field.FieldType))
                    continue;

                var isKnownCounter = KnownRelicMutableFieldNames.Contains(field.Name, StringComparer.Ordinal);
                var isPrivateMutableCounter = !field.Name.Contains("k__BackingField", StringComparison.Ordinal);

                if ((isKnownCounter || isPrivateMutableCounter) && seenNames.Add($"{current.Name}.{field.Name}"))
                {
                    accessors.Add(MemberStateAccessor.ForField(field));
                }
            }

            foreach (var prop in current.GetProperties(AllInstanceDeclaredFlags))
            {
                if (!prop.CanRead || !prop.CanWrite)
                    continue;

                if (prop.GetCustomAttribute<SavedPropertyAttribute>(inherit: false) == null)
                    continue;

                if (!IsSupportedValueType(prop.PropertyType))
                    continue;

                if (seenNames.Add($"{current.Name}.{prop.Name}"))
                {
                    accessors.Add(MemberStateAccessor.ForProperty(prop));
                }
            }
        }

        accessors.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        return accessors;
    }

    private static void TryRegisterPowerType(Type powerType)
    {
        for (var current = powerType;
             current != null && current != typeof(PowerModel) && current != typeof(AbstractModel) && current != typeof(object);
             current = current.BaseType)
        {
            foreach (var field in current.GetFields(AllInstanceDeclaredFlags))
            {
                if (field.IsStatic || field.IsInitOnly)
                    continue;

                if (!IsSupportedScalarPowerFieldType(field.FieldType))
                    continue;

                RegisterPowerFieldGenericMethod
                    .MakeGenericMethod(powerType)
                    .Invoke(null, new object[] { field });
            }
        }
    }

    private static void RegisterPowerFieldAccessor<TPower>(FieldInfo field)
        where TPower : PowerModel
    {
        PowerHiddenStateMirrors.Register<TPower>(
            field.Name,
            (sim, power) => ReadScalarAsLong(field.GetValue(power)));
    }

    private static void TryRegisterMayhemModifier(Assembly hextechAssembly)
    {
        var modifierType = HextechRuntimeAutoBinder.ResolveType(hextechAssembly, "HextechRunes.HextechMayhemModifier");
        var serializerType = HextechRuntimeAutoBinder.ResolveType(hextechAssembly, "HextechRunes.HextechMayhemCombatTrackingSerializer");
        if (modifierType == null || serializerType == null)
            return;

        var trackingProp = modifierType.GetProperty("CombatTracking", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var serializeMethod = serializerType.GetMethod("Serialize", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (trackingProp == null || serializeMethod == null)
            return;

        RegisterModifierGenericMethod
            .MakeGenericMethod(modifierType)
            .Invoke(null, new object[] { trackingProp, serializeMethod });
    }

    private static void RegisterMayhemModifierTyped<TModifier>(PropertyInfo trackingProp, MethodInfo serializeMethod)
        where TModifier : AbstractModel
    {
        string CaptureSerialized(TModifier modifier)
        {
            var tracking = trackingProp.GetValue(modifier);
            if (tracking == null)
                return string.Empty;
            return (string?)serializeMethod.Invoke(null, new[] { tracking }) ?? string.Empty;
        }

        ModelPredictionStateMirrors.RegisterModifier<TModifier, MayhemTrackingPredictedState>(
            "HextechAdapter.MayhemModifier.CombatTracking.v1",
            (sim, modifier) => new MayhemTrackingPredictedState(CaptureSerialized(modifier)),
            (modifier, ref writer) => writer.Add("combatTracking", CaptureSerialized(modifier)),
            (state, ref writer) => writer.Add("combatTracking", state.SerializedJson));
    }

    private static bool IsSupportedValueType(Type type)
    {
        return type == typeof(bool)
            || type == typeof(int)
            || type == typeof(long)
            || type == typeof(ulong)
            || type == typeof(decimal)
            || type == typeof(string)
            || type.IsEnum;
    }

    private static bool IsSupportedScalarPowerFieldType(Type type)
    {
        return type == typeof(bool)
            || type == typeof(int)
            || type == typeof(long)
            || type == typeof(decimal)
            || type.IsEnum;
    }

    private static long ReadScalarAsLong(object? raw)
    {
        return raw switch
        {
            null => 0L,
            bool b => b ? 1L : 0L,
            int i => i,
            long l => l,
            decimal d => decimal.ToInt64( decimal.Truncate(d * 1000m) ),
            Enum e => Convert.ToInt64(e),
            _ => 0L
        };
    }

    private static ReflectedValueBagState CaptureBag(object instance, IReadOnlyList<MemberStateAccessor> accessors)
    {
        var entries = new KeyValuePair<string, object?>[accessors.Count];
        for (int i = 0; i < accessors.Count; i++)
        {
            entries[i] = new KeyValuePair<string, object?>(accessors[i].Name, accessors[i].Getter(instance));
        }
        return new ReflectedValueBagState(entries);
    }

    private static void WriteLiveBag(object instance, IReadOnlyList<MemberStateAccessor> accessors, ref ModelPredictionStateWriter writer)
    {
        for (int i = 0; i < accessors.Count; i++)
        {
            WriteValue(ref writer, accessors[i].Name, accessors[i].Getter(instance));
        }
    }

    internal static void WriteValue(ref ModelPredictionStateWriter writer, string name, object? value)
    {
        switch (value)
        {
            case null:
                writer.Add(name, (string?)null);
                break;
            case bool b:
                writer.Add(name, b);
                break;
            case int i:
                writer.Add(name, (long)i);
                break;
            case long l:
                writer.Add(name, l);
                break;
            case ulong u:
                writer.Add(name, u);
                break;
            case decimal d:
                writer.Add(name, decimal.ToInt64(decimal.Truncate(d * 1000m)));
                break;
            case Enum e:
                writer.Add(name, Convert.ToInt64(e));
                break;
            case string s:
                writer.Add(name, s);
                break;
            default:
                writer.Add(name, value.ToString());
                break;
        }
    }

    private sealed class MemberStateAccessor
    {
        public string Name { get; }
        public Func<object, object?> Getter { get; }

        private MemberStateAccessor(string name, Func<object, object?> getter)
        {
            Name = name;
            Getter = getter;
        }

        public static MemberStateAccessor ForField(FieldInfo field) =>
            new(field.Name, field.GetValue);

        public static MemberStateAccessor ForProperty(PropertyInfo property) =>
            new(property.Name, obj => property.GetValue(obj));
    }

    private sealed class ReflectedValueBagState : IPredictionStateForkable
    {
        private readonly KeyValuePair<string, object?>[] _entries;

        public ReflectedValueBagState(KeyValuePair<string, object?>[] entries)
        {
            _entries = entries;
        }

        public void WriteTo(ref ModelPredictionStateWriter writer)
        {
            for (int i = 0; i < _entries.Length; i++)
            {
                WriteValue(ref writer, _entries[i].Key, _entries[i].Value);
            }
        }

        public object Fork(PredictionForkContext context)
        {
            var copy = new KeyValuePair<string, object?>[_entries.Length];
            Array.Copy(_entries, copy, _entries.Length);
            return new ReflectedValueBagState(copy);
        }
    }

    private sealed class MayhemTrackingPredictedState : IPredictionStateForkable
    {
        public string SerializedJson { get; set; }

        public MayhemTrackingPredictedState(string serializedJson)
        {
            SerializedJson = serializedJson;
        }

        public object Fork(PredictionForkContext context) =>
            new MayhemTrackingPredictedState(SerializedJson);
    }
}
