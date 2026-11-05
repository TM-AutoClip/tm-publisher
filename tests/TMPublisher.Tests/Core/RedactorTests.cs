using Shouldly;
using TMPublisher.Core.Security;

namespace TMPublisher.Tests.Core;

public sealed class RedactorTests
{
    [Theory]
    [InlineData("GET /x?access_token=abc123&a=1", "GET /x?access_token=***&a=1")]
    [InlineData("Authorization: Bearer abcdefghijkl", "Authorization: Bearer ***")]
    [InlineData("{\"refresh_token\":\"secret\"}", "{\"refresh_token\":\"***\"}")]
    [InlineData("https://discord.com/api/webhooks/1/abc", "https://discord.com/api/webhooks/***")]
    [InlineData("https://api.telegram.org/bot123:ABC_def/sendMessage", "https://api.telegram.org/bot***/sendMessage")]
    public void MasksSecrets(string input, string expected) => Redactor.Redact(input).ShouldBe(expected);
}
