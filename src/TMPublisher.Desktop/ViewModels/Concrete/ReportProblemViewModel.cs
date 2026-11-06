using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using TMPublisher.Desktop.Localization;
using TMPublisher.Desktop.Os;
using TMPublisher.Desktop.Paths;
using TMPublisher.Desktop.Reports;
using TMPublisher.Runtime.Logging;
using TMPublisher.Runtime.Os;
using TMPublisher.Runtime.Paths;
using TMPublisher.Runtime.Reports;

namespace TMPublisher.Desktop.ViewModels;

/// <summary>
/// "Report a problem": nothing is sent by the program. The user can make a zip of the last days' logs (<see cref="LogBundle"/>) and see it in
/// the file manager, open GitHub's bug form with the version, the operating system and the architecture filled in (they add what happened and
/// drag the zip into it), or copy the same details.
/// </summary>
public sealed partial class ReportProblemViewModel(AppPaths paths, Localizer text, DrawingMode drawing, IFileReveal reveal, IUrlOpener browser,
    TimeProvider clock, ILogger<ReportProblemViewModel> logger) : ObservableObject
{
    /// <summary>The status line of the dialog.</summary>
    [ObservableProperty]
    public partial string Status { get; set; } = string.Empty;

    /// <summary>The last zip made in this dialog; <c>null</c> before.</summary>
    [ObservableProperty]
    public partial string? ZipPath { get; set; }

    /// <summary>Copies a text (set by the window: the clipboard belongs to it).</summary>
    public Func<string, Task>? CopyToClipboard { get; set; }

    /// <summary>Where the zips are made (shown in the dialog with the home folder as <c>~</c>).</summary>
    public string ReportFolder => LogScrubber.Scrub(paths.ReportDirectory);

    /// <summary>The <c>system.txt</c> text: program, version, system, language, drawing mode, optional programs. No settings, keys or names.</summary>
    public string Details => SystemReport.Text("TM Publisher", Facts());

    /// <summary>Makes the zip (off the window's thread: up to a week of files is read and masked) and shows it in the file manager.</summary>
    [RelayCommand]
    private async Task PrepareLogsAsync()
    {
        try
        {
            var details = Details;
            var now = clock.GetLocalNow();
            var zip = await Task.Run(() => LogBundle.Create(paths.LogDirectory, paths.ReportDirectory, ReportLinks.ZipName, details, now,
                (name, ex) => logger.LogWarning(ex, "A report zip file {File} could not be removed", name))).ConfigureAwait(true);
            ZipPath = zip;
            logger.LogInformation("Logs zip made: {File} ({Kilobytes} KB)", Path.GetFileName(zip), new FileInfo(zip).Length / 1024);
            Status = text.Format("report.zip_ready", LogScrubber.Scrub(zip));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            logger.LogError(ex, "The logs zip could not be made");
            Status = text.Format("error.generic", LogScrubber.Scrub(ex.Message));
            return;
        }

        try
        {
            reveal.Reveal(ZipPath);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            logger.LogWarning(ex, "The file manager could not be opened on the logs zip");
            Status = text.Format("report.zip_ready_no_folder", LogScrubber.Scrub(ZipPath));
        }
    }

    /// <summary>Opens the bug form on GitHub (the browser opens it; the program sends nothing).</summary>
    [RelayCommand]
    private void ReportOnGitHub()
    {
        var address = IssueAddress();
        if (browser.TryOpen(address))
        {
            logger.LogInformation("The GitHub bug form was opened in the browser");
            Status = text[ZipPath is null ? "report.github_opened_no_zip" : "report.github_opened"];
        }
        else
        {
            Status = text.Format("error.browser", address.AbsoluteUri);
        }
    }

    /// <summary>Copies the details to the clipboard.</summary>
    [RelayCommand]
    private async Task CopyDetailsAsync()
    {
        if (CopyToClipboard is not { } copy)
        {
            return;
        }

        try
        {
            await copy(Details).ConfigureAwait(true);
            logger.LogInformation("Problem report details copied to the clipboard");
            Status = text["report.copied"];
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException or TimeoutException)
        {
            logger.LogError(ex, "The details could not be copied to the clipboard");
            Status = text.Format("error.generic", ex.Message);
        }
    }

    /// <summary>The issue form address with the version, the operating system and the architecture filled in.</summary>
    public static Uri IssueAddress() => IssueLink.For(ReportLinks.Repository, ReportLinks.Template,
    [
        new(ReportLinks.VersionField, LogHeader.Version),
        new(ReportLinks.OsField, LogHeader.OperatingSystem),
        new(ReportLinks.ArchitectureField, LogHeader.Architecture),
    ]);

    private List<KeyValuePair<string, string>> Facts()
    {
        var facts = new List<KeyValuePair<string, string>>
        {
            new("Interface language", text.Language),
            new("Drawing", drawing.Gpu ? "graphics card (--gpu)" : "processor"),
            new("Data folder", paths.IsDefaultDirectory ? "default" : "own (--data-dir)"),
            new("Background scheduler program", ProgramFiles.Scheduler is null ? "missing" : "found"),
        };
        if (OperatingSystem.IsLinux())
        {
            facts.Add(new("secret-tool (keyring)", OnPath("secret-tool") ? "found" : "missing"));
            facts.Add(new("xdg-open", OnPath("xdg-open") ? "found" : "missing"));
            facts.Add(new("Graphical session", Environment.GetEnvironmentVariable("WAYLAND_DISPLAY") is { Length: > 0 } ? "Wayland"
                : Environment.GetEnvironmentVariable("DISPLAY") is { Length: > 0 } ? "X11" : "none"));
        }

        facts.Add(new("Log files (last " + LogLimits.KeepDays + " days)",
            LogBundle.LogFiles(paths.LogDirectory, DateOnly.FromDateTime(clock.GetLocalNow().DateTime)).Count.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        return facts;
    }

    private static bool OnPath(string program) =>
        (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Any(folder => File.Exists(Path.Combine(folder, program)));
}
