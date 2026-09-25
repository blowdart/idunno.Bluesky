// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Reflection;

using idunno.AtProto;

namespace idunno.Bluesky.Serialization.Test;

public class AtProtoHttpResultResultTypeTests
{
    // Login returns whether the login attempt itself succeeded. That bool is a real payload rather than a sentinel
    // standing in for a failed call, so an absent value would not be meaningful.
    private static readonly string[] s_allowed =
    [
        "idunno.AtProto.AtProtoAgent.Login"
    ];

    public static TheoryData<Assembly> Assemblies =>
    [
        typeof(AtProtoAgent).Assembly,
        typeof(BlueskyAgent).Assembly
    ];

    /// <summary>
    /// <see cref="AtProtoHttpResult{TResult}.Succeeded"/> tests <c>Result is not null</c>, but
    /// <see cref="AtProtoHttpResult{TResult}.Result"/> is declared as <c>TResult?</c>. When <c>TResult</c> is bound to a
    /// value type that annotation is erased rather than becoming <see cref="Nullable{T}"/>, so the test is a compile time
    /// constant <see langword="true"/> and <c>Succeeded</c> degenerates into a status code check which can never report
    /// failure. Such an endpoint hands its caller a default or sentinel value alongside a successful status code.
    /// </summary>
    /// <remarks>
    /// <para>If this test fails, change the offending endpoint to return a nullable value type, for example
    /// <c>AtProtoHttpResult&lt;int?&gt;</c>, so that a failed call is distinguishable. Do not add it to the allow list
    /// unless the value is genuinely part of the payload rather than a stand in for failure.</para>
    /// </remarks>
    [Theory]
    [MemberData(nameof(Assemblies))]
    public void NoEndpointReturnsAnAtProtoHttpResultOfANonNullableValueType(Assembly assembly)
    {
        List<string> offenders = [];

        foreach (Type type in assembly.GetTypes())
        {
            if (!type.IsPublic && !type.IsNestedPublic)
            {
                continue;
            }

            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (ResultTypeOf(method.ReturnType) is not Type resultType)
                {
                    continue;
                }

                if (!resultType.IsValueType || Nullable.GetUnderlyingType(resultType) is not null)
                {
                    continue;
                }

                string name = $"{type.FullName}.{method.Name}";

                if (!s_allowed.Contains(name, StringComparer.Ordinal))
                {
                    offenders.Add($"{name} -> AtProtoHttpResult<{resultType.Name}>");
                }
            }
        }

        Assert.Empty(offenders.Distinct(StringComparer.Ordinal));
    }

    /// <summary>
    /// Returns the <c>TResult</c> of an <c>AtProtoHttpResult&lt;TResult&gt;</c> return type, unwrapping a
    /// <see cref="Task{TResult}"/> where present, or <see langword="null"/> when the type is neither.
    /// </summary>
    private static Type? ResultTypeOf(Type returnType)
    {
        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            returnType = returnType.GetGenericArguments()[0];
        }

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(AtProtoHttpResult<>))
        {
            return returnType.GetGenericArguments()[0];
        }

        return null;
    }
}
