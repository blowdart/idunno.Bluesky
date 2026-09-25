// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace idunno.AtProto;

/// <summary>
/// Converts an <see cref="AccountStatus"/> to and from its JSON representation, mapping any status which is not
/// known to <see cref="AccountStatus.Unknown"/>.
/// </summary>
/// <remarks>
/// <para>The lexicon definition of an account status is an open list of known values, so the set of statuses is
/// decided by the service, not by this library. A status added upstream would otherwise make every response
/// carrying it fail to deserialize. Unknown statuses are surfaced as <see cref="AccountStatus.Unknown"/> so the
/// rest of the response can still be read.</para>
/// <para>Writing this converter by hand also avoids the trimming problems that the reflection based enum
/// converter exhibits. See https://github.com/dotnet/runtime/issues/114307.</para>
/// </remarks>
internal sealed class AccountStatusConverter : JsonConverter<AccountStatus>
{
    /// <inheritdoc/>
    public override AccountStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"Expected a string when reading an {nameof(AccountStatus)}, found {reader.TokenType}.");
        }

        return reader.GetString() switch
        {
            "takendown" => AccountStatus.Takendown,
            "suspended" => AccountStatus.Suspended,
            "deactivated" => AccountStatus.Deactivated,
            "deleted" => AccountStatus.Deleted,
            "throttled" => AccountStatus.Throttled,
            _ => AccountStatus.Unknown
        };
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, AccountStatus value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        switch (value)
        {
            case AccountStatus.Takendown:
                writer.WriteStringValue("takendown");
                break;

            case AccountStatus.Suspended:
                writer.WriteStringValue("suspended");
                break;

            case AccountStatus.Deactivated:
                writer.WriteStringValue("deactivated");
                break;

            case AccountStatus.Deleted:
                writer.WriteStringValue("deleted");
                break;

            case AccountStatus.Throttled:
                writer.WriteStringValue("throttled");
                break;

            default:
                throw new JsonException(
                    $"{nameof(AccountStatus)}.{nameof(AccountStatus.Unknown)} cannot be serialized. It only exists to carry a value this library does not recognize.");
        }
    }
}
