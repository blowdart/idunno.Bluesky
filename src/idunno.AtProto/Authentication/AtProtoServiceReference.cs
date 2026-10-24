// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

#pragma warning disable IDE0130 // Authentication features share the protocol's root namespace.
namespace idunno.AtProto;
#pragma warning restore IDE0130

/// <summary>
/// Identifies a service in a DID document by its DID and service fragment.
/// </summary>
public sealed class AtProtoServiceReference
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AtProtoServiceReference"/> class.
    /// </summary>
    /// <param name="did">The DID controlling the service.</param>
    /// <param name="fragment">The nonempty URI fragment, without its leading <c>#</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="did"/> or <paramref name="fragment"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="fragment"/> is empty or is not a valid URI fragment.</exception>
    public AtProtoServiceReference(Did did, string fragment)
    {
        ArgumentNullException.ThrowIfNull(did);
        ArgumentException.ThrowIfNullOrEmpty(fragment);

        int index = 0;
        while (index < fragment.Length)
        {
            char character = fragment[index];
            if (character == '%' && index + 2 < fragment.Length &&
                Uri.IsHexDigit(fragment[index + 1]) && Uri.IsHexDigit(fragment[index + 2]))
            {
                index += 2;
            }
            else if (!char.IsAsciiLetterOrDigit(character) && !"-._~!$&'()*+,;=:@/?".Contains(character, StringComparison.Ordinal))
            {
                throw new ArgumentException("The service fragment must be a valid URI fragment without a leading '#'.", nameof(fragment));
            }

            index++;
        }

        Did = did;
        Fragment = fragment;
    }

    /// <summary>
    /// Gets the DID controlling the service.
    /// </summary>
    public Did Did { get; }

    /// <summary>
    /// Gets the service fragment without a leading <c>#</c>.
    /// </summary>
    public string Fragment { get; }

    /// <summary>
    /// Returns the exact DID and fragment joined by <c>#</c>.
    /// </summary>
    /// <returns>The service reference.</returns>
    public override string ToString() => $"{Did}#{Fragment}";
}
