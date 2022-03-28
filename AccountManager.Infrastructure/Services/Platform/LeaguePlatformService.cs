﻿using AccountManager.Core.Interfaces;
using AccountManager.Core.Models;
using System.Diagnostics;
using System.Net.Http.Json;
using AccountManager.Core.Enums;
using AccountManager.Core.Factories;
using AccountManager.Core.Models.RiotGames.League.Requests;
using AccountManager.Infrastructure.Services.FileSystem;
using AccountManager.Core.Services;

namespace AccountManager.Infrastructure.Services.Platform
{
    public class LeaguePlatformService : IPlatformService
    {
        private readonly ITokenService _riotService;
        private readonly ILeagueClient _leagueClient;
        private readonly IRiotClient _riotClient;
        private readonly HttpClient _httpClient;
        private readonly AlertService _alertService;
        private readonly RiotLockFileService _riotFileSystemService;

        private Dictionary<string, string> RankColorMap = new Dictionary<string, string>()
        {
            {"iron", "#372826"},
            {"bronze", "#823012"},
            {"silver", "#7e878b"},
            {"gold", "#FFD700"},
            {"platinum", "#25cb6e"},
            {"diamond", "#9e7ad6"},
            {"master", "#f359f9"},
            {"grandmaster", "#f8848f"},
            {"challenger", "#4ee1ff"},
        };
        public LeaguePlatformService(ILeagueClient leagueClient, IRiotClient riotClient, GenericFactory<AccountType, ITokenService> tokenServiceFactory, 
            IHttpClientFactory httpClientFactory, RiotLockFileService riotFileSystemService, AlertService alertService )
        {
            _leagueClient = leagueClient;
            _riotClient = riotClient;
            _riotService = tokenServiceFactory.CreateImplementation(AccountType.Valorant);
            _httpClient = httpClientFactory.CreateClient("SSLBypass");
            _riotFileSystemService = riotFileSystemService;
            _alertService = alertService;
        }
        public async Task Login(Account account)
        {
            string token;
            string port;
            EventHandler riotClientOpen = null;
            try
            {
                foreach (var process in Process.GetProcesses())
                    if (process.ProcessName.Contains("League") || process.ProcessName.Contains("Riot"))
                        process.Kill();

                Process.Start(GetRiotExePath());

                await _riotFileSystemService.WaitForClientInit();

                var signInRequest = new LeagueSignInRequest
                {
                    Username = account.Username,
                    Password = account.Password,
                    StaySignedIn = true
                };

                _riotService.TryGetPortAndToken(out token, out port);

                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"riot:{token}")));
                _ = await _httpClient.DeleteAsync($"https://127.0.0.1:{port}/rso-auth/v1/authorization");
                var sessionCreateResponse = await _httpClient.PostAsJsonAsync($"https://127.0.0.1:{port}/rso-auth/v2/authorizations", new CreateAuthorizations());
                var Sesestr = await sessionCreateResponse.Content.ReadAsStringAsync();

                var loginResponse = await _httpClient.PutAsJsonAsync($"https://127.0.0.1:{port}/rso-auth/v1/session/credentials", signInRequest);
                var loginResponseStr = await loginResponse.Content.ReadAsStringAsync();
                var loginResponseObj = await loginResponse.Content.ReadFromJsonAsync<RiotLoginResponse>();

                if (!string.IsNullOrEmpty(loginResponseObj.Error))
                {
                    if (loginResponseObj.Error == "rate_limited")
{
                        _alertService.ErrorMessage = "Error logging in, too many attempts made. Try again later.";
                        return;
                    }
                }

                if (!string.IsNullOrEmpty(loginResponseObj?.Multifactor?.Email))
{
                    var twoFactorCode = await _alertService.PromptUserFor2FA(account, loginResponseObj?.Multifactor?.Email);
                    var mfLogin = await _httpClient.PutAsJsonAsync($"https://127.0.0.1:{port}/rso-auth/v1/session/multifactor", new MultifactorRequest()
                    {
                        Code = twoFactorCode,
                        Retry = false,
                        TrustDevice = true
                    });
                    var mfLoginResponse = await mfLogin.Content.ReadFromJsonAsync<RiotLoginResponse>();

                    if (!string.IsNullOrEmpty(mfLoginResponse?.Multifactor?.Email))
                    {
                        _alertService.ErrorMessage = "Incorrect code. Login failed.";
                        return;
                    }

                    StartLeague();
                }
                else
                {
                    StartLeague();
                }
            }
            catch
            {
                _alertService.ErrorMessage = "There was an error signing in.";
            }
        }
        private void StartLeague()
        {
            var startLeagueCommandline = "--launch-product=league_of_legends --launch-patchline=live";
            var startLeague = new ProcessStartInfo
            {
                FileName = GetRiotExePath(),
                Arguments = startLeagueCommandline
            };
            Process.Start(startLeague);
        }

        public async Task<(bool, Rank)> TryFetchRank(Account account)
        {
            Rank rank = new Rank();
            try
            {
                if (string.IsNullOrEmpty(account.PlatformId))
                    account.PlatformId = await _riotClient.GetPuuId(account.Username, account.Password);
                    if (string.IsNullOrEmpty(account.PlatformId))
                        return (false, rank);

                rank = await _leagueClient.GetSummonerRankByPuuidAsync(account);
                SetRankColor(rank);
                return (true, rank);
            }
            catch
            {
                return (false, rank);
            }
        }

        public async Task<(bool, string)> TryFetchId(Account account)
        {
            var id = "";
            try
            {
                if (!string.IsNullOrEmpty(account.PlatformId))
                    return (true, account.PlatformId);

                id = await _riotClient.GetPuuId(account.Username, account.Password);
                return (true, id);
            }
            catch
            {
                return (false, id);
            }
        }
        private void SetRankColor(Rank rank)
        {
            if (rank.Tier is null)
                return;

            foreach (KeyValuePair<string, string> kvp in RankColorMap)
                if (rank.Tier.ToLower().Equals(kvp.Key))
                     rank.Color = kvp.Value;
        }
        private DriveInfo FindRiotDrive()
        {
            DriveInfo riotDrive = null;
            foreach (DriveInfo drive in DriveInfo.GetDrives())
                if (Directory.Exists($"{drive.RootDirectory}\\Riot Games"))
                    riotDrive = drive;

            return riotDrive;
        }
        private string GetRiotExePath()
        {
            return @$"{FindRiotDrive().RootDirectory}\Riot Games\Riot Client\RiotClientServices.exe";
        }
    }
}
