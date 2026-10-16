// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Cryptography;

using idunno.AtProto.Authentication;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace idunno.Bluesky.AspNet.Authentication.Test;

public sealed class ClientSigningKeyTests : IDisposable
{
    private const string ClientId = "https://app.example.com/oauth-client-metadata.json";

    private readonly string _directory = Directory.CreateTempSubdirectory("idunno-signing-key-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    public static TheoryData<string> Registrations => ["AddBluesky", "AddBlueskyOAuthClientMetadata", "AddBlueskyAgentFactory", "AddBlueskyClaimsTransformer"];

    [Theory]
    [MemberData(nameof(Registrations))]
    public void RegistrationLoadsTheKeyFromTheConfiguredPath(string registration)
    {
        (string path, string expectedX) = WriteKey();
        ServiceCollection services = CreateServices(options =>
        {
            options.ClientSigningKeyPath = path;
            options.ClientSigningKeyId = "custom-kid";
        });

        Register(services, registration);

        OAuthClientSigningKey key = GetSigningKey(services);
        Assert.Equal(expectedX, key.X);
        Assert.Equal("custom-kid", key.KeyId);
    }

    [Fact]
    public void TheKeyPathBindsFromConfiguration()
    {
        (string path, string expectedX) = WriteKey();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BlueskyAgent:OAuthOptions:ClientId"] = ClientId,
                ["BlueskyAgent:OAuthOptions:ClientSigningKeyPath"] = path,
            })
            .Build();
        ServiceCollection services = new();
        services.AddLogging();
        services.Configure<BlueskyAgentOptions>(configuration.GetSection("BlueskyAgent"), options => options.ErrorOnUnknownConfiguration = true);

        services.AddBlueskyOAuthClientMetadata();

