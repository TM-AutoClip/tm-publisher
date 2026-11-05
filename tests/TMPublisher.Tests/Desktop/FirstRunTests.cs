using Avalonia.Headless.XUnit;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TMPublisher.Desktop.Os;
using TMPublisher.Desktop.Startup;
using TMPublisher.Desktop.ViewModels;
using TMPublisher.Desktop.Views;
using TMPublisher.Storage.Keys;

namespace TMPublisher.Tests.Desktop;

public sealed class FirstRunTests
{
    [AvaloniaFact]
    public async Task NewStartWithMasterPasswordCreatesTheDatabaseAndSavesSettings()
    {
        var autostart = new FakeAutostart();
        using var h = new DesktopHarness(s => s.AddSingleton<IAutostart>(autostart));
        var model = h.Services.GetRequiredService<FirstRunViewModel>();
        model.Step = FirstRunStep.New;
        var window = new FirstRunWindow { DataContext = model };
        window.Show();
        var completed = false;
        model.Completed += (_, _) => completed = true;

        model.UseMasterPassword = true;
        model.MasterPassword = "short";
        await model.CreateAsync();
        completed.ShouldBeFalse();
        model.Message.ShouldNotBeEmpty();

        model.MasterPassword = model.MasterPasswordRepeat = "a long master password";
        model.StartWithSystem = true;
        await model.CreateAsync();
        completed.ShouldBeTrue(model.Message);
        h.Services.GetRequiredService<Vault>().State.ShouldBe(VaultState.Open);
        h.Services.GetRequiredService<Vault>().Method.ShouldBe(KeyMethods.Password);
        autostart.IsEnabled.ShouldBeTrue();
        h.Services.GetRequiredService<AppSession>().Settings.StartWithSystem.ShouldBeTrue();
        window.Close();
    }
}
