// JwtSession.cs: Captures the tokens and expiry returned from the CRM authentication API.
using System;

namespace CRMAdapter.UI.Auth.Contracts;

public sealed record JwtSession(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt);
