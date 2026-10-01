// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

namespace idunno.AtProto;

/// <summary>
/// Resolves the verified <see cref="Handle"/> for a <see cref="Did"/>.
/// </summary>
/// <remarks>
/// <para>A handle is only verified when the <see cref="DidDocument"/> for the <see cref="Did"/> declares it and the handle
/// resolves back to the same <see cref="Did"/>. An implementation returns <see cref="Handle.Invalid"/> when no declared handle
/// can be verified.</para>
/// </remarks>
/// <seealso cref="DidHandleCache"/>
public interface IDidHandleResolver
{
    /// <summary>
    /// Resolves the verified <see cref="Handle"/> for the specified <paramref name="did"/>.
    /// </summary>
    /// <param name="did">The <see cref="Did"/> to resolve the handle for.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>
    /// The task object representing the asynchronous operation, whose result is the verified <see cref="Handle"/> for
    /// <paramref name="did"/>, or <see cref="Handle.Invalid"/> if it could not be resolved or verified.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="did"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    ValueTask<Handle> ResolveHandleAsync(Did did, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the handle for the specified <paramref name="did"/> if it is already known, without resolving it.
    /// </summary>
    /// <param name="did">The <see cref="Did"/> to get the handle for.</param>
    /// <param name="handle">
    /// When this method returns, contains the known <see cref="Handle"/> for <paramref name="did"/>, which may be
    /// <see cref="Handle.Invalid"/> if it could not be verified, or <see langword="null"/> if none is known.
    /// This parameter is treated as uninitialized.
    /// </param>
    /// <returns><see langword="true"/> if a handle is known for <paramref name="did"/>; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="did"/> is <see langword="null"/>.</exception>
    bool TryGetCachedHandle(Did did, [NotNullWhen(true)] out Handle? handle);

    /// <summary>
    /// Forgets any handle known for the specified <paramref name="did"/>, so it is resolved again the next time it is needed.
    /// </summary>
    /// <param name="did">The <see cref="Did"/> whose handle is forgotten.</param>
    /// <exception cref="ArgumentNullException"><paramref name="did"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>Call this when an <c>#identity</c> event is received for <paramref name="did"/>, as its handle may have changed.</para>
    /// </remarks>
    void Invalidate(Did did);
}