        OAuthClientSigningKey key = GetSigningKey(services);
        Assert.Equal(expectedX, key.X);
        Assert.Equal(OAuthClientSigningKey.FromPemFile(path).KeyId, key.KeyId);
    }

    [Fact]
    public void AnExplicitKeyIsNotReplacedByThePath()
    {
        using ECDsa ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        OAuthClientSigningKey explicitKey = OAuthClientSigningKey.FromPem(ecdsa.ExportPkcs8PrivateKeyPem());
        ServiceCollection services = CreateServices(options =>
        {
            options.ClientSigningKey = explicitKey;
            options.ClientSigningKeyPath = Path.Combine(_directory, "missing.pem");
        });

        services.AddBlueskyOAuthClientMetadata();

        Assert.Same(explicitKey, GetSigningKey(services));
    }

    [Theory]
    [InlineData("00:00:00", 0)]
    [InlineData("00:00:45", 45)]
    public void ClientAssertionClockSkewBindsFromConfiguration(string configuredSkew, int expectedSeconds)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BlueskyAgent:OAuthOptions:ClientId"] = ClientId,
                ["BlueskyAgent:OAuthOptions:ClientAssertionClockSkew"] = configuredSkew
            })
            .Build();
        ServiceCollection services = new();
        services.AddLogging();
        services.Configure<BlueskyAgentOptions>(configuration.GetSection("BlueskyAgent"), options => options.ErrorOnUnknownConfiguration = true);
        services.AddBlueskyOAuthClientMetadata();

        using ServiceProvider provider = services.BuildServiceProvider();
        OAuthOptions options = provider.GetRequiredService<IOptions<BlueskyAgentOptions>>().Value.OAuthOptions!;

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), options.ClientAssertionClockSkew);
        Assert.Equal(OAuthOptions.DefaultClockSkew, options.ClockSkew);
    }

    [Fact]
    public void AdditionalKeyPathsBindFromConfigurationAndLoad()
    {
        (string activePath, string activeX) = WriteKey();
        (string previousPath, string previousX) = WriteKey("previous-signing-key.pem");
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BlueskyAgent:OAuthOptions:ClientId"] = ClientId,
                ["BlueskyAgent:OAuthOptions:ClientSigningKeyPath"] = activePath,
                ["BlueskyAgent:OAuthOptions:AdditionalClientSigningKeyPaths:0"] = previousPath,
            })
            .Build();
        ServiceCollection services = new();
        services.AddLogging();
        services.Configure<BlueskyAgentOptions>(configuration.GetSection("BlueskyAgent"), options => options.ErrorOnUnknownConfiguration = true);

        services.AddBlueskyOAuthClientMetadata();

        using ServiceProvider provider = services.BuildServiceProvider();
        OAuthOptions oAuthOptions = provider.GetRequiredService<IOptions<BlueskyAgentOptions>>().Value.OAuthOptions!;
        Assert.Equal(activeX, oAuthOptions.ClientSigningKey!.X);
        OAuthClientSigningKey previous = Assert.Single(oAuthOptions.AdditionalClientSigningKeys);
        Assert.Equal(previousX, previous.X);
        Assert.Equal(OAuthClientSigningKey.FromPemFile(previousPath).KeyId, previous.KeyId);
        oAuthOptions.Validate();
    }

    [Fact]
    public void AnAdditionalKeyAlreadySetIsNotAddedAgain()
    {
        (string path, _) = WriteKey("previous-signing-key.pem");
        OAuthClientSigningKey previous = OAuthClientSigningKey.FromPemFile(path);
        ServiceCollection services = CreateServices(options =>
        {
            options.ClientSigningKey = OAuthClientSigningKey.FromPem(CreatePem());
            options.AdditionalClientSigningKeys.Add(previous);
            options.AdditionalClientSigningKeyPaths.Add(path);
        });

        services.AddBlueskyOAuthClientMetadata();

        using ServiceProvider provider = services.BuildServiceProvider();
        Assert.Same(previous, Assert.Single(provider.GetRequiredService<IOptions<BlueskyAgentOptions>>().Value.OAuthOptions!.AdditionalClientSigningKeys));
    }

    [Fact]
    public void NoPathLeavesThePublicClientUnchanged()
    {
        ServiceCollection services = CreateServices(_ => { });

        services.AddBlueskyOAuthClientMetadata();

        using ServiceProvider provider = services.BuildServiceProvider();
        Assert.Null(provider.GetRequiredService<IOptions<BlueskyAgentOptions>>().Value.OAuthOptions!.ClientSigningKey);
    }

    [Fact]
    public void AMissingKeyFileThrowsWhenTheOptionsAreResolved()
    {
        string path = Path.Combine(_directory, "missing.pem");
        ServiceCollection services = CreateServices(options => options.ClientSigningKeyPath = path);

        services.AddBlueskyOAuthClientMetadata();

        using ServiceProvider provider = services.BuildServiceProvider();
        FileNotFoundException exception = Assert.Throws<FileNotFoundException>(
            () => provider.GetRequiredService<IOptions<BlueskyAgentOptions>>().Value);
        Assert.Equal(path, exception.FileName);
    }

    [Fact]
    public void TheLoaderIsRegisteredOnce()
    {
        ServiceCollection services = CreateServices(_ => { });

        services.AddBlueskyOAuthClientMetadata();
        services.AddBlueskyAgentFactory();
        services.AddBlueskyClaimsTransformer();

        Assert.Single(services, descriptor =>
            descriptor.ServiceType == typeof(IPostConfigureOptions<BlueskyAgentOptions>) &&
            descriptor.ImplementationFactory?.Method.ReturnType == typeof(ClientSigningKeyPostConfigureOptions));
    }

    [Fact]
    public void ResolvePathExpandsTheHomeDirectory()
    {
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        Assert.Equal(
            Path.GetFullPath(Path.Combine(profile, ".blueskyDotnet", "client-signing-key.pem")),
            ClientSigningKeyPostConfigureOptions.ResolvePath("~/.blueskyDotnet/client-signing-key.pem", _directory));
        Assert.Equal(Path.GetFullPath(profile), ClientSigningKeyPostConfigureOptions.ResolvePath("~", _directory));
    }

    [Fact]
    public void ResolvePathExpandsEnvironmentVariables()
    {
        string variable = $"IDUNNO_SIGNING_KEY_TEST_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(variable, _directory);
        try
        {
            Assert.Equal(
                Path.GetFullPath(Path.Combine(_directory, "key.pem")),
                ClientSigningKeyPostConfigureOptions.ResolvePath($"%{variable}%/key.pem", contentRootPath: null));
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    [Fact]
    public void ResolvePathResolvesRelativePathsAgainstTheContentRoot()
    {
        Assert.Equal(
            Path.GetFullPath(Path.Combine(_directory, "keys", "key.pem")),
            ClientSigningKeyPostConfigureOptions.ResolvePath(Path.Combine("keys", "key.pem"), _directory));
    }

    private static ServiceCollection CreateServices(Action<OAuthOptions> configureOAuthOptions)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.Configure<BlueskyAgentOptions>(options =>
        {
            options.OAuthOptions = new OAuthOptions(ClientId);
            configureOAuthOptions(options.OAuthOptions);
        });

        return services;
    }

    private static void Register(ServiceCollection services, string registration)
    {
        switch (registration)
        {
            case "AddBluesky":
                services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
                new AuthenticationBuilder(services).AddBluesky();
                break;
            case "AddBlueskyOAuthClientMetadata":
                services.AddBlueskyOAuthClientMetadata();
                break;
            case "AddBlueskyAgentFactory":
                services.AddBlueskyAgentFactory();
                break;
            case "AddBlueskyClaimsTransformer":
                services.AddBlueskyClaimsTransformer();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(registration));
        }
    }

    private static OAuthClientSigningKey GetSigningKey(ServiceCollection services)
    {
        using ServiceProvider provider = services.BuildServiceProvider();

        return Assert.IsType<OAuthClientSigningKey>(
            provider.GetRequiredService<IOptions<BlueskyAgentOptions>>().Value.OAuthOptions!.ClientSigningKey);
    }

    private static string CreatePem()
    {
        using ECDsa ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        return ecdsa.ExportPkcs8PrivateKeyPem();
    }

    private (string Path, string X) WriteKey(string fileName = "client-signing-key.pem")
    {
        using ECDsa ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        string path = Path.Combine(_directory, fileName);
        File.WriteAllText(path, ecdsa.ExportPkcs8PrivateKeyPem());

        return (path, OAuthClientSigningKey.FromPem(ecdsa.ExportPkcs8PrivateKeyPem()).X);
    }
}