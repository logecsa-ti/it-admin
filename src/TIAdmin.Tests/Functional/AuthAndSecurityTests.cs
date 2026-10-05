namespace TIAdmin.Tests.Functional;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using TIAdmin.Application.Common.Constants;
using Xunit;

/// <summary>
/// Autenticacion, autorizacion y seguridad transversal sobre el pipeline HTTP real.
/// </summary>
public sealed class AuthAndSecurityTests(TIAdminApiFactory factory) : IClassFixture<TIAdminApiFactory>
{
    private const string ProtectedEndpoint = "/api/v1/departments";

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_ShouldReturnJson401WithoutRedirect()
    {
        using var client = factory.CreateAnonymousClient();

        var response = await client.GetAsync(ProtectedEndpoint);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.Location.Should().BeNull("una API JWT nunca redirige a una pagina de login");
        var body = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>();
        body!.Success.Should().BeFalse();
        body.Errors.Should().ContainSingle(e => e.Code == "UNAUTHORIZED");
    }

    [Fact]
    public async Task ProtectedEndpoint_WithTamperedToken_ShouldReturn401()
    {
        using var valid = await factory.CreateAdminClientAsync();
        var token = valid.DefaultRequestHeaders.Authorization!.Parameter!;
        using var client = factory.CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token[..^4] + "AAAA");

        var response = await client.GetAsync(ProtectedEndpoint);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithValidToken_ShouldReturn200()
    {
        using var client = await factory.CreateAdminClientAsync();

        var response = await client.GetAsync(ProtectedEndpoint);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ShouldReturn401InvalidCredentials()
    {
        using var client = factory.CreateAnonymousClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { userName = TIAdminApiFactory.AdminUserName, password = "Wrong123!Password" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>();
        body!.Errors.Should().ContainSingle(e => e.Code == "INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Login_WithUnknownUser_ShouldReturnSameErrorAsWrongPassword()
    {
        using var client = factory.CreateAnonymousClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { userName = "does.not.exist", password = "Whatever123!" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>();
        body!.Errors.Should().ContainSingle(e => e.Code == "INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Login_ShouldNotIssueCookies()
    {
        using var client = factory.CreateAnonymousClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { userName = TIAdminApiFactory.AdminUserName, password = TIAdminApiFactory.AdminPassword });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Contains("Set-Cookie").Should().BeFalse("la API es stateless y solo usa JWT");
    }

    [Fact]
    public async Task Login_WithEmptyBody_ShouldReturn400ValidationError()
    {
        using var client = factory.CreateAnonymousClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { userName = "", password = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>();
        body!.Errors.Should().NotBeEmpty().And.OnlyContain(e => e.Code == "VALIDATION_ERROR");
    }

    [Fact]
    public async Task Me_ShouldReturnRolesAndEffectivePermissions()
    {
        using var client = await factory.CreateAdminClientAsync();

        var response = await client.GetAsync("/api/v1/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiEnvelope<ProfileData>>();
        body!.Data!.UserName.Should().Be(TIAdminApiFactory.AdminUserName);
        body.Data.Roles.Should().Contain(SystemRoles.SuperAdmin);
        body.Data.Permissions.Should().Contain(Permissions.OrganizationManage);
    }

    [Fact]
    public async Task UserWithoutPermission_ShouldReceiveJson403()
    {
        using var client = await factory.CreateAssetManagerClientAsync();

        var read = await client.GetAsync(ProtectedEndpoint);
        var write = await client.PostAsJsonAsync(ProtectedEndpoint, new { code = "NOPE", name = "Sin permiso" });

        read.StatusCode.Should().Be(HttpStatusCode.OK, "TI_ASSET_MANAGER tiene ORGANIZATION.VIEW");
        write.StatusCode.Should().Be(HttpStatusCode.Forbidden, "TI_ASSET_MANAGER no tiene ORGANIZATION.MANAGE");
        var body = await write.Content.ReadFromJsonAsync<ApiEnvelope<object>>();
        body!.Errors.Should().ContainSingle(e => e.Code == "FORBIDDEN");
    }

    [Fact]
    public async Task Refresh_ShouldRotateTokenAndRejectReuse()
    {
        using var client = factory.CreateAnonymousClient();
        var login = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { userName = TIAdminApiFactory.AdminUserName, password = TIAdminApiFactory.AdminPassword });
        var original = (await login.Content.ReadFromJsonAsync<ApiEnvelope<LoginData>>())!.Data!;

        var refreshed = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = original.RefreshToken });
        var reused = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = original.RefreshToken });

        refreshed.StatusCode.Should().Be(HttpStatusCode.OK);
        var rotated = (await refreshed.Content.ReadFromJsonAsync<ApiEnvelope<LoginData>>())!.Data!;
        rotated.RefreshToken.Should().NotBe(original.RefreshToken);
        reused.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await reused.Content.ReadFromJsonAsync<ApiEnvelope<object>>())!.Errors
            .Should().ContainSingle(e => e.Code == "INVALID_REFRESH_TOKEN");
    }

    [Fact]
    public async Task Logout_ShouldRevokeRefreshToken()
    {
        using var anonymous = factory.CreateAnonymousClient();
        var login = await anonymous.PostAsJsonAsync("/api/v1/auth/login",
            new { userName = TIAdminApiFactory.AdminUserName, password = TIAdminApiFactory.AdminPassword });
        var tokens = (await login.Content.ReadFromJsonAsync<ApiEnvelope<LoginData>>())!.Data!;
        using var client = factory.CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var logout = await client.PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = tokens.RefreshToken });
        var refresh = await anonymous.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = tokens.RefreshToken });

        logout.StatusCode.Should().Be(HttpStatusCode.OK);
        refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task HealthLive_ShouldBeAnonymous()
    {
        using var client = factory.CreateAnonymousClient();

        var response = await client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Responses_ShouldIncludeSecurityHeadersAndEchoCorrelationId()
    {
        using var client = factory.CreateAnonymousClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("X-Correlation-Id", "test-correlation-123");

        var response = await client.SendAsync(request);

        response.Headers.GetValues("X-Correlation-Id").Should().ContainSingle().Which.Should().Be("test-correlation-123");
        response.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle().Which.Should().Be("nosniff");
        response.Headers.GetValues("X-Frame-Options").Should().ContainSingle().Which.Should().Be("DENY");
        response.Headers.Contains("Content-Security-Policy").Should().BeTrue();
    }

    [Fact]
    public async Task CorsPreflight_FromAllowedOrigin_ShouldBeAccepted()
    {
        using var client = factory.CreateAnonymousClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, ProtectedEndpoint);
        request.Headers.Add("Origin", TIAdminApiFactory.AllowedOrigin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "authorization");

        var response = await client.SendAsync(request);

        response.Headers.TryGetValues("Access-Control-Allow-Origin", out var origins).Should().BeTrue();
        origins.Should().ContainSingle().Which.Should().Be(TIAdminApiFactory.AllowedOrigin);
    }

    [Fact]
    public async Task CorsPreflight_FromUnknownOrigin_ShouldNotBeAllowed()
    {
        using var client = factory.CreateAnonymousClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, ProtectedEndpoint);
        request.Headers.Add("Origin", "https://evil.example");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await client.SendAsync(request);

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    private sealed record ProfileData(
        int Id,
        string UserName,
        IReadOnlyCollection<string> Roles,
        IReadOnlyCollection<string> Permissions);
}

