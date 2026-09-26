// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Authentication;

namespace idunno.AtProto;

public partial class AtProtoAgent
{
    // Only forward access credentials if the session is authenticated and the service being called is the service the session is authenticated to.
    private AccessCredentials? GetSyncCredentials(Uri service)
    {
        if (IsAuthenticated && service == Service)
        {
            return Credentials;
        }

        if (IsAuthenticated)
        {
            Logger.CredentialsNotForwarded(_logger, Credentials.Service, service);
        }

        return null;
    }

    private async Task<(Did Did, Uri Pds)> ResolveSyncRepo(AtIdentifier repo, CancellationToken cancellationToken)
    {
        Did did = repo switch
        {
            Did repositoryDid => repositoryDid,
            Handle handle => await ResolveHandle(handle, cancellationToken).ConfigureAwait(false)
                ?? throw new ArgumentException($"{repo} cannot be resolved to a DID.", nameof(repo)),
            _ => throw new ArgumentException($"{repo} is not a valid AtIdentifier.", nameof(repo))
        };

        Uri pds = await ResolvePds(did, cancellationToken: cancellationToken).ConfigureAwait(false)
            ?? throw new ArgumentException($"{did} cannot be resolved to a personal data server.", nameof(repo));

        return (did, pds);
    }
}
