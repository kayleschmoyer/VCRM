using System;
using System.Runtime.CompilerServices;

namespace CRMAdapter.Tests.SecurityTests;

internal static class SecurityTestEnvironment
{
    private static bool _initialized;

    [ModuleInitializer]
    public static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;

        SetIfMissing("CRM_JWT_SIGNING_KEY", "unit-test-signing-key");
        SetIfMissing("CRM_SQL_VAST_DESKTOP", "Server=localhost;Database=VastDesktop;Encrypt=True;TrustServerCertificate=False;");
        SetIfMissing("CRM_SQL_VAST_ONLINE", "Server=localhost;Database=VastOnline;Encrypt=True;TrustServerCertificate=False;");
        SetIfMissing("CRM_SQL_AUDIT", "Server=localhost;Database=Audit;Encrypt=True;TrustServerCertificate=False;");
        SetIfMissing("CRM_ENCRYPTION_KEY_CURRENT", Convert.ToBase64String(new byte[32]));
        SetIfMissing("CRM_API_CLIENT_ID", "test-client-id");
        SetIfMissing("CRM_API_CLIENT_SECRET", "test-client-secret");
    }

    private static void SetIfMissing(string name, string value)
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name)))
        {
            Environment.SetEnvironmentVariable(name, value);
        }
    }
}
