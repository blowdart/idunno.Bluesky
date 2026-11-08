// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Xml;
using System.Xml.Linq;

using Microsoft.AspNetCore.DataProtection.Repositories;

using Npgsql;

namespace idunno.Bluesky.Labeler.Host.PostgreSQL;

/// <summary>Retains the host's append-only Data Protection keys and revocations in its PostgreSQL database.</summary>
internal sealed class PostgreSqlXmlRepository(string connectionString) : IXmlRepository, ILabelerHostAuthenticationPersistence
{
    /// <summary>Creates authentication tables and the key repository before serving requests.</summary>
    [SuppressMessage("Security", "CA2100", Justification = "The SQL is a trusted embedded authentication schema, with no external input.")]
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        using Stream schema = typeof(Program).Assembly.GetManifestResourceStream("Labeler.AuthenticationSchema.sql")
            ?? throw new InvalidOperationException("The PostgreSQL authentication schema is missing.");
        using StreamReader reader = new(schema);
        string sql = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        command.CommandText = """

            CREATE TABLE IF NOT EXISTS labeler_data_protection_keys (
                id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                friendly_name TEXT NOT NULL,
                xml TEXT NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<XElement> GetAllElements()
    {
        using NpgsqlConnection connection = new(connectionString);
        connection.Open();
        using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = "SELECT xml FROM labeler_data_protection_keys ORDER BY id";
        using NpgsqlDataReader reader = command.ExecuteReader();
        List<XElement> elements = [];
        while (reader.Read())
        {
            using StringReader text = new(reader.GetString(0));
            using XmlReader xml = XmlReader.Create(text, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            });
            elements.Add(XElement.Load(xml));
        }

        return elements.AsReadOnly();
    }

    /// <inheritdoc/>
    public void StoreElement(XElement element, string friendlyName)
    {
        ArgumentNullException.ThrowIfNull(element);
        using NpgsqlConnection connection = new(connectionString);
        connection.Open();
        using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = "INSERT INTO labeler_data_protection_keys (friendly_name, xml) VALUES ($1, $2)";
        command.Parameters.AddWithValue(friendlyName);
        command.Parameters.AddWithValue(element.ToString(SaveOptions.DisableFormatting));
        command.ExecuteNonQuery();
    }
}
