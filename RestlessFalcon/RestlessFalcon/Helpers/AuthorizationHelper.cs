using RestlessFalcon.Helpers.Impl;

namespace RestlessFalcon.Helpers;

public static class AuthorizationHelper
{
    public static bool IsAuthorized(AuthKeyHelper auth, string? key) =>
        !string.IsNullOrWhiteSpace(key) && auth.CheckAuthKeyValidity(key);
}
