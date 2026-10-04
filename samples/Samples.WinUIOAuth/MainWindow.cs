// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky;
using idunno.Bluesky.Actor;

using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.System;

namespace Samples.WinUIOAuth;

internal sealed class MainWindow : Window
{
    private readonly TextBox _handle = new() { Header = "Bluesky handle", PlaceholderText = "you.bsky.social", MaxLength = 253 };
    private readonly Button _login = new() { Content = "Log in with OAuth" };
    private readonly Button _refresh = new() { Content = "Refresh profile", IsEnabled = false };
    private readonly Button _logout = new() { Content = "Log out", IsEnabled = false };
    private readonly Button _cancel = new() { Content = "Cancel", IsEnabled = false };
    private readonly TextBlock _status = new() { Text = "Log in to view your profile. No credentials are saved.", TextWrapping = TextWrapping.Wrap };
    private readonly Image _avatar = new() { Width = 96, Height = 96, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly TextBlock _displayName = new() { FontSize = 24, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _profileHandle = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _bio = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _counts = new();
    private readonly OAuthCallbackRouter _router = new();

    private BlueskyAgent? _agent;
    private TaskCompletionSource<(Uri Callback, OAuthLoginState State)>? _callback;
    private CancellationTokenSource? _operationCancellation;
    private Task _operation = Task.CompletedTask;
    private bool _busy;
    private bool _closing;
    private bool _allowClose;

    internal MainWindow()
    {
        Title = "idunno.Bluesky Windows OAuth sample";
        AppWindow.Resize(new Windows.Graphics.SizeInt32(620, 720));
        StackPanel buttons = new() { Orientation = Orientation.Horizontal, Spacing = 8 };
        buttons.Children.Add(_login);
        buttons.Children.Add(_refresh);
        buttons.Children.Add(_logout);
        buttons.Children.Add(_cancel);

        StackPanel panel = new() { Spacing = 16, Margin = new Thickness(24) };
        panel.Children.Add(_handle);
        panel.Children.Add(buttons);
        panel.Children.Add(_status);
        panel.Children.Add(_avatar);
        panel.Children.Add(_displayName);
        panel.Children.Add(_profileHandle);
        panel.Children.Add(_bio);
        panel.Children.Add(_counts);
        Content = new ScrollViewer() { Content = panel };
        AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);

        _login.Click += (_, _) => StartOperation(LoginAsync, "Login failed. Check your handle and connection, then try again.");
        _refresh.Click += (_, _) => StartOperation(RefreshProfileAsync, "Profile refresh failed. Check your connection or log in again.");
        _logout.Click += (_, _) => StartOperation(LogoutAsync, "Local credentials were cleared but could not logout from Bluesky.");
        _cancel.Click += (_, _) => _operationCancellation?.Cancel();
        _avatar.ImageFailed += (_, _) => SetStatus("The profile loaded, but its avatar could not be displayed.");
        AppWindow.Closing += OnClosing;
    }

    internal void HandleActivation(ActivationRequest activation)
    {
        if (_closing)
        {
            return;
        }

        Activate();
        if (activation.IsLaunch)
        {
            return;
        }

        if (activation.Error is not null)
        {
            SetStatus(activation.Error);
            return;
        }

        if (activation.Callback is not Uri callback)
        {
            SetStatus("Windows delivered no OAuth callback address.");
            return;
        }

        if (_operationCancellation?.IsCancellationRequested == true)
        {
            _router.Clear();
            SetStatus("This operation was canceled. The callback was rejected.");
            return;
        }

        try
        {
            OAuthLoginState state = _router.Take(callback);
            if (_callback is null || !_callback.TrySetResult((callback, state)))
            {
                SetStatus("The callback was rejected because this login is no longer waiting.");
            }
        }
        catch (InvalidOperationException exception)
        {
            // Router messages are fixed strings: never display the callback, code, or server error text.
            SetStatus(exception.Message);
        }
    }

    private void StartOperation(Func<CancellationToken, Task> action, string failureMessage)
    {
        if (_busy || _closing)
        {
            return;
        }

        _operation = RunOperationAsync(action, failureMessage);
    }

    private async Task RunOperationAsync(Func<CancellationToken, Task> action, string failureMessage)
    {
        _busy = true;
        using CancellationTokenSource cancellation = new(TimeSpan.FromMinutes(6));
        _operationCancellation = cancellation;
        UpdateButtons();
        try
        {
            await action(cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            SetStatus("Operation canceled or timed out. Logout revocation may be incomplete if it was canceled.");
        }
        catch (TimeoutException)
        {
            SetStatus("Login timed out. Start login again.");
        }
        catch (Exception exception) when (exception is OAuthException or CredentialException or LogoutException or
            AuthenticationRequiredException or HttpRequestException or ArgumentException or FormatException or
            System.Text.Json.JsonException or System.Runtime.InteropServices.COMException or System.Security.Cryptography.CryptographicException or
            Microsoft.IdentityModel.Tokens.SecurityTokenException)
        {
            // SDK/provider exception text can include sensitive data. Show only this operation's fixed message.
            SetStatus(failureMessage);
        }
        finally
        {
            _router.Clear();
            _callback = null;
            _operationCancellation = null;
            _busy = false;
            UpdateButtons();
        }
    }

    private async Task LoginAsync(CancellationToken cancellationToken)
    {
        if (!Handle.TryParse(_handle.Text.Trim().TrimStart('@'), out Handle? handle))
        {
            SetStatus("Enter a valid Bluesky handle, such as you.bsky.social.");
            return;
        }

        await ClearSessionAsync();
        SetStatus("Discovering your server and preparing OAuth...");
        BlueskyAgent agent = new(new BlueskyAgentOptions()
        {
            OAuthOptions = new OAuthOptions()
            {
                ClientId = OAuthCallbackRouter.ClientId,
                ReturnUri = new Uri(OAuthCallbackRouter.RedirectUri),
                Scopes = ["atproto", OAuthCallbackRouter.ProfileScope]
            }
        });

        OAuthLoginState? loginState = null;
        bool retainAgent = false;
        try
        {
            (Uri startUri, OAuthLoginState state) = await Task.Run(async () =>
            {
                OAuthClient client = agent.CreateOAuthClient();
                Uri uri = await agent.BuildOAuth2LoginUri(client, handle, cancellationToken: cancellationToken).ConfigureAwait(false);
                return (uri, client.State ?? throw new OAuthException("No login state was prepared."));
            }, cancellationToken);

            loginState = state;
            cancellationToken.ThrowIfCancellationRequested();
            _callback = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _router.Begin(state);
            SetStatus("Complete login in your browser. This request expires in five minutes.");
            if (!await Launcher.LaunchUriAsync(startUri))
            {
                SetStatus("Windows could not open the browser. Check your default browser and try again.");
                return;
            }

            (Uri callback, OAuthLoginState pendingState) = await _callback.Task.WaitAsync(OAuthCallbackRouter.LoginLifetime, cancellationToken);
            bool authenticated = await Task.Run(() => agent.ProcessOAuth2LoginResponse(
                agent.CreateOAuthClient(pendingState), callback.OriginalString, cancellationToken), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!authenticated || !agent.IsAuthenticated)
            {
                SetStatus("Login was denied or the OAuth response failed validation. Start login again.");
                return;
            }

            _agent = agent;
            SetStatus("Signed in. Loading your profile...");
            await RefreshProfileAsync(cancellationToken);
            retainAgent = ReferenceEquals(_agent, agent);
        }
        finally
        {
            if (loginState is not null)
            {
                loginState.ProofKey = string.Empty;
                loginState.CodeVerifier = string.Empty;
                loginState.State = string.Empty;
                loginState.StartUrl = null;
            }

            if (!retainAgent && ReferenceEquals(_agent, agent))
            {
                await ClearSessionAsync();
            }
            else if (!retainAgent)
            {
                await Task.Run(agent.Dispose, CancellationToken.None);
            }
        }
    }

    private async Task RefreshProfileAsync(CancellationToken cancellationToken)
    {
        BlueskyAgent? agent = _agent;
        AccessCredentials? credentials = agent?.Credentials;
        if (agent is null || credentials is null || !agent.IsAuthenticated)
        {
            await ClearSessionAsync();
            SetStatus("Your session has ended. Log in again.");
            return;
        }

        SetStatus("Loading your profile...");
        AtProtoHttpResult<ProfileViewDetailed> result = await Task.Run(
            () => agent.GetProfile(credentials.Did, cancellationToken: cancellationToken), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (!result.Succeeded)
        {
            SetStatus($"Profile request failed (HTTP {(int)result.StatusCode}). Retry or log out.");
            return;
        }

        ProfileViewDetailed profile = result.Result;
        _displayName.Text = profile.DisplayName ?? string.Empty;
        _profileHandle.Text = $"@{profile.Handle}";
        _bio.Text = profile.Description ?? string.Empty;
        _counts.Text = string.Format(CultureInfo.CurrentCulture, "{0:N0} followers  |  {1:N0} following", profile.FollowersCount, profile.FollowsCount);
        _avatar.Source = profile.Avatar is Uri avatar && avatar.Scheme == Uri.UriSchemeHttps &&
            string.IsNullOrEmpty(avatar.UserInfo) && !avatar.IsLoopback
            ? new BitmapImage(avatar)
            : null;
        SetStatus("Signed in. Only your profile is requested. The SDK refreshes tokens in memory.");
    }

    private async Task LogoutAsync(CancellationToken cancellationToken)
    {
        SetStatus("Revoking this session...");
        try
        {
            if (_agent is not null)
            {
                await Task.Run(() => _agent.Logout(cancellationToken), cancellationToken);
            }

            SetStatus("Logged out. Server tokens were revoked and local credentials cleared.");
        }
        finally
        {
            await ClearSessionAsync();
        }
    }

    private async Task ClearSessionAsync()
    {
        BlueskyAgent? agent = _agent;
        _agent = null;
        _avatar.Source = null;
        _displayName.Text = string.Empty;
        _profileHandle.Text = string.Empty;
        _bio.Text = string.Empty;
        _counts.Text = string.Empty;
        if (agent is not null)
        {
            await Task.Run(agent.Dispose);
        }
    }

    private void SetStatus(string message)
    {
        _status.Text = message;
        if (AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged))
        {
            AutomationPeer? peer = FrameworkElementAutomationPeer.FromElement(_status)
                ?? FrameworkElementAutomationPeer.CreatePeerForElement(_status);
            peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        }
    }

    private void UpdateButtons()
    {
        bool authenticated = _agent?.IsAuthenticated == true;
        _handle.IsEnabled = !_busy && !authenticated && !_closing;
        _login.IsEnabled = !_busy && !authenticated && !_closing;
        _refresh.IsEnabled = !_busy && authenticated && !_closing;
        _logout.IsEnabled = !_busy && _agent is not null && !_closing;
        _cancel.IsEnabled = _busy && !_closing;
    }

    private async void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_allowClose)
        {
            return;
        }

        args.Cancel = true;
        if (_closing)
        {
            return;
        }

        _closing = true;
        SetStatus("Closing and discarding the in-memory session...");
        UpdateButtons();
        _operationCancellation?.Cancel();
        await _operation;
        _router.Clear();
        _callback = null;
        await ClearSessionAsync();
        _allowClose = true;
        Close();
    }
}
