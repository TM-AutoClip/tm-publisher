using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using TMPublisher.Runtime.Instance;

namespace TMPublisher.Tests.Runtime;

/// <summary>Finding a running TM Publisher 0.1.0–0.1.2 (its single-instance mutex) without mistaking the new scheduler for it.</summary>
public sealed class OldVersionGuardTests
{
    [Fact]
    public void TheNameIsTheOneTheOldVersionsUsed()
    {
        // 0.1.2: "TMPublisher-" + the first 16 hex digits of SHA-256 over the upper-case full path.
        var name = OldVersionGuard.NameOf(Path.Combine(Path.GetTempPath(), "tmp-data"));
        name.ShouldStartWith("TMPublisher-");
        name.Length.ShouldBe("TMPublisher-".Length + 16);
        OldVersionGuard.NameOf(Path.Combine(Path.GetTempPath(), "TMP-DATA")).ShouldBe(name);
    }

    [Fact]
    public void OnWindowsOnlyAnOwnedMutexIsAnOldVersionNotAnOpenHandle()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("The mutex is the Windows check.");
        }

        var guard = new OldVersionGuard(Path.Combine(Path.GetTempPath(), "tmpublisher-tests", Guid.NewGuid().ToString("N")), [], NullLogger<OldVersionGuard>.Instance);
        guard.OldVersionRuns().ShouldBeFalse();

        // A handle that only opens the name (does not own it) is not an old version.
        using (new Mutex(false, guard.Name, out _))
        {
            guard.OldVersionRuns().ShouldBeFalse();
        }

        // An old version owns the mutex on its main thread for its whole life.
        using var owned = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var oldVersion = new Thread(() =>
        {
            using var mutex = new Mutex(true, guard.Name, out _);
            owned.Set();
            release.Wait(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            mutex.ReleaseMutex();
        });
        oldVersion.Start();
        owned.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken).ShouldBeTrue();
        try
        {
            guard.OldVersionRuns().ShouldBeTrue();
        }
        finally
        {
            release.Set();
            oldVersion.Join();
        }

        guard.OldVersionRuns().ShouldBeFalse();
    }

    [Fact]
    public void AMutexFileOpenInAnySessionIsAnOldVersion()
    {
        var root = Path.Combine(Path.GetTempPath(), "tmpublisher-tests", "shm-" + Guid.NewGuid().ToString("N"));
        try
        {
            var guard = new OldVersionGuard(Path.Combine(root, "data"), [Path.Combine(root, "missing"), root], NullLogger<OldVersionGuard>.Instance);
            guard.AnyFileHeld().ShouldBeFalse();

            // What .NET leaves for a named mutex: <root>/.dotnet/shm/session<id>/<name>, kept open (shared lock) by every process that has it.
            var session = Path.Combine(root, ".dotnet", "shm", "session4242");
            Directory.CreateDirectory(Path.Combine(root, ".dotnet", "shm", "session17"));
            Directory.CreateDirectory(session);
            var file = Path.Combine(session, guard.Name);
            File.WriteAllBytes(file, new byte[16]);
            File.WriteAllBytes(Path.Combine(session, "TMPublisher-0000000000000000"), new byte[16]);

            // Left behind by a process that ended: nobody holds it.
            guard.AnyFileHeld().ShouldBeFalse();

            using (new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
            {
                guard.AnyFileHeld().ShouldBeTrue();
            }

            guard.AnyFileHeld().ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
