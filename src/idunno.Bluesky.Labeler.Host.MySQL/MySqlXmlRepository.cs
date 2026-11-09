// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Xml;
using System.Xml.Linq;

using Microsoft.AspNetCore.DataProtection.Repositories;

using MySqlConnector;

namespace idunno.Bluesky.Labeler.Host.MySQL;

/// <summary>Retains the host's append-only Data Protection keys and revocations in its external MySQL database.</summary>
internal sealed class MySqlXmlRepository(string connectionString) : IXmlRepository, ILabelerHostAuthenticationPersistence
{
    /// <summary>Creates authentication tables and the key repository before serving requests.</summary>
    [SuppressMessage("Security", "CA2100", Justification = "The SQL is a trusted embedded authentication schema, with no external input.")]
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        using Stream schema = typeof(Program).Assembly.GetManifestResourceStream("Labeler.AuthenticationSchema.sql")
            ?? throw new InvalidOperationException("The MySQL authentication schema is missing.");
        using StreamReader reader = new(schema);
        string sql = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        using MySqlConnection connection = new(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        using MySqlCommand command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS `labeler_data_protection_keys` (
                `id` BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                `friendly_name` TEXT NOT NULL,
                `xml` LONGTEXT NOT NULL
            ) ENGINE = InnoDB;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<XElement> GetAllElements()
    {
        using MySqlConnection connection = new(connectionString);
        connection.Open();
        using MySqlCommand command = connection.CreateCommand();
        command.CommandText = "SELECT `xml` FROM `labeler_data_protection_keys` ORDER BY `id`";
        using MySqlDataReader reader = command.ExecuteReader();
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
        using MySqlConnection connection = new(connectionString);
        connection.Open();
        using MySqlCommand command = connection.CreateCommand();
        command.CommandText = "INSERT INTO `labeler_data_protection_keys` (`friendly_name`, `xml`) VALUES (@name, @xml)";
        command.Parameters.AddWithValue("@name", friendlyName);
        command.Parameters.AddWithValue("@xml", element.ToString(SaveOptions.DisableFormatting));
        command.ExecuteNonQuery();
    }
}
