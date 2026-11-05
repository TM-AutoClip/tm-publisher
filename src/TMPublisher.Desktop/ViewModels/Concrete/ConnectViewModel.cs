using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using TMPublisher.Core.Connections;
using TMPublisher.Core.Platforms;
using TMPublisher.Core.Publishing;
using TMPublisher.Desktop.Accounts;
using TMPublisher.Desktop.Localization;
using TMPublisher.Desktop.Os;

namespace TMPublisher.Desktop.ViewModels;

/// <summary>
/// Adding an account, everything in one window: the platform from a drop-down, the guide beside it step by step (each step is ticked),
/// the platform's warnings, the inputs (connecting is not possible while a required one is empty), and "Connect and test" last, which ends
/// with success or failure.
/// </summary>
public sealed partial class ConnectViewModel : ObservableObject, IConnectionPrompt
{
    private readonly AccountService _accounts;
    private readonly BrowserLauncher _browser;
    private readonly Localizer _text;
    private readonly ILogger<ConnectViewModel> _logger;
    private TaskCompletionSource<string>? _paste;
    private CancellationTokenSource? _running;

    /// <summary>Creates.</summary>
    public ConnectViewModel(IPlatformDirectory platforms, AccountService accounts, BrowserLauncher browser, Localizer text, ILogger<ConnectViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(platforms);
        _accounts = accounts;
        _browser = browser;
        _text = text;
        _logger = logger;
        Platforms = platforms.All.Select(p => new PlatformOption(p, p.DisplayName)).ToList();
    }

    /// <summary>Raised after a successful connection.</summary>
    public event EventHandler? Connected;

    /// <summary>Platforms.</summary>
    public IReadOnlyList<PlatformOption> Platforms { get; }

    /// <summary>Chosen platform.</summary>
    [ObservableProperty]
    public partial PlatformOption? Platform { get; set; }

    /// <summary>Guide steps.</summary>
    public ObservableCollection<GuideStepViewModel> Steps { get; } = [];

    /// <summary>Warnings of the platform.</summary>
    public ObservableCollection<string> Warnings { get; } = [];

    /// <summary>Inputs.</summary>
    public ObservableCollection<FieldViewModel> Fields { get; } = [];

    /// <summary>Status line.</summary>
    [ObservableProperty]
    public partial string Status { get; set; } = string.Empty;

    /// <summary>The result is a failure.</summary>
    [ObservableProperty]
    public partial bool Failed { get; set; }

    /// <summary>The result is a success.</summary>
    [ObservableProperty]
    public partial bool Succeeded { get; set; }

    /// <summary>The flow runs.</summary>
    [ObservableProperty]
    public partial bool Busy { get; set; }

    /// <summary>The paste box is shown.</summary>
    [ObservableProperty]
    public partial bool PasteVisible { get; set; }

    /// <summary>Prompt above the paste box.</summary>
    [ObservableProperty]
    public partial string PastePrompt { get; set; } = string.Empty;

    /// <summary>Pasted value.</summary>
    [ObservableProperty]
    public partial string PasteValue { get; set; } = string.Empty;

    /// <summary>Copies a value to the clipboard (set by the window).</summary>
    public Func<string, Task>? CopyToClipboard { get; set; }

    partial void OnPlatformChanged(PlatformOption? value)
    {
        Steps.Clear();
        Warnings.Clear();
        Fields.Clear();
        Status = string.Empty;
        Failed = Succeeded = false;
        if (value is null)
        {
            return;
        }

        foreach (var field in value.Platform.Connection.Fields)
        {
            Fields.Add(new FieldViewModel(field, _text[field.LabelKey], field.HintKey is null ? null : _text[field.HintKey]));
        }

        var number = 1;
        foreach (var step in value.Platform.Guide.Steps)
        {
            Func<string>? copy = step.CopyFieldKey is { } key ? () => Fields.FirstOrDefault(f => f.Field.Key == key)?.Value ?? string.Empty : null;
            Steps.Add(new GuideStepViewModel(number++, _text[step.TextKey], step.Link, copy));
        }

        foreach (var warning in value.Platform.Guide.WarningKeys)
        {
            Warnings.Add(_text[warning]);
        }
    }

    /// <summary>Whether every required input has a value.</summary>
    public bool CanConnect() => Platform is not null && !Busy && Fields.All(f => !f.Field.Required || f.IsReadOnly || f.Value.Trim().Length > 0);

    /// <summary>Connects and tests.</summary>
    [RelayCommand]
    public async Task ConnectAsync()
    {
        if (Platform is not { } option)
        {
            return;
        }

        if (!CanConnect())
        {
            Fail(_text["connect.fill_required"]);
            return;
        }

        Busy = true;
        Failed = Succeeded = false;
        _running = new CancellationTokenSource();
        try
        {
            var input = new ConnectionInput(Fields.ToDictionary(f => f.Field.Key, f => f.Value.Trim(), StringComparer.Ordinal));
            var result = await option.Platform.Connection.ConnectAsync(input, this, _running.Token).ConfigureAwait(true);
            var account = await _accounts.SaveAsync(option.Platform.Code, result, CancellationToken.None).ConfigureAwait(true);
            Succeeded = true;
            Status = _text.Format("connect.success", account.DisplayName, option.Name);
            Connected?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException)
        {
            Fail(_text["connect.canceled"]);
        }
        catch (Exception ex) when (ex is PlatformAuthException or PlatformRejectedException or PlatformQuotaException or ArgumentException or HttpRequestException)
        {
            _logger.LogWarning(ex, "Connecting a {Platform} account failed", option.Platform.Code);
            Fail(_text.Format("connect.failed", ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connecting a {Platform} account failed unexpectedly", option.Platform.Code);
            Fail(_text.Format("connect.failed", ex.Message));
        }
        finally
        {
            Busy = false;
            PasteVisible = false;
            _running.Dispose();
            _running = null;
        }
    }

    /// <summary>Stops a running flow.</summary>
    [RelayCommand]
    private void Cancel()
    {
        _running?.Cancel();
        _paste?.TrySetCanceled();
    }

    /// <summary>Sends the pasted value to the waiting flow.</summary>
    [RelayCommand]
    private void SubmitPaste()
    {
        PasteVisible = false;
        _paste?.TrySetResult(PasteValue);
    }

    /// <summary>Opens a guide link.</summary>
    [RelayCommand]
    private void OpenLink(GuideStepViewModel? step)
    {
        if (step?.Link is { } link)
        {
            _browser.Open(link);
        }
    }

    /// <summary>Copies a guide value.</summary>
    [RelayCommand]
    private async Task CopyAsync(GuideStepViewModel? step)
    {
        if (step is not null && CopyToClipboard is { } copy)
        {
            await copy(step.CopyValue).ConfigureAwait(true);
            Status = _text["connect.copied"];
        }
    }

    /// <inheritdoc />
    public Task OpenBrowserAsync(Uri address, CancellationToken cancellationToken)
    {
        _browser.Open(address);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<string> AskPasteAsync(string promptKey, CancellationToken cancellationToken)
    {
        _paste = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        PastePrompt = _text[promptKey];
        PasteValue = string.Empty;
        PasteVisible = true;
        using var registration = cancellationToken.Register(() => _paste.TrySetCanceled());
        return await _paste.Task.ConfigureAwait(true);
    }

    /// <inheritdoc />
    void IConnectionPrompt.Status(string messageKey) => Status = _text[messageKey];

    private void Fail(string message)
    {
        Failed = true;
        Status = message;
    }
}
