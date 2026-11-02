// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;

namespace idunno.Bluesky.Labeler.Cli;

/// <summary>Protects Windows credentials with current-user DPAPI without an additional package.</summary>
[SupportedOSPlatform("windows")]
internal static class CliCredentialProtection
{
    /// <summary>Encrypts a credential for the current Windows user.</summary>
    /// <param name="plaintext">The complete credential envelope.</param>
    /// <returns>The protected envelope.</returns>
    internal static byte[] Protect(byte[] plaintext) => Transform(plaintext, protect: true);

    /// <summary>Decrypts a credential belonging to the current Windows user.</summary>
    /// <param name="ciphertext">The protected credential envelope.</param>
    /// <returns>The decrypted envelope.</returns>
    internal static byte[] Unprotect(byte[] ciphertext) => Transform(ciphertext, protect: false);

    /// <summary>Invokes DPAPI without permitting UI or machine-wide protection.</summary>
    private static byte[] Transform(byte[] input, bool protect)
    {
        DataBlob source = new() { Length = input.Length, Data = Marshal.AllocHGlobal(input.Length) };
        DataBlob destination = default;
        try
        {
            Marshal.Copy(input, 0, source.Data, input.Length);
            bool succeeded = protect ?
                CryptProtectData(ref source, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out destination) :
                CryptUnprotectData(ref source, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out destination);
            if (!succeeded)
            {
                throw new CryptographicException("Windows current-user credential protection failed.");
            }

            byte[] result = new byte[destination.Length];
            Marshal.Copy(destination.Data, result, 0, result.Length);
            return result;
        }
        finally
        {
            Marshal.Copy(new byte[input.Length], 0, source.Data, input.Length);
            Marshal.FreeHGlobal(source.Data);
            if (destination.Data != IntPtr.Zero)
            {
                Marshal.Copy(new byte[destination.Length], 0, destination.Data, destination.Length);
                _ = LocalFree(destination.Data);
            }
        }
    }

    /// <summary>Describes a DPAPI-owned or caller-owned native buffer.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        /// <summary>Contains the buffer length in bytes.</summary>
        [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Keeps descriptive interop field names together in the native buffer representation.")]
        internal int Length;

        /// <summary>Contains the buffer address.</summary>
        [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Keeps descriptive interop field names together in the native buffer representation.")]
        internal IntPtr Data;
    }

    /// <summary>Protects a buffer with the current user's Windows credentials.</summary>
    [DllImport("crypt32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    [SuppressMessage("Interoperability", "SYSLIB1054:Use LibraryImportAttribute instead of DllImportAttribute", Justification = "Uses runtime marshalling to avoid enabling unsafe code in the CLI project.")]
    private static extern bool CryptProtectData(ref DataBlob input, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out DataBlob output);

    /// <summary>Unprotects a buffer with the current user's Windows credentials.</summary>
    [DllImport("crypt32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    [SuppressMessage("Interoperability", "SYSLIB1054:Use LibraryImportAttribute instead of DllImportAttribute", Justification = "Uses runtime marshalling to avoid enabling unsafe code in the CLI project.")]
    private static extern bool CryptUnprotectData(ref DataBlob input, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out DataBlob output);

    /// <summary>Releases memory allocated by DPAPI.</summary>
    [DllImport("kernel32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [SuppressMessage("Interoperability", "SYSLIB1054:Use LibraryImportAttribute instead of DllImportAttribute", Justification = "Uses runtime marshalling to avoid enabling unsafe code in the CLI project.")]
    private static extern IntPtr LocalFree(IntPtr memory);
}
