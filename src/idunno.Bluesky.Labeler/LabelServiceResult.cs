// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

namespace idunno.Bluesky.Labeler;

/// <summary>Represents either a successful label-service result or a classified service error.</summary>
/// <typeparam name="T">The reference type returned on success.</typeparam>
/// <remarks>
/// <para>The result is created only through <see cref="Success"/> or <see cref="Failure"/>, ensuring exactly one outcome is present.</para>
/// <para>Cancellation, programming errors and unexpected implementation failures are not converted to this type.</para>
/// </remarks>
[SuppressMessage("Design", "CA1000:DoNotDeclareStaticMembersOnGenericTypes",
    Justification = "The approved typed success and failure factories enforce exactly one valid result state.")]
public sealed class LabelServiceResult<T> where T : class
{
    /// <summary>Initializes a new instance of the <see cref="LabelServiceResult{T}"/> class for the success and failure factories.</summary>
    /// <remarks><para>Keeping construction private prevents callers from creating outcomes with both a
    /// result and an error, or with neither.</para></remarks>
    private LabelServiceResult(T? result, LabelServiceError? error)
    {
        Result = result;
        Error = error;
    }

    /// <summary>Creates a successful result.</summary>
    /// <param name="result">A result value other than <see langword="null" />.</param>
    /// <returns>A successful service result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is <see langword="null"/>.</exception>
    public static LabelServiceResult<T> Success(T result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new LabelServiceResult<T>(result, null);
    }

    /// <summary>Creates a failed result.</summary>
    /// <param name="error">A classified error other than <see langword="null" />.</param>
    /// <returns>A failed service result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is <see langword="null"/>.</exception>
    public static LabelServiceResult<T> Failure(LabelServiceError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new LabelServiceResult<T>(null, error);
    }

    /// <summary>Gets a value that indicates whether the operation succeeded.</summary>
    /// <value><see langword="true"/> if the operation succeeded; otherwise, <see langword="false"/>.</value>
    [MemberNotNullWhen(true, nameof(Result))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool Succeeded => Result is not null;

    /// <summary>Gets the successful result value.</summary>
    /// <value>The result value, or <see langword="null"/> when the operation failed.</value>
    public T? Result { get; }

    /// <summary>Gets the classified failure.</summary>
    /// <value>The service error, or <see langword="null"/> when the operation succeeded.</value>
    public LabelServiceError? Error { get; }
}
