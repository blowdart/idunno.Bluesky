// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using idunno.AtProto;

namespace idunno.Bluesky.Test;

[ExcludeFromCodeCoverage]
public class BlueskyErrorTests
{
    public static TheoryData<Type> ErrorTypes
    {
        get
        {
            TheoryData<Type> data = [];

            foreach (Type type in typeof(BlueskyError).Assembly.GetTypes()
                .Where(t => t.IsSealed && !t.IsAbstract && typeof(BlueskyError).IsAssignableFrom(t))
                .OrderBy(t => t.Name, StringComparer.Ordinal))
            {
                data.Add(type);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(ErrorTypes))]
    public void EveryBlueskyErrorDeclaresAnErrorTitleWhichMapsBackToItself(Type errorType)
    {
        string errorTitle = ErrorTitleFor(errorType);

        AtErrorDetail atErrorDetail = CreateErrorDetail(errorTitle);

        AtErrorDetail? mapped = BlueskyError.Map(atErrorDetail);

        Assert.NotNull(mapped);
        Assert.IsType(errorType, mapped);
        Assert.Equal(errorTitle, mapped.Error);
    }

    [Theory]
    [MemberData(nameof(ErrorTypes))]
    public void EveryBlueskyErrorConstructorRejectsAMismatchedErrorTitle(Type errorType)
    {
        AtErrorDetail atErrorDetail = CreateErrorDetail("ThisIsNotAnErrorTitle");

        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
            () => Activator.CreateInstance(errorType, atErrorDetail));

        Assert.IsType<ArgumentException>(exception.InnerException);
    }

    [Theory]
    [MemberData(nameof(ErrorTypes))]
    public void EveryBlueskyErrorConstructorRejectsANullErrorDetail(Type errorType)
    {
        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
            () => Activator.CreateInstance(errorType, [null]));

        Assert.IsType<ArgumentNullException>(exception.InnerException);
    }

    [Fact]
    public void ErrorTitlesAreUniqueAcrossAllBlueskyErrors()
    {
        List<string> errorTitles = [.. ErrorTypes.Select(row => ErrorTitleFor(row.Data))];

        Assert.Equal(errorTitles.Count, errorTitles.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void MapReturnsNullWhenPassedNull()
    {
        Assert.Null(BlueskyError.Map(null));
    }

    [Fact]
    public void MapReturnsTheOriginalDetailWhenTheErrorTitleIsUnknown()
    {
        AtErrorDetail atErrorDetail = CreateErrorDetail("SomeErrorNobodyHasEverHeardOf");

        Assert.Same(atErrorDetail, BlueskyError.Map(atErrorDetail));
    }

    [Fact]
    public void MapDoesNotHandleAtProtoErrorTitles()
    {
        AtErrorDetail atErrorDetail = CreateErrorDetail("ExpiredToken");

        Assert.Same(atErrorDetail, BlueskyError.Map(atErrorDetail));
    }

    [Fact]
    public void BlockedByActorIsDistinctFromBlockedActor()
    {
        Assert.IsType<BlockedByActor>(BlueskyError.Map(CreateErrorDetail("BlockedByActor")));
        Assert.IsType<BlockedActor>(BlueskyError.Map(CreateErrorDetail("BlockedActor")));
    }

    private static AtErrorDetail CreateErrorDetail(string error)
    {
        AtErrorDetail atErrorDetail = new();

        typeof(AtErrorDetail)
            .GetProperty(nameof(AtErrorDetail.Error))!
            .SetValue(atErrorDetail, error);

        return atErrorDetail;
    }

    private static string ErrorTitleFor(Type errorType)
    {
        FieldInfo field = errorType.GetField("ErrorTitle", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            ?? throw new InvalidOperationException($"{errorType.Name} does not declare an ErrorTitle constant.");

        return (string)field.GetRawConstantValue()!;
    }
}
