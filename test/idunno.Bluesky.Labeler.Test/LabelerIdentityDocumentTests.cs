// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;
using System.Text.Json.Nodes;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler;

namespace idunno.Bluesky.Labeler.Test;

/// <summary>Checks label key custody helpers and public DID-document publication data.</summary>
public class LabelerIdentityDocumentTests
{
    [Fact]
    public void EncryptedKeyFilePreparationAndExportFailuresLeaveNoOutput()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string blocker = Path.Combine(directory, "blocker");
        string keyPath = Path.Combine(directory, "label-key.p8");
        File.WriteAllText(blocker, "not a directory");

        try
        {
            PbeParameters parameters = new(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 100_000);
            Assert.Throws<IOException>(() => LabelSigningKeyFile.GenerateEncryptedFile(
                path: Path.Combine(blocker, "label-key.p8"),
                curve: (LabelSigningCurve)(-1),
                password: "test-only-password",
                pbeParameters: parameters));
            PbeParameters unsupported = new(PbeEncryptionAlgorithm.Aes256Cbc, new HashAlgorithmName("Unsupported"), 1);
            Assert.Throws<CryptographicException>(() => LabelSigningKeyFile.GenerateEncryptedFile(
                path: keyPath,
                curve: LabelSigningCurve.P256,
                password: "test-only-password",
                pbeParameters: unsupported));
            Assert.False(File.Exists(keyPath));
            Assert.Equal([blocker], Directory.GetFiles(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("verificationMethod", "null")]
    [InlineData("service", "null")]
    [InlineData("verificationMethod", """{"type":"Multikey","controller":"did:web:labeler.example"}""")]
    [InlineData("service", """{"type":"AtprotoLabeler","serviceEndpoint":"https://labels.example"}""")]
    [InlineData("verificationMethod", """{"id":null,"type":"Multikey","controller":"did:web:labeler.example"}""")]
    [InlineData("service", """{"id":null,"type":"AtprotoLabeler","serviceEndpoint":"https://labels.example"}""")]
    [InlineData("verificationMethod", """{"id":"","type":"Multikey","controller":"did:web:labeler.example"}""")]
    [InlineData("service", """{"id":"","type":"AtprotoLabeler","serviceEndpoint":"https://labels.example"}""")]
    public void PublicationRejectsMalformedEntriesEvenAlongsideValidLabelerEntries(string collection, string malformedEntry)
    {
        Did did = new("did:web:labeler.example");
        Uri endpoint = new("https://labels.example");
        using LabelSigningKey signingKey = LabelSigningKey.Generate(LabelSigningCurve.P256);
        JsonObject document = JsonNode.Parse(LabelerIdentityDocument.CreateDidWebSnippet(did, signingKey, endpoint))!.AsObject();
        document["id"] = did.Value;
        document[collection]!.AsArray().Add(JsonNode.Parse(malformedEntry));
        JsonSerializerOptions options = new(AtProtoJsonSerializerOptions.Default);
#if NET9_0_OR_GREATER
        // Exercise the public verifier with malformed objects independently of serializer nullability enforcement.
        options.RespectNullableAnnotations = false;
#endif
        DidDocument resolved = JsonSerializer.Deserialize<DidDocument>(document.ToJsonString(), options)!;

        Assert.Throws<InvalidDataException>(() => LabelerIdentityDocument.VerifyPublication(
            didDocument: resolved,
            did: did,
            signingKey: signingKey,
            labelerEndpoint: endpoint));
    }

    [Fact]
    public void EncryptedKeyFileRoundTripsAndDoesNotOverwrite()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string keyPath = Path.Combine(directory, "label-key.p8");
        string backupPath = Path.Combine(directory, "label-key-backup.p8");
        const string password = "test-only-password";
        PbeParameters parameters = new(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 100_000);

        try
        {
            if (OperatingSystem.IsWindows())
            {
                DirectoryInfo parent = new(directory);
                DirectorySecurity security = parent.GetAccessControl();
                security.AddAccessRule(new FileSystemAccessRule(
                    identity: new SecurityIdentifier(WellKnownSidType.WorldSid, domainSid: null),
                    fileSystemRights: FileSystemRights.Read,
                    inheritanceFlags: InheritanceFlags.ObjectInherit | InheritanceFlags.ContainerInherit,
                    propagationFlags: PropagationFlags.None,
                    type: AccessControlType.Allow));
                parent.SetAccessControl(security);
            }

            using LabelSigningKey generated = LabelSigningKeyFile.GenerateEncryptedFile(
                path: keyPath,
                curve: LabelSigningCurve.P256,
                password: password,
                pbeParameters: parameters);
            using LabelSigningKey loaded = LabelSigningKeyFile.LoadEncryptedFile(keyPath, password);

            AssertPrivateFilePermissions(keyPath);
            Assert.Equal(generated.ExportPublicKeyMultibase(), loaded.ExportPublicKeyMultibase());
            Assert.Throws<IOException>(() => LabelSigningKeyFile.GenerateEncryptedFile(
                path: keyPath,
                curve: LabelSigningCurve.P256,
                password: password,
                pbeParameters: parameters));

            LabelSigningKeyFile.CreateEncryptedBackup(keyPath, backupPath);
            AssertPrivateFilePermissions(backupPath);
            Assert.Equal(File.ReadAllBytes(keyPath), File.ReadAllBytes(backupPath));
            Assert.Throws<IOException>(() => LabelSigningKeyFile.CreateEncryptedBackup(keyPath, backupPath));
            Assert.Throws<CryptographicException>(() => LabelSigningKeyFile.LoadEncryptedFile(keyPath, "wrong-password"));
            Assert.Empty(Directory.EnumerateFiles(directory, "*.tmp"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Checks that key files retain private access permissions after their final rename.</summary>
    private static void AssertPrivateFilePermissions(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            FileSecurity security = new FileInfo(path).GetAccessControl();
            Assert.True(security.AreAccessRulesProtected);
            AuthorizationRuleCollection rules = security.GetAccessRules(
                includeExplicit: true,
                includeInherited: true,
                targetType: typeof(SecurityIdentifier));
            FileSystemAccessRule rule = Assert.IsType<FileSystemAccessRule>(Assert.Single(rules.Cast<AuthorizationRule>()));
            Assert.False(rule.IsInherited);
            Assert.Equal(identity.User, rule.IdentityReference);
            Assert.Equal(AccessControlType.Allow, rule.AccessControlType);
            Assert.Equal(FileSystemRights.FullControl, rule.FileSystemRights);
        }
        else
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
        }
    }

    [Fact]
    public void DidWebSnippetContainsOnlyPublicEntriesAndVerifiesResolvedDocument()
    {
        Did did = new("did:web:labeler.example");
        Uri endpoint = new("https://labels.example");
        using LabelSigningKey signingKey = LabelSigningKey.Generate(LabelSigningCurve.P256);

        string snippet = LabelerIdentityDocument.CreateDidWebSnippet(did, signingKey, endpoint);
        JsonObject snippetObject = JsonNode.Parse(snippet)!.AsObject();
        Assert.Equal(2, snippetObject.Count);
        Assert.Contains("#atproto_label", snippet);
        Assert.Contains("#atproto_labeler", snippet);
        Assert.DoesNotContain("PRIVATE KEY", snippet, StringComparison.Ordinal);

        JsonObject documentObject = new()
        {
            ["id"] = did.ToString(),
            ["alsoKnownAs"] = new JsonArray("at://labeler.example"),
            ["verificationMethod"] = snippetObject["verificationMethod"]!.DeepClone(),
            ["service"] = snippetObject["service"]!.DeepClone()
        };
        DidDocument document = JsonSerializer.Deserialize<DidDocument>(
            documentObject.ToJsonString(),
            AtProtoJsonSerializerOptions.Default)!;

        LabelerIdentityDocument.VerifyPublication(document, did, signingKey, endpoint);
        Assert.Throws<InvalidDataException>(() =>
            LabelerIdentityDocument.VerifyPublication(document, did, signingKey, new Uri("https://wrong.example")));
        Assert.Throws<ArgumentException>(() =>
            LabelerIdentityDocument.CreateDidWebSnippet(did, signingKey, new Uri("http://labels.example")));
        Assert.Throws<ArgumentException>(() =>
            LabelerIdentityDocument.CreateDidWebSnippet(did, signingKey, new Uri("https://labels.example/xrpc")));
    }

    [Fact]
    public void DidWebServiceSnippetDoesNotReplaceUnrelatedDocumentProperties()
    {
        string snippet = LabelerIdentityDocument.CreateDidWebServiceSnippet(
            new Did("did:web:labeler.example"),
            new Uri("https://new-labels.example"));
        JsonObject snippetObject = JsonNode.Parse(snippet)!.AsObject();

        Assert.Equal(["service"], snippetObject.Select(property => property.Key));
        Assert.Single(snippetObject["service"]!.AsArray());
        Assert.Equal(
            "https://new-labels.example/",
            snippetObject["service"]![0]!["serviceEndpoint"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("https://operator@labels.example/")]
    [InlineData("https://operator:password@labels.example/")]
    [InlineData("https://labels.example/#unexpected")]
    public void PublicationRejectsEndpointComponentsIgnoredByUriEquality(string publishedEndpoint)
    {
        Did did = new("did:web:labeler.example");
        Uri endpoint = new("https://labels.example");
        using LabelSigningKey signingKey = LabelSigningKey.Generate(LabelSigningCurve.P256);
        JsonObject document = JsonNode.Parse(LabelerIdentityDocument.CreateDidWebSnippet(did, signingKey, endpoint))!.AsObject();
        document["id"] = did.Value;
        document["service"]![0]!["serviceEndpoint"] = publishedEndpoint;
        DidDocument resolved = JsonSerializer.Deserialize<DidDocument>(
            document.ToJsonString(),
            AtProtoJsonSerializerOptions.Default)!;

        Assert.Throws<InvalidDataException>(() => LabelerIdentityDocument.VerifyPublication(
            didDocument: resolved,
            did: did,
            signingKey: signingKey,
            labelerEndpoint: endpoint));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PublicationRejectsMissingOrNullServiceEndpoint(bool missing)
    {
        Did did = new("did:web:labeler.example");
        Uri endpoint = new("https://labels.example");
        using LabelSigningKey signingKey = LabelSigningKey.Generate(LabelSigningCurve.P256);
        JsonObject document = JsonNode.Parse(LabelerIdentityDocument.CreateDidWebSnippet(did, signingKey, endpoint))!.AsObject();
        document["id"] = did.Value;
        JsonObject service = document["service"]![0]!.AsObject();
        if (missing)
        {
            service.Remove("serviceEndpoint");
        }
        else
        {
            service["serviceEndpoint"] = null;
        }

        JsonSerializerOptions options = new(AtProtoJsonSerializerOptions.Default);
#if NET9_0_OR_GREATER
        // Exercise verifier validation independently of serializer nullability enforcement.
        options.RespectNullableAnnotations = false;
#endif
        DidDocument resolved = JsonSerializer.Deserialize<DidDocument>(document.ToJsonString(), options)!;

        Assert.Throws<InvalidDataException>(() => LabelerIdentityDocument.VerifyPublication(
            didDocument: resolved,
            did: did,
            signingKey: signingKey,
            labelerEndpoint: endpoint));
    }
}
