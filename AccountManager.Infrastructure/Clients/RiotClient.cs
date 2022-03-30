﻿using AccountManager.Core.Interfaces;
using AccountManager.Core.Models;
using AccountManager.Core.Models.RiotGames;
using AccountManager.Core.Models.RiotGames.League.Requests;
using AccountManager.Core.Models.RiotGames.Valorant;
using AccountManager.Core.Models.RiotGames.Valorant.Responses;
using AccountManager.Core.Services;
using CloudFlareUtilities;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AccountManager.Core.Static;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace AccountManager.Infrastructure.Clients
{
    public partial class RiotClient : IRiotClient
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly AlertService _alertService;
        private readonly IMemoryCache _memoryCache;
        private readonly IDistributedCache _persistantCache;

        public RiotClient(IHttpClientFactory httpClientFactory, AlertService alertService, IMemoryCache memoryCache, IDistributedCache persistantCache)
        {
            _httpClientFactory = httpClientFactory;
            _alertService = alertService;
            _memoryCache = memoryCache;
            _persistantCache = persistantCache;
        }

        private async Task AddHeadersToClient(HttpClient httpClient)
        {
            if (httpClient.DefaultRequestHeaders.Contains("X-Riot-ClientVersion"))
                return;

            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Riot-ClientPlatform", "ew0KCSJwbGF0Zm9ybVR5cGUiOiAiUEMiLA0KCSJwbGF0Zm9ybU9TIjogIldpbmRvd3MiLA0KCSJwbGF0Zm9ybU9TVmVyc2lvbiI6ICIxMC4wLjE5MDQyLjEuMjU2LjY0Yml0IiwNCgkicGxhdGZvcm1DaGlwc2V0IjogIlVua25vd24iDQp9");
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Riot-ClientVersion", await GetExpectedClientVersion());
        }

        public async Task<string?> GetExpectedClientVersion()
        {
            var client = _httpClientFactory.CreateClient("CloudflareBypass");
            var response = await client.GetFromJsonAsync<ExpectedClientVersionResponse>("https://valorant-api.com/v1/version");
            return response?.Data?.RiotClientVersion;
        }

        public async Task<RiotAuthResponse> GetRiotClientInitialCookies(InitialAuthTokenRequest request, Account account)
        {
            var ssidCacheKey = $"{account.Username}.riot.auth.ssid";
            var cookieContainer = new CookieContainer();
            var cachedSessionCookie = await _persistantCache.GetAsync<Cookie>(ssidCacheKey);

            if (cachedSessionCookie is not null)
                cookieContainer.Add(cachedSessionCookie);

            var innerHandler = new HttpClientHandler()
            {
                CookieContainer = cookieContainer
            };
            var handler = new ClearanceHandler(innerHandler)
            {
                MaxRetries = 2
            };

            using (var client = new HttpClient(handler))
            {
                HttpResponseMessage authResponse;
                authResponse = await client.PostAsJsonAsync("https://auth.riotgames.com/api/v1/authorization", request);

                var authResponseDeserialized = await authResponse.Content.ReadFromJsonAsync<TokenResponseWrapper>();
                var authObject = new RiotAuthResponse()
                {
                    Content = authResponseDeserialized,
                    Cookies = MapCookies(cookieContainer.GetAllCookies())
                };

                return authObject;
            }
        }

        private RiotAuthCookies MapCookies(CookieCollection cookies)
        {
            return new RiotAuthCookies
            {
                Asid = cookies.FirstOrDefault((cookie) => cookie?.Name == "asid", null),
                Clid = cookies.FirstOrDefault((cookie) => cookie?.Name == "clid", null),
                Csid = cookies.FirstOrDefault((cookie) => cookie?.Name == "csid", null),
                Tdid = cookies.FirstOrDefault((cookie) => cookie?.Name == "tdid", null),
                Sub = cookies.FirstOrDefault((cookie) => cookie?.Name == "sub", null),
                Ssid = cookies.FirstOrDefault((cookie) => cookie?.Name == "ssid", null),
            };
        }

        public async Task<RiotAuthResponse> RiotAuthenticate(Account account, RiotAuthCookies initialCookies)
        {
            var ssidCacheKey = $"{account.Username}.riot.auth.ssid";
            var cookieContainer = new CookieContainer();
            cookieContainer.Add(initialCookies.GetCollection());

            var cachedSessionCookie = await _persistantCache.GetAsync<Cookie>(ssidCacheKey);
            if (cachedSessionCookie is not null)
                cookieContainer.Add(cachedSessionCookie);

            var innerHandler = new HttpClientHandler()
            {
                CookieContainer = cookieContainer
            };

            var handler = new ClearanceHandler(innerHandler)
            {
                MaxRetries = 2
            };

            using (var client = new HttpClient(handler))
            {

                HttpResponseMessage authResponse = await client.PutAsJsonAsync("https://auth.riotgames.com/api/v1/authorization", new AuthRequest
                {
                    Type = "auth",
                    Username = account.Username,
                    Password = account.Password
                });

                var tokenResponse = await authResponse.Content.ReadFromJsonAsync<TokenResponseWrapper>();

                if (tokenResponse?.Type == "multifactor")
                {
                    var mfCode = await _alertService.PromptUserFor2FA(account, tokenResponse.Multifactor.Email);

                    authResponse = await client.PutAsJsonAsync($"https://auth.riotgames.com/api/v1/authorization", new MultifactorRequest()
                    {
                        Code = mfCode,
                        Type = "multifactor",
                        RememberDevice = true
                    });

                    tokenResponse = await authResponse.Content.ReadFromJsonAsync<TokenResponseWrapper>();

                    if (tokenResponse?.Type == "multifactor")
                        _alertService.ErrorMessage = $"Incorrect code. Unable to authenticate {account.Username}";
                }

                var cookies = cookieContainer.GetAllCookies();
                var sessionCookie = cookies.FirstOrDefault((cookie) => cookie?.Name == "ssid", null);

                if (sessionCookie is not null)
                    await _persistantCache.SetAsync(ssidCacheKey, sessionCookie);

                var response = new RiotAuthResponse
                {
                    Content = tokenResponse,
                    Cookies = MapCookies(cookies)
                };

                return response;
            }
        }

        public async Task<string?> GetToken(Account account)
        {
            var initialAuthTokenRequest = new InitialAuthTokenRequest
            {
                Id = "play-valorant-web-prod",
                Nonce = "1",
                RedirectUri = "https://playvalorant.com/opt_in",
                ResponseType = "token id_token"
            };

            var riotAuthResponse = await GetRiotClientInitialCookies(initialAuthTokenRequest, account);
            if (riotAuthResponse?.Content?.Response?.Parameters is null)
                riotAuthResponse = await RiotAuthenticate(account, riotAuthResponse.Cookies);

            var matches = Regex.Matches(riotAuthResponse.Content.Response.Parameters.Uri,
                    @"access_token=((?:[a-zA-Z]|\d|\.|-|_)*).*id_token=((?:[a-zA-Z]|\d|\.|-|_)*).*expires_in=(\d*)");

            var token = matches[0].Groups[1].Value;

            return token;
        }

        public async Task<string> GetEntitlementToken(string token)
        {
            var client = _httpClientFactory.CreateClient("CloudflareBypass");

            await AddHeadersToClient(client);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var entitlementResponse = await client.PostAsJsonAsync("https://entitlements.auth.riotgames.com/api/token/v1", new { });
            var entitlementResponseDeserialized = await entitlementResponse.Content.ReadFromJsonAsync<EntitlementTokenResponse>();

            return entitlementResponseDeserialized.EntitlementToken;
        }

        public async Task<string?> GetPuuId(string username, string password)
        {
            var client = _httpClientFactory.CreateClient("CloudflareBypass");
            await AddHeadersToClient(client);

            var bearerToken = await GetToken(new Account
            {
                Username = username,
                Password = password
            });
            if (bearerToken is null)
                return null;

            var entitlementToken = await GetEntitlementToken(bearerToken);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-Riot-Entitlements-JWT", entitlementToken);

            var response = await client.GetFromJsonAsync<UserInfoResponse>("https://auth.riotgames.com/userinfo");
            return response?.PuuId;
        }

        public async Task<Rank> GetValorantRank(Account account)
        {
            int rankNumber;
            var client = _httpClientFactory.CreateClient("CloudflareBypass");
            await AddHeadersToClient(client);
            var bearerToken = await GetToken(account);
            if (bearerToken is null)
                return new Rank();

            var entitlementToken = await GetEntitlementToken(bearerToken);

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-Riot-Entitlements-JWT", entitlementToken);

            var response = await client.GetFromJsonAsync<ValorantRankedResponse>($"https://pd.na.a.pvp.net/mmr/v1/players/{account.PlatformId}");

            if (response?.QueueSkills?.Competitive?.TotalGamesNeededForRating > 0)
                return new Rank()
                {
                    Tier = "PLACEMENTS",
                    Ranking = $"{5 - response?.QueueSkills?.Competitive?.TotalGamesNeededForRating}/5"
                };
            else if (response?.QueueSkills?.Competitive?.CurrentSeasonGamesNeededForRating > 0)
                return new Rank()
                {
                    Tier = "PLACEMENTS",
                    Ranking = $"{1 - response?.QueueSkills?.Competitive?.CurrentSeasonGamesNeededForRating}/1"
                };
            else
                rankNumber = response?.LatestCompetitiveUpdate?.TierAfterUpdate ?? 0;

            var valorantRanking = new List<string>() {
                "IRON",
                "BRONZE",
                "SILVER" ,
                "GOLD" ,
                "PLATINUM" ,
                "DIAMOND" ,
                "IMMORTAL" ,
            };

            var rank = new Rank()
            {
                Tier = valorantRanking[rankNumber / 3],
                Ranking = new string('I', rankNumber % 3 + 1)
            };

            return rank;
        }
    }
    public class MultifactorRequest
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("code")]
        public string Code { get; set; }

        [JsonPropertyName("rememberDevice")]
        public bool RememberDevice { get; set; }
    }
}
