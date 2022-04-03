﻿using AccountManager.Core.Interfaces;
using AccountManager.Core.Models;
using System.Diagnostics;
using AccountManager.Core.Enums;
using AccountManager.Core.Factories;
using AccountManager.Infrastructure.Services.FileSystem;
using AccountManager.Core.Services;
using AccountManager.Core.Models.RiotGames.Requests;

namespace AccountManager.Infrastructure.Services.Platform
{
    public class TeamFightTacticsPlatformService : IPlatformService
    {
        private readonly ILeagueClient _leagueClient;
        private readonly IRiotClient _riotClient;
        private readonly RiotFileSystemService _riotFileSystemService;
        private readonly AlertService _alertService;
        private readonly Dictionary<string, string> RankColorMap = new Dictionary<string, string>()
        {
            {"iron", "#242424"},
            {"bronze", "#823012"},
            {"silver", "#7e878b"},
            {"gold", "#FFD700"},
            {"platinum", "#25cb6e"},
            {"diamond", "#9e7ad6"},
            {"master", "#f359f9"},
            {"grandmaster", "#f8848f"},
            {"challenger", "#4ee1ff"},
        };
        public TeamFightTacticsPlatformService(ILeagueClient leagueClient, IRiotClient riotClient, 
            RiotFileSystemService riotFileSystemService, AlertService alertService)
        {
            _leagueClient = leagueClient;
            _riotClient = riotClient;
            _riotFileSystemService = riotFileSystemService;
            _alertService = alertService;
        }

        public async Task Login(Account account)
        {
            try
            {
                foreach (var process in Process.GetProcesses())
                    if (process.ProcessName.Contains("League") || process.ProcessName.Contains("Riot"))
                        process.Kill();

                await _riotFileSystemService.WaitForClientClose();
                _riotFileSystemService.DeleteLockfile();

                var request = new InitialAuthTokenRequest
                {
                    Id = "riot-client",
                    Nonce = "1",
                    RedirectUri = "http://localhost/redirect",
                    ResponseType = "token id_token",
                    Scope = "openid offline_access lol ban profile email phone account"
                };

                var authResponse = await _riotClient.RiotAuthenticate(request, account);

                await _riotFileSystemService.WriteRiotYaml("NA", authResponse?.Cookies?.Tdid?.Value ?? "", authResponse?.Cookies?.Ssid?.Value ?? "",
                    authResponse?.Cookies?.Sub?.Value ?? "", authResponse?.Cookies?.Csid?.Value ?? "");

                StartLeague();
            }
            catch
            {
                _alertService.AddErrorMessage("There was an error signing in.");
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
            try
            {
                if (string.IsNullOrEmpty(account.PlatformId))
                    account.PlatformId = await _riotClient.GetPuuId(account.Username, account.Password);

                var rank = await _leagueClient.GetTFTRankByPuuidAsync(account);
                SetRankColor(rank);
                return (true, rank);
            }
            catch
            {
                return (false, new Rank());
            }
        }

        public async Task<(bool, string)> TryFetchId(Account account)
        {
            try
            {
                if (!string.IsNullOrEmpty(account.PlatformId))
                    return (true, account.PlatformId);

                var id = await _riotClient.GetPuuId(account.Username, account.Password);
                return (id is not null, id ?? string.Empty);
            }
            catch
            {
                return (false, string.Empty);
            }
        }

        private void SetRankColor(Rank rank)
        {
            if (rank.Tier is null)
                return;

            rank.Color = RankColorMap.FirstOrDefault((kvp) => rank.Tier.ToLower().Equals(kvp.Key)).Value;
        }

        private DriveInfo? FindRiotDrive()
        {
            DriveInfo? riotDrive = DriveInfo.GetDrives().FirstOrDefault(
                (drive) => Directory.Exists($"{drive?.RootDirectory}\\Riot Games"), null);

            return riotDrive;
        }

        private string GetRiotExePath()
        {
            return @$"{FindRiotDrive()?.RootDirectory}\Riot Games\Riot Client\RiotClientServices.exe";
        }
    }
}