// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.DataProtection;

using idunno.Security;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers the optional Xenon Razor Pages administration UI.</summary>
public static class LabelerUIExtensions
{
    /// <summary>Adds the Xenon administration pages to an ASP.NET Core application.</summary>
    /// <param name="services">The service collection with labeler administration already registered.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Labeler administration has not been registered.</exception>
    /// <remarks><para>Also register Bluesky authentication and its sign-in UI, call authentication and
    /// authorization middleware, and map Razor Pages. Administrative pages require an authenticated
    /// Bluesky DID; provider-backed team membership and permissions are checked again for each operation.
    /// Terms and privacy notices are publicly accessible.</para></remarks>
    public static IServiceCollection AddLabelerUI(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (!services.Any(descriptor => descriptor.ServiceType == typeof(idunno.Bluesky.AspNet.Labeler.AdministrationDispatcher)))
        {
            throw new InvalidOperationException("Register labeler administration before adding the Xenon UI.");
        }

        services.AddRazorPages(options =>
        {
            options.Conventions.AddAreaFolderApplicationModelConvention(
                "Bluesky",
                "/",
                model => model.Filters.Add(new idunno.Bluesky.AspNet.Labeler.UI.AuthenticationLayoutFilter()));
            options.Conventions.AuthorizeAreaFolder("Xenon", "/");
            options.Conventions.AllowAnonymousToAreaPage("Xenon", "/Terms");
            options.Conventions.AllowAnonymousToAreaPage("Xenon", "/Privacy");
            options.Conventions.AddAreaFolderApplicationModelConvention(
                "Xenon",
                "/",
                model =>
                {
                    if (model.ViewEnginePath is not ("/Terms" or "/Privacy"))
                    {
                        model.Filters.Add(new ServiceFilterAttribute(typeof(idunno.Bluesky.AspNet.Labeler.UI.LabelerUiPageFilter)));
                    }
                });
        }).AddApplicationPart(typeof(LabelerUIExtensions).Assembly);
        services.AddScoped<idunno.Bluesky.AspNet.Labeler.UI.LabelerUiPageFilter>(
            serviceProvider => new idunno.Bluesky.AspNet.Labeler.UI.LabelerUiPageFilter(
                serviceProvider.GetRequiredService<idunno.Bluesky.AspNet.Labeler.UI.LabelerUiAdministration>()));
        services.TryAddScoped<idunno.Bluesky.AspNet.Labeler.UI.LabelerUiAdministration>(
            serviceProvider => new idunno.Bluesky.AspNet.Labeler.UI.LabelerUiAdministration(
                serviceProvider.GetRequiredService<idunno.Bluesky.Labeler.Administration.ILabelAdministrationStore>(),
                serviceProvider.GetRequiredService<idunno.Bluesky.AspNet.Labeler.AdministrationDispatcher>()));
        services.TryAddSingleton<IDidHandleResolver>(serviceProvider => new DidHandleCache(new DidHandleCacheOptions
        {
            LoggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>(),
            Size = 10_000,
            ResolutionTimeout = TimeSpan.FromSeconds(5)
        }));
        services.TryAddSingleton(TimeProvider.System);
        services.AddHostedService(serviceProvider => new idunno.Bluesky.AspNet.Labeler.UI.HandleSearchIndexer(
            store: serviceProvider.GetRequiredService<idunno.Bluesky.Labeler.Storage.ILabelStore>(),
            resolver: serviceProvider.GetRequiredService<IDidHandleResolver>(),
            clock: serviceProvider.GetRequiredService<TimeProvider>(),
            logger: serviceProvider.GetRequiredService<ILogger<idunno.Bluesky.AspNet.Labeler.UI.HandleSearchIndexer>>()));
        services.AddHttpClient(idunno.Bluesky.AspNet.Labeler.UI.SubjectPreviewService.ClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(8);
            client.MaxResponseContentBufferSize = 524_288;
        }).ConfigurePrimaryHttpMessageHandler(() =>
        {
            SocketsHttpHandler handler = SsrfSocketsHttpHandlerFactory.Create(new SsrfOptions
            {
                ConnectTimeout = TimeSpan.FromSeconds(5)
            });
            handler.AllowAutoRedirect = false;
            handler.UseCookies = false;
            handler.MaxConnectionsPerServer = 8;
            handler.PooledConnectionLifetime = TimeSpan.FromMinutes(5);
            return handler;
        });
        services.TryAddScoped<idunno.Bluesky.AspNet.Labeler.UI.SubjectPreviewService>(serviceProvider => new(
            clients: serviceProvider.GetRequiredService<IHttpClientFactory>(),
            loggerFactory: serviceProvider.GetRequiredService<ILoggerFactory>(),
            protection: serviceProvider.GetRequiredService<IDataProtectionProvider>(),
            clock: serviceProvider.GetRequiredService<TimeProvider>()));
        return services;
    }
}
