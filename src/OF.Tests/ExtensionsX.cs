using Ardalis.GuardClauses;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using OF.Common;
using System.Text;

namespace OF.Tests;
internal static class ExtensionsX
{
    /// <summary>
    /// Replace the <see cref="TimeProvider"/> by <see cref="FakeTimeProvider"/> always providing the same point in time.
    /// </summary>
    /// <param name="services"></param>
    public static void FakeTime(this IServiceCollection services)
    {
        services.Replace(
            ServiceDescriptor.Singleton(
                typeof(FakeTimeProvider),
                typeof(FakeTimeProvider)));
        services.Replace(
            ServiceDescriptor.Singleton(
                typeof(TimeProvider),
                provider => provider.GetService<FakeTimeProvider>()!));
    }

    /// <summary>
    /// Undecorate the type's name if generic to make it human readable.
    /// It uses the full name of the types unless it belongs to the <see cref="System"/> namespace.
    /// </summary>
    public static string FriendlyName(this Type type)
        => FriendlyName(type, false);

    /// <summary>
    /// Undecorate the type's name if generic to make it human readable.
    /// It uses the full name of the types unless it belongs to the <see cref="System"/> namespace or
    /// <paramref name="omitNamespace"/> is <see langword="true"/>.
    /// </summary>
    public static string FriendlyName(this Type type, bool omitNamespace)
        => FriendlyName(
            type,
            x => !(omitNamespace || (x.Namespace?.StartsWith("System", StringComparison.Ordinal) ?? true)));

    /// <summary>
    /// Undecorate the type's name if generic to make it human readable.
    /// </summary>
    /// <param name="type">the type to get the name from</param>
    /// <param name="preferNamespace">get whether the namespace need to be added to the type when relevant</param>
    /// <returns>the undecorated type's name</returns>
    /// <exception cref="ArgumentException">throws if either <paramref name="type"/> or <paramref name="getTypeName"/> is null</exception>
    public static string FriendlyName(this Type type, Func<Type, bool>? preferNamespace)
    {
        Guard.Against.Null(type, nameof(type));
        preferNamespace ??= t => true;

        var nameBuilder = new StringBuilder();
        Scan(
            nameBuilder,
            type,
            GetName,
            preferNamespace,
            overrideNamespaceUsage: false);
        return nameBuilder.ToString();
    }

    /// <summary>
    /// Scans the generic types list and builds the type name
    /// </summary>
    private static void Scan(
        StringBuilder builder,
        Type type,
        Func<Type, Func<Type, bool>, string> getName,
        Func<Type, bool> getUseNamespace,
        bool overrideNamespaceUsage)
    {
        var tree = GetBoundGenericArgumentTypes(type);

        if (tree.TryPop(out var slice))
        {
            AppendTypeName(
                builder,
                slice.type,
                slice.boundGenericTypeParameters,
                getName,
                getUseNamespace,
                overrideNamespaceUsage);
        }

        while (tree.TryPop(out slice))
        {
            builder.Append("::");
            AppendTypeName(
                builder,
                slice.type,
                slice.boundGenericTypeParameters,
                getName,
                getUseNamespace,
                overrideNamespaceUsage: true);
        }
    }

    private static void AppendTypeName(
        StringBuilder builder,
        Type current,
        Type[] genericParameterTypes,
        Func<Type, Func<Type, bool>, string> getName,
        Func<Type, bool> getUseNamespace,
        bool overrideNamespaceUsage)
    {
        var name = getName(
            current,
            !overrideNamespaceUsage ?
                getUseNamespace :
                (t) => false)
            .AsSpan();

        if (genericParameterTypes.Length > 0)
        {
            var index = name.IndexOf('`');

            if (index >= 0)
            {
                name = name[..index];
            }
        }

        builder.Append(name);

        if (genericParameterTypes.Length > 0)
        {
            builder.Append('<');
            var inner = genericParameterTypes[0];
            Scan(
                builder,
                inner,
                getName,
                getUseNamespace,
                inner.IsNested && inner.IsGenericTypeParameter);

            var i = 1;

            while (i < genericParameterTypes.Length)
            {
                builder.Append(", ");
                inner = genericParameterTypes[i++];
                Scan(
                    builder,
                    inner,
                    getName,
                    getUseNamespace,
                    inner.IsNested && inner.IsGenericTypeParameter);
            }

            builder.Append('>');
        }
    }

    private static string GetName(Type t, Func<Type, bool> useNamespace)
    {
        var name = t.Name ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(name) &&
            !string.IsNullOrWhiteSpace(t.Namespace) &&
            useNamespace(t))
        {
            name = t.Namespace + "." + name;
        }

        return name;
    }

    private static Stack<(Type type, Type[] boundGenericTypeParameters)> GetBoundGenericArgumentTypes(Type from)
    {
        var tree = new Stack<(Type, Type[])>();
        var genericParameterTypes = from.GetGenericArguments().AsSpan();

        do
        {
            var count = GetBoundGenericArgumentsCount(from, genericParameterTypes);
            var boundGenericTypesPerType = (
                from,
                count == 0 ?
                    Array.Empty<Type>() :
                    genericParameterTypes[^count..].ToArray());
            tree.Push(boundGenericTypesPerType);
            genericParameterTypes = genericParameterTypes[..^count];
        }
        while (!from.IsGenericTypeParameter && (from = from.DeclaringType) != null);

        return tree;

        static int GetBoundGenericArgumentsCount(Type t, ReadOnlySpan<Type> genericTypeParameters)
        {
            if (t?.IsGenericType != true)
            {
                return 0;
            }

            return t.IsNested ?
                genericTypeParameters.Length - t.DeclaringType.GetGenericArguments().Length :
                genericTypeParameters.Length;
        }
    }
}
