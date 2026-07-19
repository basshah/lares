using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Lares.Api.Contracts.Auth;

namespace Lares.Api.Tests;

public class AuthFlowTests(LaresApiFactory factory) : IClassFixture<LaresApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static RegisterRequest NewUser() =>
        new($"user-{Guid.NewGuid():N}@test.az", "Passw0rd123", "Test User");

    private async Task<AuthResponse> RegisterAsync(RegisterRequest? request = null)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", request ?? NewUser());
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    [Fact]
    public async Task Register_ReturnsTokens_And_MeWorks()
    {
        var user = NewUser();
        var auth = await RegisterAsync(user);

        Assert.NotEmpty(auth.AccessToken);
        Assert.NotEmpty(auth.RefreshToken);
        Assert.Equal(user.Email, auth.User.Email);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var me = await response.Content.ReadFromJsonAsync<UserDto>();
        Assert.Equal(user.Email, me!.Email);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsEmailTaken()
    {
        var user = NewUser();
        await RegisterAsync(user);

        var response = await _client.PostAsJsonAsync("/api/auth/register", user);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiError>();
        Assert.Equal("EMAIL_TAKEN", error!.Code);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var user = NewUser();
        await RegisterAsync(user);

        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(user.Email, "WrongPassword1"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_RotatesToken_And_OldTokenDies()
    {
        var auth = await RegisterAsync();

        var refreshResponse = await _client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest(auth.RefreshToken));
        refreshResponse.EnsureSuccessStatusCode();
        var rotated = await refreshResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotEqual(auth.RefreshToken, rotated!.RefreshToken);

        var replayResponse = await _client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest(auth.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, replayResponse.StatusCode);
    }

    [Fact]
    public async Task Logout_RevokesRefreshToken()
    {
        var auth = await RegisterAsync();

        var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout")
        {
            Content = JsonContent.Create(new RefreshRequest(auth.RefreshToken)),
        };
        logoutRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var logoutResponse = await _client.SendAsync(logoutRequest);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        var refreshResponse = await _client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest(auth.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }
}
