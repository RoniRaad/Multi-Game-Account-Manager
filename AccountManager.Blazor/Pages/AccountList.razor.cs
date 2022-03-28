﻿using AccountManager.Core.Models;

namespace AccountManager.Blazor.Pages
{
    public partial class AccountList
    {
        private bool addAccountPrompt { get; set; } = false;
        public List<Account> ListItems = new List<Account>();
        private bool DragMode = false;
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
                LoadList();
        }
        public void SaveList()
        {
            _accountService.WriteAllAccounts(ListItems);
        }
        public void LoadList()
        {
            var accounts = _accountService.GetAllAccountsMin();
            ListItems = accounts;
            InvokeAsync(() => StateHasChanged());
            _ = Task.Run(async () =>
            {
                var fullAccounts = await _accountService.GetAllAccounts();
                ListItems = accounts;
                _ = InvokeAsync(() => StateHasChanged());
            });
        }
        public void StartAddAccount()
        {
            addAccountPrompt = true;
        }
        public void CancelAddAccount()
        {
            addAccountPrompt = false;
        }
        public void FinishAddAccount()
        {
            addAccountPrompt = false;
        }
    }
}
