using System;
using FluentAssertions;
using Klemmbrett.Services;
using Xunit;

namespace Klemmbrett.Tests;

public class UpdateServiceTests
{
    [Theory]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("v1.2.3+abc123", "1.2.3")]       // MinVer-Build-Metadaten
    [InlineData("1.2.3-alpha.0.4", "1.2.3")]     // MinVer-Prerelease
    [InlineData("V2.0.0", "2.0.0")]
    public void ParseVersion_HandlesKrosteTagFormats(string tag, string expected)
        => UpdateService.ParseVersion(tag).Should().Be(Version.Parse(expected));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("kein-tag")]
    public void ParseVersion_ReturnsNullForGarbage(string? tag)
        => UpdateService.ParseVersion(tag).Should().BeNull();

    [Fact]
    public void ParseVersion_NormalizesMissingBuild_SoArityDoesNotMatter()
    {
        // "1.2" darf nicht als älter gelten als "1.2.0" (sonst wird ein Update verpasst)
        UpdateService.ParseVersion("1.2").Should().Be(UpdateService.ParseVersion("1.2.0"));
        (UpdateService.ParseVersion("1.3") > UpdateService.ParseVersion("1.2.0")).Should().BeTrue();
    }
}