/// <summary>
/// Usa su propia factory: consume el cupo del limitador de login.
/// </summary>
public sealed class LoginRateLimitTests(LowLoginLimitApiFactory factory) : IClassFixture<LowLoginLimitApiFactory>
{
    [Fact]
    public async Task Login_BeyondLimit_ShouldReturn429()
    {
        using var client = factory.CreateAnonymousClient();
        var statuses = new List<HttpStatusCode>();

        for (var i = 0; i <= LowLoginLimitApiFactory.Limit; i++)
        {
            var response = await client.PostAsJsonAsync("/api/v1/auth/login",
                new { userName = "nobody", password = "Nothing123!" });
            statuses.Add(response.StatusCode);
        }

        statuses.Take(LowLoginLimitApiFactory.Limit).Should().OnlyContain(s => s == HttpStatusCode.Unauthorized);
        statuses.Last().Should().Be(HttpStatusCode.TooManyRequests);
    }
}

/// <summary>
/// La renovacion tiene su propio limitador: agotar el de login (fuerza bruta de contrasenas) no debe
/// impedir que las sesiones validas se renueven al recargar la SPA.
/// </summary>
public sealed class RefreshRateLimitTests(LowLoginLimitApiFactory factory) : IClassFixture<LowLoginLimitApiFactory>
{
    [Fact]
    public async Task Refresh_WhenLoginLimitExhausted_ShouldStillSucceed()
    {
        using var client = factory.CreateAnonymousClient();
        var login = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { userName = TIAdminApiFactory.AdminUserName, password = TIAdminApiFactory.AdminPassword });
        var session = (await login.Content.ReadFromJsonAsync<ApiEnvelope<RefreshData>>())!.Data!;

        HttpStatusCode last;
        do
        {
            last = (await client.PostAsJsonAsync("/api/v1/auth/login", new { userName = "nobody", password = "Nothing123!" })).StatusCode;
        }
        while (last != HttpStatusCode.TooManyRequests);

        var refresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = session.RefreshToken });

        refresh.StatusCode.Should().Be(HttpStatusCode.OK, await refresh.Content.ReadAsStringAsync());
    }

    private sealed record RefreshData(string AccessToken, string RefreshToken);
}
