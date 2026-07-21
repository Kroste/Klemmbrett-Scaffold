using FluentAssertions;
using Klemmbrett.Logging;
using Xunit;

namespace Klemmbrett.Tests;

public class MaskedLayoutRendererTests
{
    [Theory]
    [InlineData("password=geheim123", "password=***")]
    [InlineData("Token: abc.def.ghi", "Token: ***")]
    [InlineData("api_key=sk-12345 weiter", "api_key=*** weiter")]
    [InlineData("Server=db1;User Id=app;Password=topsecret;", "Server=db1;User Id=app;Password=***;")]
    public void Mask_ReplacesSecrets(string input, string expected)
        => MaskedLayoutRenderer.Mask(input).Should().Be(expected);

    [Fact]
    public void Mask_LeavesNormalTextAlone()
        => MaskedLayoutRenderer.Mask("Scan von 192.168.10.0/24 fertig")
            .Should().Be("Scan von 192.168.10.0/24 fertig");
}
