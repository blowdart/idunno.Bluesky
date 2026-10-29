// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Xml;
using System.Xml.Linq;

using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Data.Sqlite;

namespace Xenon.TestHost;

/// <summary>Retains the test host's append-only Data Protection key ring in its SQLite database.</summary>
internal sealed class SqliteXmlRepository(string database) : IXmlRepository
{
    private readonly string _connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = database,
        Pooling = false
    }.ToString();

    /// <summary>Creates the repository and imports the previous file-backed key ring once, in one transaction.</summary>
    internal void Initialize(string directory, ILoggerFactory loggerFactory)
    {
        using SqliteConnection connection = new(_connectionString);
        connection.Open();
        using SqliteTransaction transaction = connection.BeginTransaction();
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'xenon_data_protection_keys'";
        if (Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) == 0)
        {
            command.CommandText = """
                CREATE TABLE xenon_data_protection_keys (
                    id INTEGER PRIMARY KEY,
                    friendly_name TEXT,
                    xml TEXT NOT NULL
                )
                """;
            command.ExecuteNonQuery();
            FileSystemXmlRepository previous = new(new DirectoryInfo(directory), loggerFactory);
            foreach (XElement element in previous.GetAllElements())
            {
                Insert(connection: connection, transaction: transaction, element: element, friendlyName: null);
            }
        }

        transaction.Commit();
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<XElement> GetAllElements()
    {
        using SqliteConnection connection = new(_connectionString);
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT xml FROM xenon_data_protection_keys ORDER BY id";
        using SqliteDataReader reader = command.ExecuteReader();
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
        using SqliteConnection connection = new(_connectionString);
        connection.Open();
        Insert(connection: connection, transaction: null, element: element, friendlyName: friendlyName);
    }

    /// <summary>Appends an XML element without replacing existing keys or revocation records.</summary>
    private static void Insert(SqliteConnection connection, SqliteTransaction? transaction, XElement element, string? friendlyName)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO xenon_data_protection_keys (friendly_name, xml) VALUES ($name, $xml)";
        command.Parameters.AddWithValue("$name", (object?)friendlyName ?? DBNull.Value);
        command.Parameters.AddWithValue("$xml", element.ToString(SaveOptions.DisableFormatting));
        command.ExecuteNonQuery();
    }
}
