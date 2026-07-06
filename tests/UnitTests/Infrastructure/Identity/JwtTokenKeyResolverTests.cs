using Microsoft.eShopWeb.Infrastructure.Identity;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.Infrastructure.Identity;

public class JwtTokenKeyResolverTests
{
    private const string ValidKey = "a-perfectly-valid-signing-key-of-32+bytes";

    private static IConfiguration BuildConfig(params (string Key, string? Value)[] pairs)
    {
        var dict = new Dictionary<string, string?>();
        foreach (var (key, value) in pairs)
            dict[key] = value;
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    [Fact]
    public void ReturnsConfiguredKey_WhenTokensKeyIsSet()
    {
        var config = BuildConfig((JwtTokenKeyResolver.ConfigurationKeyName, ValidKey));
        var result = JwtTokenKeyResolver.Resolve(config, isDevelopment: false);
        Assert.Equal(ValidKey, result);
    }

    [Fact]
    public void FallsBackToEnvironmentVariableEntry_WhenTokensKeyMissing()
    {
        var config = BuildConfig((JwtTokenKeyResolver.EnvironmentVariableName, ValidKey));
        var result = JwtTokenKeyResolver.Resolve(config, isDevelopment: false);
        Assert.Equal(ValidKey, result);
    }

    [Fact]
    public void PrefersTokensKey_WhenBothConfigured()
    {
        var other = "another-perfectly-valid-signing-key-of-32+b";
        var config = BuildConfig(
            (JwtTokenKeyResolver.ConfigurationKeyName, ValidKey),
            (JwtTokenKeyResolver.EnvironmentVariableName, other));
        var result = JwtTokenKeyResolver.Resolve(config, isDevelopment: false);
        Assert.Equal(ValidKey, result);
    }

    [Fact]
    public void ReturnsDevelopmentFallback_InDevelopment_WhenUnconfigured()
    {
        var config = BuildConfig();
        var result = JwtTokenKeyResolver.Resolve(config, isDevelopment: true);
        Assert.Equal(JwtTokenKeyResolver.DevelopmentOnlyKey, result);
    }

    [Fact]
    public void Throws_InProduction_WhenUnconfigured()
    {
        var config = BuildConfig();
        var ex = Assert.Throws<InvalidOperationException>(
            () => JwtTokenKeyResolver.Resolve(config, isDevelopment: false));
        Assert.Contains(JwtTokenKeyResolver.ConfigurationKeyName, ex.Message);
        Assert.Contains(JwtTokenKeyResolver.EnvironmentVariableName, ex.Message);
    }

    [Fact]
    public void Throws_WhenConfiguredKeyIsTooShort()
    {
        var config = BuildConfig((JwtTokenKeyResolver.ConfigurationKeyName, "short-key"));
        var ex = Assert.Throws<InvalidOperationException>(
            () => JwtTokenKeyResolver.Resolve(config, isDevelopment: true));
        Assert.Contains("32", ex.Message);
    }
}
