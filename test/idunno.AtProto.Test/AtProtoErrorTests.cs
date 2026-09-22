// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;

namespace idunno.AtProto.Test;

[ExcludeFromCodeCoverage]
public class AtProtoErrorTests
{
    public static TheoryData<Type> ErrorTypes
    {
        get
        {
            TheoryData<Type> data = [];

            foreach (Type type in typeof(AtProtoError).Assembly.GetTypes()
                .Where(t => t.IsSealed && !t.IsAbstract && typeof(AtProtoError).IsAssignableFrom(t))
                .OrderBy(t => t.Name, StringComparer.Ordinal))
            {
                data.Add(type);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(ErrorTypes))]
    public void EveryAtProtoErrorDeclaresAnErrorTitleWhichMapsBackToItself(Type errorType)
    {
        string errorTitle = ErrorTitleFor(errorType);

        AtErrorDetail atErrorDetail = CreateErrorDetail(errorTitle);

        AtErrorDetail? mapped = AtProtoError.Map(atErrorDetail);

        Assert.NotNull(mapped);
        Assert.IsType(errorType, mapped);
        Assert.Equal(errorTitle, mapped.Error);
    }

    [Theory]
    [MemberData(nameof(ErrorTypes))]
    public void EveryAtProtoErrorConstructorRejectsAMismatchedErrorTitle(Type errorType)
    {
        AtErrorDetail atErrorDetail = CreateErrorDetail("ThisIsNotAnErrorTitle");

        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
            () => Activator.CreateInstance(errorType, atErrorDetail));

        Assert.IsType<ArgumentException>(exception.InnerException);
    }

    [Theory]
    [MemberData(nameof(ErrorTypes))]
    public void EveryAtProtoErrorConstructorRejectsANullErrorDetail(Type errorType)
    {
        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
            () => Activator.CreateInstance(errorType, [null]));

        Assert.IsType<ArgumentNullException>(exception.InnerException);
    }

    [Fact]
    public void ErrorTitlesAreUniqueAcrossAllAtProtoErrors()
    {
        List<string> errorTitles = [.. ErrorTypes.Select(row => ErrorTitleFor(row.Data))];

        Assert.Equal(errorTitles.Count, errorTitles.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void MapReturnsNullWhenPassedNull()
    {
        Assert.Null(AtProtoError.Map(null));
    }

    [Fact]
    public void MapReturnsTheOriginalDetailWhenTheErrorTitleIsUnknown()
    {
        AtErrorDetail atErrorDetail = CreateErrorDetail("SomeErrorNobodyHasEverHeardOf");

        AtErrorDetail? mapped = AtProtoError.Map(atErrorDetail);

        Assert.Same(atErrorDetail, mapped);
    }

    [Fact]
    public void MapIsCaseSensitive()
    {
        AtErrorDetail atErrorDetail = CreateErrorDetail("expiredtoken");

        AtErrorDetail? mapped = AtProtoError.Map(atErrorDetail);

        Assert.Same(atErrorDetail, mapped);
    }

    [Fact]
    public void MappingCarriesTheOriginalErrorDetailOntoTheMappedError()
    {
        AtErrorDetail atErrorDetail = CreateErrorDetail(ExpiredToken.ErrorTitle);
        atErrorDetail.Message = "The token has expired.";
        atErrorDetail.RawContent = "{}";
        atErrorDetail.Instance = new Uri("https://example.org/xrpc/com.atproto.server.getSession");
        atErrorDetail.HttpMethod = HttpMethod.Get;

        AtErrorDetail? mapped = AtProtoError.Map(atErrorDetail);

        ExpiredToken expiredToken = Assert.IsType<ExpiredToken>(mapped);
        Assert.Equal(atErrorDetail.Message, expiredToken.Message);
        Assert.Equal(atErrorDetail.RawContent, expiredToken.RawContent);
        Assert.Equal(atErrorDetail.Instance, expiredToken.Instance);
        Assert.Equal(atErrorDetail.HttpMethod, expiredToken.HttpMethod);
    }

    [Fact]
    public void CopyingAnErrorDetailDoesNotShareItsExtensionData()
    {
        AtErrorDetail atErrorDetail = CreateErrorDetail(ExpiredToken.ErrorTitle);
        atErrorDetail.ExtensionData = new Dictionary<string, JsonElement>
        {
            { "retryAfter", JsonSerializer.SerializeToElement(30) }
        };

        ExpiredToken expiredToken = new(atErrorDetail);

        Assert.NotNull(expiredToken.ExtensionData);
        Assert.NotSame(atErrorDetail.ExtensionData, expiredToken.ExtensionData);
        Assert.Single(expiredToken.ExtensionData);

        atErrorDetail.ExtensionData["addedLater"] = JsonSerializer.SerializeToElement(true);

        Assert.Single(expiredToken.ExtensionData);
    }

    [Fact]
    public void ErrorMappersPassedToTheClientAreNotAddedTo()
    {
        List<Func<AtErrorDetail?, AtErrorDetail?>> errorMappers = [DummyMapper];

        _ = new AtProtoHttpClient<string>(null, null, null, null, errorMappers);

        Assert.Equal([DummyMapper], errorMappers);
    }

    [Fact]
    public void AReadOnlyCollectionOfErrorMappersIsAccepted()
    {
        IList<Func<AtErrorDetail?, AtErrorDetail?>> errorMappers =
            new ReadOnlyCollection<Func<AtErrorDetail?, AtErrorDetail?>>([DummyMapper]);

        TestableAtProtoHttpClient client = new(errorMappers);

        Assert.Equal([DummyMapper, AtProtoError.Map], client.Mappers);
    }

    [Fact]
    public void TheBaseErrorMapperIsAppendedWhenCallerMappersDoNotIncludeIt()
    {
        TestableAtProtoHttpClient client = new([DummyMapper]);

        Assert.Equal([DummyMapper, AtProtoError.Map], client.Mappers);
    }

    [Fact]
    public void TheBaseErrorMapperIsNotDuplicatedWhenCallerMappersAlreadyIncludeIt()
    {
        TestableAtProtoHttpClient client = new([AtProtoError.Map, DummyMapper]);

        Assert.Equal([AtProtoError.Map, DummyMapper], client.Mappers);
    }

    [Fact]
    public void TheDefaultErrorMapperChainIsTheBaseMapper()
    {
        TestableAtProtoHttpClient client = new(null);

        Assert.Equal([AtProtoError.Map], client.Mappers);
    }

    private static AtErrorDetail? DummyMapper(AtErrorDetail? atErrorDetail) => atErrorDetail;

    private static AtErrorDetail CreateErrorDetail(string error) => new() { Error = error };

    private static string ErrorTitleFor(Type errorType)
    {
        FieldInfo field = errorType.GetField("ErrorTitle", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            ?? throw new InvalidOperationException($"{errorType.Name} does not declare an ErrorTitle constant.");

        return (string)field.GetRawConstantValue()!;
    }

    [ExcludeFromCodeCoverage]
    private sealed class TestableAtProtoHttpClient : AtProtoHttpClient<string>
    {
        public TestableAtProtoHttpClient(IList<Func<AtErrorDetail?, AtErrorDetail?>>? errorMappers)
            : base(null, null, null, null, errorMappers)
        {
        }

        public IReadOnlyCollection<Func<AtErrorDetail?, AtErrorDetail?>> Mappers => ErrorMapperChain;
    }
}
