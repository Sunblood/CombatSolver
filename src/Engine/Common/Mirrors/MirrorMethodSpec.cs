using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using MegaCrit.Sts2.Core.Models;

namespace CombatSolver.Engine.Common.Mirrors;

// Identifies the original virtual method whose model-specific overrides are mirrored.
// Keeping the base MethodInfo lets the registry support methods declared below AbstractModel,
// including protected methods excluded by the publicizer.
internal class MirrorMethodSpec
{
    private static readonly ConcurrentDictionary<(Type WrapperBaseType, MethodInfo NativeHookMethod), MethodInfo>
        ForwardedHooks = new();

    private readonly BindingFlags _lookupFlags;
    private readonly Type[] _parameterTypes;

    public string Name { get; }

    public MethodInfo BaseMethod { get; }

    public MirrorMethodSpec(
        Type declaringType,
        string name,
        BindingFlags bindingFlags,
        Type[] parameterTypes)
    {
        Name = name;
        _lookupFlags = bindingFlags & ~BindingFlags.DeclaredOnly;
        _parameterTypes = parameterTypes;
        BaseMethod = declaringType.GetMethod(
                name,
                _lookupFlags | BindingFlags.DeclaredOnly,
                binder: null,
                parameterTypes,
                modifiers: null)
            ?? throw new MissingMethodException(declaringType.FullName, name);
    }

    public static MirrorMethodSpec Hook(string name, Type[] parameterTypes)
    {
        return new MirrorMethodSpec(
            typeof(AbstractModel),
            name,
            BindingFlags.Instance | BindingFlags.Public,
            parameterTypes);
    }

    /// <summary>
    /// Registers an abstract base class's <c>sealed override</c> hook forwarder so <see cref="TryGetOverride"/>
    /// checks whether concrete derived models override <paramref name="forwardedVirtualMethod"/> instead.
    /// </summary>
    public static void RegisterForwardedHook(
        Type wrapperBaseType,
        MethodInfo nativeHookMethod,
        MethodInfo forwardedVirtualMethod)
    {
        ArgumentNullException.ThrowIfNull(wrapperBaseType);
        ArgumentNullException.ThrowIfNull(nativeHookMethod);
        ArgumentNullException.ThrowIfNull(forwardedVirtualMethod);
        if (!typeof(AbstractModel).IsAssignableFrom(wrapperBaseType))
            throw new ArgumentException($"{wrapperBaseType.FullName} does not derive from AbstractModel.", nameof(wrapperBaseType));
        if (!wrapperBaseType.IsAbstract)
            throw new ArgumentException($"{wrapperBaseType.FullName} must be an abstract forwarder base class.", nameof(wrapperBaseType));
        if (nativeHookMethod.DeclaringType != wrapperBaseType || !nativeHookMethod.IsVirtual)
            throw new ArgumentException("nativeHookMethod must be declared as a virtual/override method on wrapperBaseType.", nameof(nativeHookMethod));
        if (forwardedVirtualMethod.DeclaringType != wrapperBaseType || !forwardedVirtualMethod.IsVirtual || forwardedVirtualMethod.IsFinal)
            throw new ArgumentException("forwardedVirtualMethod must be a non-sealed virtual method declared on wrapperBaseType.", nameof(forwardedVirtualMethod));

        MethodInfo rootHookMethod = nativeHookMethod.GetBaseDefinition();
        if (!ForwardedHooks.TryAdd((wrapperBaseType, rootHookMethod), forwardedVirtualMethod))
            throw new InvalidOperationException($"Forwarded hook is already registered for {wrapperBaseType.FullName}.{nativeHookMethod.Name}.");
    }

    public bool TryGetOverride(Type receiverType, [NotNullWhen(true)] out MethodInfo? overrideMethod)
    {
        overrideMethod = receiverType.GetMethod(
            Name,
            _lookupFlags,
            binder: null,
            _parameterTypes,
            modifiers: null);

        if (overrideMethod is null || overrideMethod.DeclaringType == BaseMethod.DeclaringType)
        {
            overrideMethod = null;
            return false;
        }

        while (overrideMethod.DeclaringType is { } declaringType
            && ForwardedHooks.TryGetValue((declaringType, BaseMethod.GetBaseDefinition()), out MethodInfo? forwardedBase))
        {
            ParameterInfo[] parameters = forwardedBase.GetParameters();
            Type[] forwardedParamTypes = new Type[parameters.Length];
            for (int index = 0; index < parameters.Length; index++)
                forwardedParamTypes[index] = parameters[index].ParameterType;

            MethodInfo? candidate = receiverType.GetMethod(
                forwardedBase.Name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                forwardedParamTypes,
                modifiers: null);
            if (candidate is null || candidate.DeclaringType == forwardedBase.DeclaringType)
            {
                overrideMethod = null;
                return false;
            }

            overrideMethod = candidate;
        }

        return true;
    }
}
