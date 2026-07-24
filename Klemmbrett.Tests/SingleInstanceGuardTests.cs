using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Klemmbrett.Services;
using Xunit;
using Xunit.v3;

namespace Klemmbrett.Tests;

public class SingleInstanceGuardTests
{
    private static string UniquePipeName() =>
        "Klemmbrett.Test." + Guid.NewGuid().ToString("N");

    [Fact]
    public void TryClaim_FirstInstance_Succeeds()
    {
        var pipe = UniquePipeName();
        using var sut = new SingleInstanceGuard(pipe);

        sut.TryClaim().Should().BeTrue();
        sut.IsPrimary.Should().BeTrue();
    }

    [Fact]
    public void TryClaim_SecondInstance_FailsWhilePrimaryLives()
    {
        var pipe = UniquePipeName();
        using var primary = new SingleInstanceGuard(pipe);
        primary.TryClaim().Should().BeTrue();

        using var second = new SingleInstanceGuard(pipe);
        second.TryClaim().Should().BeFalse();
        second.IsPrimary.Should().BeFalse();
    }

    [Fact]
    public async Task NotifyPrimary_FiresActivationRequestedOnPrimary()
    {
        var pipe = UniquePipeName();
        using var primary = new SingleInstanceGuard(pipe);
        primary.TryClaim().Should().BeTrue();

        using var signaled = new ManualResetEventSlim(false);
        primary.ActivationRequested += () => signaled.Set();

        using (var second = new SingleInstanceGuard(pipe))
            second.NotifyPrimary().Should().BeTrue();

        // Event kommt aus dem ThreadPool-Listener — kurz warten reicht.
        await Task.Yield();
        signaled.Wait(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken)
            .Should().BeTrue("die primäre Instance muss auf das Aktivierungssignal reagieren");
    }

    [Fact]
    public void Dispose_ReleasesPipe_SoNextClaimSucceeds()
    {
        var pipe = UniquePipeName();
        using (var first = new SingleInstanceGuard(pipe))
            first.TryClaim().Should().BeTrue();

        using var second = new SingleInstanceGuard(pipe);
        second.TryClaim().Should().BeTrue("nach Dispose muss die Pipe wieder frei sein");
    }
}
