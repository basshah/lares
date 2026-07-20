using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Lares.Api.Contracts.Auth;
using Lares.Api.Contracts.Homes;

namespace Lares.Api.Tests;

public class HomeFlowTests(LaresApiFactory factory) : IClassFixture<LaresApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static RegisterRequest NewUser() =>
        new($"user-{Guid.NewGuid():N}@test.az", "Passw0rd123", "Test User");

    private async Task<AuthResponse> RegisterAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", NewUser());
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string accessToken, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        return await _client.SendAsync(request);
    }

    [Fact]
    public async Task CreateHome_ReturnsOwner_And_MeReflectsIt()
    {
        var auth = await RegisterAsync();

        var createResponse = await SendAsync(HttpMethod.Post, "/api/homes/create", auth.AccessToken,
            new CreateHomeRequest("Test Home"));
        createResponse.EnsureSuccessStatusCode();
        var home = await createResponse.Content.ReadFromJsonAsync<HomeDto>();

        Assert.Equal("Owner", home!.Role);
        Assert.NotNull(home.InviteCode);
        Assert.Single(home.Members);

        var meResponse = await SendAsync(HttpMethod.Get, "/api/homes/me", auth.AccessToken);
        meResponse.EnsureSuccessStatusCode();
        var me = await meResponse.Content.ReadFromJsonAsync<HomeDto>();
        Assert.Equal(home.Id, me!.Id);
    }

    [Fact]
    public async Task CreateHome_WhenAlreadyInHome_ReturnsBadRequest()
    {
        var auth = await RegisterAsync();
        await SendAsync(HttpMethod.Post, "/api/homes/create", auth.AccessToken, new CreateHomeRequest("First Home"));

        var response = await SendAsync(HttpMethod.Post, "/api/homes/create", auth.AccessToken,
            new CreateHomeRequest("Second Home"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiError>();
        Assert.Equal("ALREADY_IN_A_HOME", error!.Code);
    }

    [Fact]
    public async Task JoinHome_WithValidCode_AddsMember()
    {
        var owner = await RegisterAsync();
        var createResponse = await SendAsync(HttpMethod.Post, "/api/homes/create", owner.AccessToken,
            new CreateHomeRequest("Shared Home"));
        var home = await createResponse.Content.ReadFromJsonAsync<HomeDto>();

        var member = await RegisterAsync();
        var joinResponse = await SendAsync(HttpMethod.Post, "/api/homes/join", member.AccessToken,
            new JoinHomeRequest(home!.InviteCode!));
        joinResponse.EnsureSuccessStatusCode();
        var joined = await joinResponse.Content.ReadFromJsonAsync<HomeDto>();

        Assert.Equal("Member", joined!.Role);
        Assert.Null(joined.InviteCode);

        var ownerMeResponse = await SendAsync(HttpMethod.Get, "/api/homes/me", owner.AccessToken);
        var ownerMe = await ownerMeResponse.Content.ReadFromJsonAsync<HomeDto>();
        Assert.Equal(2, ownerMe!.Members.Count);
    }

    [Fact]
    public async Task JoinHome_WithInvalidCode_ReturnsBadRequest()
    {
        var auth = await RegisterAsync();

        var response = await SendAsync(HttpMethod.Post, "/api/homes/join", auth.AccessToken,
            new JoinHomeRequest("BADCODE1"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiError>();
        Assert.Equal("INVALID_INVITE_CODE", error!.Code);
    }

    [Fact]
    public async Task RegenerateInvite_AsNonOwner_Returns403_AsOwner_InvalidatesOldCode()
    {
        var owner = await RegisterAsync();
        var createResponse = await SendAsync(HttpMethod.Post, "/api/homes/create", owner.AccessToken,
            new CreateHomeRequest("Shared Home"));
        var home = await createResponse.Content.ReadFromJsonAsync<HomeDto>();

        var member = await RegisterAsync();
        await SendAsync(HttpMethod.Post, "/api/homes/join", member.AccessToken, new JoinHomeRequest(home!.InviteCode!));

        var forbiddenResponse = await SendAsync(HttpMethod.Post, "/api/homes/regenerate-invite", member.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);
        var error = await forbiddenResponse.Content.ReadFromJsonAsync<ApiError>();
        Assert.Equal("NOT_HOME_OWNER", error!.Code);

        var regenerateResponse = await SendAsync(HttpMethod.Post, "/api/homes/regenerate-invite", owner.AccessToken);
        regenerateResponse.EnsureSuccessStatusCode();
        var regenerated = await regenerateResponse.Content.ReadFromJsonAsync<RegenerateInviteResponse>();
        Assert.NotEqual(home.InviteCode, regenerated!.InviteCode);

        var oldCodeJoinResponse = await SendAsync(HttpMethod.Post, "/api/homes/join", (await RegisterAsync()).AccessToken,
            new JoinHomeRequest(home.InviteCode!));
        Assert.Equal(HttpStatusCode.BadRequest, oldCodeJoinResponse.StatusCode);
    }

    [Fact]
    public async Task Leave_AsMember_RemovesMembership()
    {
        var owner = await RegisterAsync();
        var createResponse = await SendAsync(HttpMethod.Post, "/api/homes/create", owner.AccessToken,
            new CreateHomeRequest("Shared Home"));
        var home = await createResponse.Content.ReadFromJsonAsync<HomeDto>();

        var member = await RegisterAsync();
        await SendAsync(HttpMethod.Post, "/api/homes/join", member.AccessToken, new JoinHomeRequest(home!.InviteCode!));

        var leaveResponse = await SendAsync(HttpMethod.Post, "/api/homes/leave", member.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, leaveResponse.StatusCode);

        var memberMeResponse = await SendAsync(HttpMethod.Get, "/api/homes/me", member.AccessToken);
        Assert.Equal(HttpStatusCode.NotFound, memberMeResponse.StatusCode);

        var ownerMeResponse = await SendAsync(HttpMethod.Get, "/api/homes/me", owner.AccessToken);
        var ownerMe = await ownerMeResponse.Content.ReadFromJsonAsync<HomeDto>();
        Assert.Single(ownerMe!.Members);
    }

    [Fact]
    public async Task Leave_AsOwner_IsBlocked()
    {
        var owner = await RegisterAsync();
        await SendAsync(HttpMethod.Post, "/api/homes/create", owner.AccessToken, new CreateHomeRequest("Solo Home"));

        var leaveResponse = await SendAsync(HttpMethod.Post, "/api/homes/leave", owner.AccessToken);

        Assert.Equal(HttpStatusCode.BadRequest, leaveResponse.StatusCode);
        var error = await leaveResponse.Content.ReadFromJsonAsync<ApiError>();
        Assert.Equal("OWNER_CANNOT_LEAVE", error!.Code);

        var meResponse = await SendAsync(HttpMethod.Get, "/api/homes/me", owner.AccessToken);
        meResponse.EnsureSuccessStatusCode();
    }
}
