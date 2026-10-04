// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Runtime.InteropServices;

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

using Windows.ApplicationModel.Activation;

namespace Samples.WinUIOAuth;

internal static class Program
{
    private static readonly ConcurrentQueue<ActivationRequest> s_activations = new();
    private static MainWindow? s_window;

    // Redirection completes in the MTA, before starting the STA UI. Blocking an STA on
    // RedirectActivationToAsync can deadlock COM activation; no UI exists in the forwarding process.
    [MTAThread]
    private static void Main()
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();
        AppActivationArguments activation = AppInstance.GetCurrent().GetActivatedEventArgs();
        AppInstance instance = AppInstance.FindOrRegisterForKey("idunno.Bluesky.WinUIOAuth");

        if (!instance.IsCurrent)
        {
            instance.RedirectActivationToAsync(activation).AsTask().GetAwaiter().GetResult();
            return;
        }

        instance.Activated += OnActivated;
        s_activations.Enqueue(CaptureActivation(activation));

        Thread uiThread = new(() =>
        {
            WinRT.ComWrappersSupport.InitializeComWrappers();
            Application.Start(parameters =>
            {
                SynchronizationContext.SetSynchronizationContext(
                    new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
                _ = new App();
            });
        });
        uiThread.SetApartmentState(ApartmentState.STA);
        uiThread.Start();
        uiThread.Join();
        instance.Activated -= OnActivated;
        instance.UnregisterKey();
    }

    internal static void AttachWindow(MainWindow window)
    {
        Volatile.Write(ref s_window, window);
        DrainActivations();
    }

    private static void OnActivated(object? sender, AppActivationArguments args)
    {
        s_activations.Enqueue(CaptureActivation(args));
        MainWindow? window = Volatile.Read(ref s_window);
        if (window is not null && !window.DispatcherQueue.TryEnqueue(DrainActivations))
        {
            // Closing discards callbacks rather than transferring an OAuth secret to a new process.
            while (s_activations.TryDequeue(out _))
            {
            }
        }
    }

    private static ActivationRequest CaptureActivation(AppActivationArguments args)
    {
        // Redirected WinRT data can belong to the forwarding process. Copy it while the
        // Activated handler is running, before redirection completes and that process exits.
        try
        {
            if (args.Kind == ExtendedActivationKind.Launch)
            {
                return new ActivationRequest(IsLaunch: true);
            }

            if (args.Kind == ExtendedActivationKind.Protocol && args.Data is IProtocolActivatedEventArgs protocol)
            {
                return new ActivationRequest(IsLaunch: false, Callback: new Uri(protocol.Uri.OriginalString, UriKind.Absolute));
            }

            return new ActivationRequest(IsLaunch: false, Error: "Only launch and OAuth protocol activations are supported.");
        }
        catch (COMException)
        {
            return new ActivationRequest(IsLaunch: false, Error: "Windows could not deliver the activation. Cancel this login and start again.");
        }
        catch (UriFormatException)
        {
            return new ActivationRequest(IsLaunch: false, Error: "Windows delivered an invalid callback address.");
        }
    }

    private static void DrainActivations()
    {
        while (s_activations.TryDequeue(out ActivationRequest? activation))
        {
            s_window?.HandleActivation(activation);
        }
    }
}
