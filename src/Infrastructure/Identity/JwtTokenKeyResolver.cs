using System;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace Microsoft.eShopWeb.Infrastructure.Identity;

/// <summary>
/// Resolves the JWT signing key from configuration. Never hardcoded.
/// Supply via "Tokens:Key" config value or JWT_SECRET_KEY env var.
/// Missing in non-Development environments fails fast at startup.
/// </summary>
public static class JwtTokenKeyResolver
{
    public const string ConfigurationKeyName = "Tokens:Key";
    public const string EnvironmentVariableName = "JWT_SECRET_KEY";
    public const int MinimumKeyLengthBytes = 32;

    // Dev-only fallback: demo runs out-of-the-box; never returned outside Development.
    public const string DevelopmentOnlyKey = "DEVELOPMENT-ONLY-eShopOnWeb-signing-key!";

    public static string Resolve(IConfiguration configuration, bool isDevelopment)
    {
        var key = configuration[ConfigurationKeyName] ?? configuration[EnvironmentVariableName];

        if (string.IsNullOrWhiteSpace(key))
        {
            if (isDevelopment)
                return DevelopmentOnlyKey;

            throw new InvalidOperationException(
                $"JWT signing key is not configured. Set '{ConfigurationKeyName}' in configuration " +
                $"or the '{EnvironmentVariableName}' environment variable.");
        }

        if (Encoding.ASCII.GetByteCount(key) < MinimumKeyLengthBytes)
            throw new InvalidOperationException(
                $"JWT signing key must be at least {MinimumKeyLengthBytes} bytes; the configured key is too short.");

        return key;
    }
}
