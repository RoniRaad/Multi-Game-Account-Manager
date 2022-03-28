using Microsoft.AspNetCore.Components;
using AccountManager.Core.Models;
using AccountManager.Core.Interfaces;

namespace AccountManager.Blazor.Components
{
    public partial class AccountListItem
    {
        [Parameter, EditorRequired]
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public Account Account { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        [Parameter]
        public bool RenderButtons { get; set; }

        [Parameter, EditorRequired]
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public Action ReloadList { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        [Parameter, EditorRequired]
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public IAccountService AccountService { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        public bool EditPrompt { get; set; } = false;
        protected override void OnInitialized()
        {
            Account = Account ?? throw new ArgumentNullException();
            AccountService = AccountService ?? throw new ArgumentNullException();
            base.OnInitialized();
        }

        public void Delete()
        {
            if (Account is null)
                throw new ArgumentNullException();
            AccountService.RemoveAccount(Account);
            ReloadList();
        }
    }
}