// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Buffers.Binary;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

using idunno.AtProto;
using idunno.Bluesky.Labeler;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace idunno.Bluesky.AspNet.Labeler;

/// <summary>Provides browserless local administration through an explicitly protected OS channel.</summary>
public static class LabelerLocalAdministration
{
    /// <summary>Registers a local-only pipe or Linux filesystem socket sharing the administrative dispatcher.</summary>
    /// <param name="services">The service collection with administration already registered.</param>
    /// <param name="endpoint">The Windows pipe name or absolute Linux socket filename in a private directory.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentException">The endpoint is invalid.</exception>
    /// <exception cref="InvalidOperationException">Administration is missing or local hosting is already registered.</exception>
    /// <exception cref="PlatformNotSupportedException">The OS has no implemented protected peer mechanism.</exception>
    /// <remarks><para>The channel permits the service OS identity only. Binding that identity to a DID is
    /// a separate explicit operation. Unix UID 0 is supported without requiring a browser or another account.
    /// Protect the socket directory's ancestors against replacement by other users.</para></remarks>
    public static IServiceCollection AddLabelerLocalAdministration(this IServiceCollection services, string endpoint)
    {
        ArgumentNullException.ThrowIfNull(services);
        ValidateEndpoint(endpoint);
        if (!services.Any(descriptor => descriptor.ServiceType == typeof(AdministrationDispatcher)) ||
            services.Any(descriptor => descriptor.ServiceType == typeof(LocalAdministrationHost)))
        {
            throw new InvalidOperationException("Register administration first and local hosting only once.");
        }

        services.AddSingleton(provider => new LocalAdministrationHost(
            endpoint: endpoint,
            dispatcher: provider.GetRequiredService<AdministrationDispatcher>(),
            store: provider.GetRequiredService<ILabelAdministrationStore>(),
            logger: provider.GetRequiredService<ILogger<LocalAdministrationHost>>()));
        services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<LocalAdministrationHost>());
        return services;
    }

    /// <summary>Bootstraps a verified administrator bound to the explicit setup process's OS identity.</summary>
    /// <param name="services">The configured service provider.</param>
    /// <param name="administrator">The administrator's DID or verified handle.</param>
    /// <param name="operationId">The stable setup retry identity.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The durable bootstrap result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    /// <exception cref="PlatformNotSupportedException">The current platform is not supported.</exception>
    /// <remarks><para>Invoke only from a deliberate setup process with provider initialization privileges.
    /// Never expose this method as an HTTP action or call it automatically on ordinary host startup.</para></remarks>
    public static Task<LabelServiceResult<LabelAdministrationReceipt>> BootstrapLocalAdministratorAsync(
        this IServiceProvider services,
        AtIdentifier administrator,
        string operationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        AdministrationConfiguration configuration = services.GetRequiredService<AdministrationConfiguration>();
        return new LabelTeamService(services.GetRequiredService<ILabelAdministrationStore>(), configuration.HttpClient).BootstrapAsync(
            administrator: administrator,
            localIdentity: CurrentIdentity(),
            operationId: operationId,
            cancellationToken: cancellationToken);
    }

    /// <summary>Sends one bounded JSON command over a protected local channel, never loopback TCP.</summary>
    /// <param name="endpoint">The configured pipe name or absolute socket filename.</param>
    /// <param name="command">The UTF-8 JSON administration command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The JSON response containing an explicit success or error outcome.</returns>
    /// <exception cref="ArgumentException">An endpoint or command is invalid.</exception>
    /// <exception cref="UnauthorizedAccessException">The channel peer is not the current OS identity.</exception>
    /// <exception cref="IOException">The local transport fails.</exception>
    /// <exception cref="PlatformNotSupportedException">The OS is unsupported.</exception>
    /// <remarks><para>The client and server must run as the same OS identity. A CLI connected over SSH
    /// can therefore act as root only when root has an explicit live team binding.</para></remarks>
    public static async Task<string> SendLocalCommandAsync(
        string endpoint,
        string command,
        CancellationToken cancellationToken = default)
    {
        ValidateEndpoint(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        byte[] payload = Encoding.UTF8.GetBytes(command);
        if (payload.Length > AdministrationWire.MaximumRequestBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(command));
        }

        if (OperatingSystem.IsWindows())
        {
            using NamedPipeClientStream pipe = new(
                serverName: ".",
                pipeName: endpoint,
                direction: PipeDirection.InOut,
                options: PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly,
                impersonationLevel: TokenImpersonationLevel.Identification);
            await pipe.ConnectAsync(cancellationToken).ConfigureAwait(false);
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            if (!Equals(pipe.GetAccessControl().GetOwner(typeof(SecurityIdentifier)), identity.User))
            {
                throw new UnauthorizedAccessException("The pipe owner differs from the expected service identity.");
            }

            await WriteFrameAsync(pipe, payload, cancellationToken).ConfigureAwait(false);
            return Encoding.UTF8.GetString(await ReadFrameAsync(pipe, 1024 * 1024, cancellationToken).ConfigureAwait(false));
        }

        using Socket socket = new(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await socket.ConnectAsync(new UnixDomainSocketEndPoint(endpoint), cancellationToken).ConfigureAwait(false);
        if (LocalPeer.GetUid(socket) != LocalPeer.EffectiveUid())
        {
            throw new UnauthorizedAccessException("The socket peer differs from the expected service identity.");
        }

        using NetworkStream stream = new(socket, ownsSocket: false);
        await WriteFrameAsync(stream, payload, cancellationToken).ConfigureAwait(false);
        return Encoding.UTF8.GetString(await ReadFrameAsync(stream, 1024 * 1024, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Gets an OS identity without trusting a request-provided UID, SID or username.</summary>
    internal static string CurrentIdentity()
    {
        if (OperatingSystem.IsWindows())
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            return "sid:" + (identity.User?.Value ?? throw new UnauthorizedAccessException("The process has no SID."));
        }

        if (OperatingSystem.IsLinux())
        {
            return "uid:" + LocalPeer.EffectiveUid().ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        throw new PlatformNotSupportedException("Protected local administration supports Windows and Linux.");
    }

    /// <summary>Rejects ambiguous pipe names, relative sockets and unimplemented local-channel platforms.</summary>
    internal static void ValidateEndpoint(string endpoint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        if (OperatingSystem.IsWindows())
        {
            if (endpoint.Length > 128 || endpoint.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_' or '.')))
            {
                throw new ArgumentException("Use a simple local pipe name.", nameof(endpoint));
            }
        }
        else if (!OperatingSystem.IsLinux() ||
            RuntimeInformation.ProcessArchitecture is not (Architecture.X64 or Architecture.Arm64))
        {
            throw new PlatformNotSupportedException("Protected local sockets support Linux x64 and arm64.");
        }
        else if (!Path.IsPathFullyQualified(endpoint) || Encoding.UTF8.GetByteCount(endpoint) > 100)
        {
            throw new ArgumentException("Use an absolute Linux socket filename no longer than 100 UTF-8 bytes.", nameof(endpoint));
        }
    }

    /// <summary>Reads a bounded network-order frame without waiting for a duplex stream to close.</summary>
    internal static async Task<byte[]> ReadFrameAsync(Stream stream, int maximum, CancellationToken token)
    {
        byte[] header = new byte[4];
        await stream.ReadExactlyAsync(header, token).ConfigureAwait(false);
        int length = BinaryPrimitives.ReadInt32BigEndian(header);
        if (length is <= 0 || length > maximum)
        {
            throw new InvalidDataException("The local administrative frame length is invalid.");
        }

        byte[] payload = new byte[length];
        await stream.ReadExactlyAsync(payload, token).ConfigureAwait(false);
        return payload;
    }

    /// <summary>Writes a bounded frame and flushes it before waiting for the peer.</summary>
    internal static async Task WriteFrameAsync(Stream stream, byte[] payload, CancellationToken token)
    {
        byte[] header = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(header, payload.Length);
        await stream.WriteAsync(header, token).ConfigureAwait(false);
        await stream.WriteAsync(payload, token).ConfigureAwait(false);
        await stream.FlushAsync(token).ConfigureAwait(false);
    }
}

/// <summary>Serves one deadline-bounded command per connection and reauthenticates every invocation.</summary>
internal sealed class LocalAdministrationHost(
    string endpoint,
    AdministrationDispatcher dispatcher,
    ILabelAdministrationStore store,
    ILogger<LocalAdministrationHost> logger) : BackgroundService
{
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Does not report host startup until the OS endpoint has been created successfully.</summary>
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await base.StartAsync(cancellationToken).ConfigureAwait(false);
        await _ready.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Propagates endpoint initialization failures to host startup as well as the background worker.</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await AcceptAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _ready.TrySetException(exception);
            throw;
        }
    }

    /// <summary>Accepts only OS-protected local connections and dispatches through shared services.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
        Justification = "Each accepted stream is asynchronously disposed in an unconditional finally before the next connection.")]
    private async Task AcceptAsync(CancellationToken stoppingToken)
    {
        if (OperatingSystem.IsWindows())
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            SecurityIdentifier sid = identity.User ?? throw new UnauthorizedAccessException("The service has no SID.");
            PipeSecurity security = new();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            security.SetOwner(sid);
            security.AddAccessRule(new PipeAccessRule(
                new SecurityIdentifier(WellKnownSidType.NetworkSid, null), PipeAccessRights.FullControl, AccessControlType.Deny));
            security.AddAccessRule(new PipeAccessRule(sid, PipeAccessRights.FullControl, AccessControlType.Allow));
            while (!stoppingToken.IsCancellationRequested)
            {
                NamedPipeServerStream pipe = NamedPipeServerStreamAcl.Create(
                    pipeName: endpoint, direction: PipeDirection.InOut, maxNumberOfServerInstances: 1,
                    transmissionMode: PipeTransmissionMode.Byte, options: PipeOptions.Asynchronous,
                    inBufferSize: 4096, outBufferSize: 4096, pipeSecurity: security);
                try
                {
                    _ready.TrySetResult();
                    await pipe.WaitForConnectionAsync(stoppingToken).ConfigureAwait(false);
                    string? peer = null;
                    pipe.RunAsClient(() =>
                    {
                        if (OperatingSystem.IsWindows())
                        {
                            using WindowsIdentity client = WindowsIdentity.GetCurrent();
                            peer = client.User?.Value;
                        }
                    });
                    if (peer != sid.Value)
                    {
                        AdministrationLog.Rejected(logger, "unexpected-sid");
                        continue;
                    }

                    await ProcessAsync(pipe, "sid:" + peer, stoppingToken).ConfigureAwait(false);
                }
                finally
                {
                    await pipe.DisposeAsync().ConfigureAwait(false);
                }
            }

            return;
        }

        string directory = Path.GetDirectoryName(endpoint)!;
        Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        LocalPeer.VerifyDirectory(directory);
        if (Path.Exists(endpoint))
        {
            throw new IOException("The local socket path already exists; verify ownership and remove a stale socket explicitly.");
        }

        using Socket listener = new(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        listener.Bind(new UnixDomainSocketEndPoint(endpoint));
        File.SetUnixFileMode(endpoint, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        listener.Listen(16);
        (ulong Device, ulong Inode) ownedSocket = LocalPeer.SocketIdentity(endpoint);
        _ready.TrySetResult();
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using Socket socket = await listener.AcceptAsync(stoppingToken).ConfigureAwait(false);
                uint peer = LocalPeer.GetUid(socket);
                if (peer != LocalPeer.EffectiveUid())
                {
                    AdministrationLog.Rejected(logger, "unexpected-uid");
                    continue;
                }

                NetworkStream stream = new(socket, ownsSocket: false);
                try
                {
                    await ProcessAsync(stream, "uid:" + peer.ToString(System.Globalization.CultureInfo.InvariantCulture), stoppingToken).ConfigureAwait(false);
                }
                finally
                {
                    await stream.DisposeAsync().ConfigureAwait(false);
                }
            }
        }
        finally
        {
            if (Path.Exists(endpoint) && LocalPeer.SocketIdentity(endpoint) == ownedSocket)
            {
                File.Delete(endpoint);
            }
        }
    }

    /// <summary>Authenticates before parsing input and bounds malformed requests without stopping the host.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S6667:Logging in a catch clause should pass the caught exception as a parameter",
        Justification = "Untrusted JSON and authentication exceptions may contain sensitive input; log only their category.")]
    private async Task ProcessAsync(Stream stream, string peer, CancellationToken stoppingToken)
    {
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            LabelServiceResult<LabelAdministrationCaller> caller = await store.AuthenticateLocalAsync(peer, deadline.Token).ConfigureAwait(false);
            AdministrationResponse response;
            if (!caller.Succeeded)
            {
                response = new(false, caller.Error.Code.ToString(), caller.Error.Message);
            }
            else
            {
                try
                {
                    byte[] payload = await LabelerLocalAdministration.ReadFrameAsync(stream, AdministrationWire.MaximumRequestBytes, deadline.Token).ConfigureAwait(false);
                    AdministrationCommand command = JsonSerializer.Deserialize(payload, AdministrationJsonContext.Default.AdministrationCommand)
                        ?? throw new JsonException("The command is null.");
                    response = await dispatcher.DispatchAsync(caller.Result, command, deadline.Token).ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is JsonException or ArgumentException or FormatException or InvalidDataException)
                {
                    AdministrationLog.Rejected(logger, exception.GetType().Name);
                    LabelServiceResult<LabelAdministrationReceipt> audit = await store.RecordAdministrationDenialAsync(
                        reason: "Malformed local administrative invocation.", caller: caller.Result, cancellationToken: deadline.Token).ConfigureAwait(false);
                    response = audit.Succeeded
                        ? new(false, "InvalidRequest", "The administrative command is invalid.")
                        : new(false, "ServiceUnavailable", "The required denial audit could not be persisted.");
                }
            }

            await LabelerLocalAdministration.WriteFrameAsync(
                stream, JsonSerializer.SerializeToUtf8Bytes(response, AdministrationJsonContext.Default.AdministrationResponse), deadline.Token).ConfigureAwait(false);
        }
        catch (IOException)
        {
            AdministrationLog.Rejected(logger, "connection-failed");
        }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
        {
            AdministrationLog.Rejected(logger, "deadline-exceeded");
        }
    }
}

