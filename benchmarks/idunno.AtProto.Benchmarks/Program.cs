// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using BenchmarkDotNet.Running;

namespace idunno.AtProto.Benchmarks;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length > 0 && string.Equals(args[0], "capture", StringComparison.OrdinalIgnoreCase))
        {
            bool xrpcOnly = args.Length > 1 && string.Equals(args[1], "xrpc", StringComparison.OrdinalIgnoreCase);
            int directoryArgument = xrpcOnly ? 2 : 1;
            string outputDirectory = args.Length > directoryArgument ? args[directoryArgument] : Capture.DefaultOutputDirectory();

            string? handle = Environment.GetEnvironmentVariable(CaptureScrubber.HandleVariable);
            string? password = Environment.GetEnvironmentVariable(CaptureScrubber.PasswordVariable);
            if (string.IsNullOrWhiteSpace(handle) || string.IsNullOrWhiteSpace(password))
            {
                await Console.Error.WriteLineAsync(
                    $"Capturing the timeline needs a Bluesky account. Set the {CaptureScrubber.HandleVariable} and {CaptureScrubber.PasswordVariable} environment variables, or run benchmarks/capture.ps1 with -Handle and -Password.").ConfigureAwait(false);
                return 1;
            }

            await Capture.RunAsync(outputDirectory, xrpcOnly, handle, password).ConfigureAwait(false);
            return 0;
        }

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        return 0;
    }
}