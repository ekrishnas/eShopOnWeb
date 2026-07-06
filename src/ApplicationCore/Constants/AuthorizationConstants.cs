namespace Microsoft.eShopWeb.ApplicationCore.Constants;

public class AuthorizationConstants
{
    public const string AUTH_KEY = "AuthKeyOfDoomThatMustBeAMinimumNumberOfBytes";

    // Intentional demo seed password, shown on the login page by design.
    public const string DEFAULT_PASSWORD = "Pass@word1";

    // JWT signing key: resolved from configuration at runtime.
    // See src/Infrastructure/Identity/JwtTokenKeyResolver.cs.
}