/// <summary>Uses kernel peer credentials and Linux stat layouts without accepting caller-supplied identities.</summary>
internal static partial class LocalPeer
{
    /// <summary>Gets the service's effective Unix UID.</summary>
    [LibraryImport("libc", EntryPoint = "geteuid")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static partial uint EffectiveUid();

    /// <summary>Reads Linux SO_PEERCRED into a fixed pid/uid/gid structure.</summary>
    [LibraryImport("libc", EntryPoint = "getsockopt", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static partial int GetPeerCredential(
        System.Net.Sockets.SafeSocketHandle socket,
        int level,
        int option,
        out PeerCredential credential,
        ref uint length);

    /// <summary>Reads lstat without following a final-component symbolic link.</summary>
    [LibraryImport("libc", EntryPoint = "lstat", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static partial int ReadStat([In] byte[] path, [Out, MarshalAs(UnmanagedType.LPArray, SizeConst = 256)] byte[] stat);

    /// <summary>Gets a connected peer's UID from Linux rather than protocol input.</summary>
    internal static uint GetUid(Socket socket)
    {
        uint length = 12;
        if (GetPeerCredential(socket.SafeHandle, 1, 17, out PeerCredential credential, ref length) != 0 || length != 12)
        {
            throw new IOException("The OS could not establish the local peer identity.", new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError()));
        }

        return credential.Uid;
    }

    /// <summary>Requires a service-owned private directory, rejecting symlinks and permissive existing paths.</summary>
    internal static void VerifyDirectory(string path)
    {
        string? current = Path.GetFullPath(path);
        bool endpointDirectory = true;
        while (current is not null)
        {
            byte[] stat = Stat(current);
            (uint mode, uint owner) = ModeAndOwner(stat);
            bool privateDirectory = (mode & 0x1FF) == 0x1C0 && owner == EffectiveUid();
            bool safeAncestor = (owner == 0 || owner == EffectiveUid()) &&
                ((mode & 0x12) == 0 || (owner == 0 && (mode & 0x200) != 0));
            if ((mode & 0xF000) != 0x4000 || (endpointDirectory ? !privateDirectory : !safeAncestor))
            {
                throw new UnauthorizedAccessException("Use a service-owned mode-0700 socket directory with non-symlink, trusted ancestors.");
            }

            endpointDirectory = false;
            current = Path.GetDirectoryName(current);
        }
    }

    /// <summary>Identifies only a service-owned private socket, so cleanup never unlinks a replaced path.</summary>
    internal static (ulong Device, ulong Inode) SocketIdentity(string path)
    {
        byte[] stat = Stat(path);
        (uint mode, uint owner) = ModeAndOwner(stat);
        if ((mode & 0xF000) != 0xC000 || (mode & 0x1FF) != 0x180 || owner != EffectiveUid())
        {
            throw new UnauthorizedAccessException("The local endpoint must be a service-owned mode-0600 socket.");
        }

        return (BitConverter.ToUInt64(stat, 0), BitConverter.ToUInt64(stat, 8));
    }

    /// <summary>Reads one Linux stat structure without following its final component.</summary>
    private static byte[] Stat(string path)
    {
        byte[] stat = new byte[256];
        if (ReadStat(Encoding.UTF8.GetBytes(path + '\0'), stat) != 0)
        {
            throw new IOException("The OS could not verify the local socket directory.", new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError()));
        }

        return stat;
    }

    /// <summary>Reads architecture-dependent permission and ownership fields from Linux stat.</summary>
    private static (uint Mode, uint Owner) ModeAndOwner(byte[] stat)
    {
        // Linux's stat ABI places mode/uid at different offsets on these two supported architectures.
        int modeOffset = RuntimeInformation.ProcessArchitecture == Architecture.X64 ? 24 : 16;
        int uidOffset = RuntimeInformation.ProcessArchitecture == Architecture.X64 ? 28 : 24;
        return (BitConverter.ToUInt32(stat, modeOffset), BitConverter.ToUInt32(stat, uidOffset));
    }

    /// <summary>Matches Linux's fixed SO_PEERCRED structure.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private readonly struct PeerCredential
    {
#pragma warning disable IDE1006 // Native SO_PEERCRED fields retain their protocol names rather than managed private-field naming.
        internal readonly int Pid;
        internal readonly uint Uid;
        internal readonly uint Gid;
#pragma warning restore IDE1006
    }
}
