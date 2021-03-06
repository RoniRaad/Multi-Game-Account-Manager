using Microsoft.AspNetCore.Components;
using AccountManager.Core.Models;
using Blazorise.Charts;
using AccountManager.Core.Models.Steam;
using Microsoft.Extensions.Caching.Distributed;
using AccountManager.Core.Static;

namespace AccountManager.Blazor.Components.AccountListTile.TileContent.Pages.Steam
{
    public partial class SteamFrontPage
    {
        public static int OrderNumber = 0;
        private Account _account = new();
        private bool steamInstallNotFound = false;
        [Parameter]
        public Account Account { get; set; } = new();
        List<SteamGameManifest> Games { get; set; } = new();

        protected override void OnInitialized()
        {
            _account = Account;
        }

        protected override async Task OnParametersSetAsync()
        {
            if (_account != Account)
            {
                _account = Account;
            }

            await base.OnParametersSetAsync();
        }
        public string SelectedSteamGame = "none";

        public void SetGame(string appId)
        {
            _persistantCache.SetString($"{Account.Guid}.SelectedSteamGame", appId);
        }

        public void OnRadioClicked(ChangeEventArgs args)
        {
            SetGame(args?.Value?.ToString() ?? "none");
            SelectedSteamGame = args?.Value?.ToString() ?? "none";
        }
        public async Task RefreshGamesAsync()
        {
            Games.Clear();

            SelectedSteamGame = await _persistantCache.GetStringAsync($"{Account.Guid}.SelectedSteamGame") ?? "none";
            if (!File.Exists(Path.Combine(_userSettings.Settings.SteamInstallDirectory, "steam.exe")))
            {
                steamInstallNotFound = true;
            }

            if (_steamLibraryService.TryGetGameManifests(out var gameManifests))
            {
                Games.AddRange(gameManifests);
            }

            Games.RemoveAll(game => game.Name == "Steamworks Common Redistributables" || (game.LastOwner != Account.PlatformId && _userSettings.Settings.OnlyShowOwnedSteamGames));
        }

        protected async override Task OnAfterRenderAsync(bool first)
        {
            var cachedSelectedGame = await _persistantCache.GetStringAsync($"{Account.Guid}.SelectedSteamGame") ?? "none";
            if (cachedSelectedGame != SelectedSteamGame)
            {
                SelectedSteamGame = cachedSelectedGame;
                StateHasChanged();
            }
            await base.OnAfterRenderAsync(first);
        }

        protected async override Task OnInitializedAsync()
        {
            await RefreshGamesAsync();
            await base.OnInitializedAsync();
        }
    }
}