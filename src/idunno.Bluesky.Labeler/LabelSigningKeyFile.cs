// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;

using idunno.AtProto.Labels;

namespace idunno.Bluesky.Labeler;

/// <summary>
/// Creates, loads, and backs up password-encrypted label signing key files.
/// </summary>
public static class LabelSigningKeyFile
{
    private const int MaximumFileLength = 64 * 1024;

    /// <summary>
    /// Generates a label signing key and saves its encrypted PKCS#8 representation without replacing an existing file.
    /// </summary>
    /// <param name="path">The destination file path.</param>
    /// <param name="curve">The curve for the generated key.</param>
    /// <param name="password">A nonempty password used to encrypt the key.</param>
    /// <param name="pbeParameters">The explicit encryption and password-derivation parameters.</param>
    /// <returns>The caller-owned generated key.</returns>
    /// <exception cref="ArgumentException"><paramref name="path"/> is blank or the password is empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="pbeParameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="IOException">The destination already exists, or the key cannot be saved.</exception>
    /// <exception cref="UnauthorizedAccessException">The key file cannot be created with private access permissions.</exception>
    /// <exception cref="CryptographicException">The provider cannot generate or encrypt the key.</exception>
    /// <remarks>
    /// <para>The key file is encrypted before it is written and is created atomically without overwriting an existing file. Files are created with access restricted to the current user. On Windows, the file ACL disables inherited access rules and grants full control only to the current Windows identity. Protect the destination directory against replacement or deletion by other users.</para>
    /// </remarks>
    public static LabelSigningKey GenerateEncryptedFile(
        string path,
        LabelSigningCurve curve,
        ReadOnlySpan<char> password,
        PbeParameters pbeParameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ValidatePassword(password);
        ArgumentNullException.ThrowIfNull(pbeParameters);

        string fullPath = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);
        string temporaryPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        LabelSigningKey key = LabelSigningKey.Generate(curve);
        byte[] encrypted = [];
        bool completed = false;

        try
        {
            encrypted = key.ExportEncryptedPkcs8PrivateKey(password, pbeParameters);
            using (FileStream stream = CreatePrivateFile(temporaryPath))
            {
                stream.Write(encrypted);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, fullPath);
            completed = true;
            return key;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encrypted);
            if (!completed)
            {
                key.Dispose();
            }

            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    /// <summary>
    /// Loads a password-encrypted PKCS#8 label signing key from a local file.
    /// </summary>
    /// <param name="path">The source file path.</param>
    /// <param name="password">The password used to encrypt the key.</param>
    /// <returns>A caller-owned signing key.</returns>
    /// <exception cref="ArgumentException"><paramref name="path"/> is blank or the password is empty.</exception>
    /// <exception cref="FileNotFoundException">The source file does not exist.</exception>
    /// <exception cref="InvalidDataException">The source file exceeds the supported size limit.</exception>
    /// <exception cref="CryptographicException">The password is incorrect or the file does not contain a supported encrypted PKCS#8 key.</exception>
    public static LabelSigningKey LoadEncryptedFile(string path, ReadOnlySpan<char> password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ValidatePassword(password);

        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is <= 0 or > MaximumFileLength)
        {
            throw new InvalidDataException($"The encrypted key file must be between 1 and {MaximumFileLength} bytes.");
        }

        byte[] encrypted = new byte[checked((int)stream.Length)];
        try
        {
            stream.ReadExactly(encrypted);
            return LabelSigningKey.ImportEncryptedPkcs8PrivateKey(encrypted, password);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encrypted);
        }
    }

    /// <summary>
    /// Copies an encrypted key file to a new backup path without replacing an existing backup.
    /// </summary>
    /// <param name="sourcePath">The encrypted source file.</param>
    /// <param name="backupPath">The new backup file path.</param>
    /// <exception cref="ArgumentException"><paramref name="sourcePath"/> or <paramref name="backupPath"/> is blank.</exception>
    /// <exception cref="FileNotFoundException">The source file does not exist.</exception>
    /// <exception cref="IOException">The backup already exists or cannot be created.</exception>
    /// <exception cref="UnauthorizedAccessException">The backup cannot be created with private access permissions.</exception>
    /// <remarks>
    /// <para>The backup remains encrypted with the source file's password and is created with access restricted to the current user, without inheriting the source file's access rules. Store the backup and its password separately from the label database and the running service.</para>
    /// </remarks>
    public static void CreateEncryptedBackup(string sourcePath, string backupPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(backupPath);

        string fullSourcePath = Path.GetFullPath(sourcePath);
        string fullBackupPath = Path.GetFullPath(backupPath);
        string directory = Path.GetDirectoryName(fullBackupPath)!;
        string temporaryPath = Path.Combine(directory, $".{Path.GetFileName(fullBackupPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            using FileStream source = new(fullSourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using (FileStream backup = CreatePrivateFile(temporaryPath))
            {
                source.CopyTo(backup);
                backup.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, fullBackupPath);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    /// <summary>Creates a new private file and rejects Windows filesystems that do not enforce its access rules.</summary>
    private static FileStream CreatePrivateFile(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            SecurityIdentifier user = identity.User
                ?? throw new UnauthorizedAccessException("The current Windows identity has no user security identifier.");
            FileSecurity security = new();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            security.AddAccessRule(new FileSystemAccessRule(user, FileSystemRights.FullControl, AccessControlType.Allow));

            FileStream? stream = null;
            try
            {
                stream = new FileInfo(path).Create(
                    mode: FileMode.CreateNew,
                    rights: FileSystemRights.FullControl,
                    share: FileShare.None,
                    bufferSize: 4096,
                    options: FileOptions.WriteThrough,
                    fileSecurity: security);
                FileSecurity actual = stream.GetAccessControl();
                AuthorizationRuleCollection rules = actual.GetAccessRules(
                    includeExplicit: true,
                    includeInherited: true,
                    targetType: typeof(SecurityIdentifier));
                if (!actual.AreAccessRulesProtected ||
                    rules.Count != 1 ||
                    rules[0] is not FileSystemAccessRule rule ||
                    rule.IsInherited ||
                    rule.IdentityReference != user ||
                    rule.AccessControlType != AccessControlType.Allow ||
                    rule.FileSystemRights != FileSystemRights.FullControl)
                {
                    throw new UnauthorizedAccessException("The filesystem did not enforce the private key file's access rules.");
                }

                FileStream result = stream;
                stream = null;
                return result;
            }
            finally
            {
                stream?.Dispose();
            }
        }

        return new FileStream(path, new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
            BufferSize = 4096,
            Options = FileOptions.WriteThrough,
            UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite
        });
    }

    private static void ValidatePassword(ReadOnlySpan<char> password)
    {
        if (password.IsEmpty)
        {
            throw new ArgumentException("A nonempty password is required.", nameof(password));
        }
    }
}
