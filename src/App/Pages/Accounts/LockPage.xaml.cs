﻿using Bit.App.Models;
using System;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace Bit.App.Pages
{
    public partial class LockPage : BaseContentPage
    {
        private readonly AppOptions _appOptions;
        private readonly LockPageViewModel _vm;

        public LockPage(AppOptions appOptions = null)
        {
            _appOptions = appOptions;
            InitializeComponent();
            _vm = BindingContext as LockPageViewModel;
            _vm.Page = this;
            _vm.UnlockedAction = () =>
            {
                if(_appOptions != null)
                {
                    if(_appOptions.FromAutofillFramework && _appOptions.SaveType.HasValue)
                    {
                        Application.Current.MainPage = new NavigationPage(new AddEditPage(appOptions: _appOptions));
                        return;
                    }
                    else if(_appOptions.Uri != null)
                    {
                        Application.Current.MainPage = new NavigationPage(new AutofillCiphersPage(_appOptions));
                        return;
                    }
                }
                Application.Current.MainPage = new TabsPage();
            };
            MasterPasswordEntry = _masterPassword;
            PinEntry = _pin;
        }

        public Entry MasterPasswordEntry { get; set; }
        public Entry PinEntry { get; set; }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _vm.InitAsync();
            if(!_vm.FingerprintLock)
            {
                if(_vm.PinLock)
                {
                    RequestFocus(PinEntry);
                }
                else
                {
                    RequestFocus(MasterPasswordEntry);
                }
            }
        }

        private void Unlock_Clicked(object sender, EventArgs e)
        {
            if(DoOnce())
            {
                var tasks = Task.Run(async () =>
                {
                    await Task.Delay(50);
                    Device.BeginInvokeOnMainThread(async () =>
                    {
                        await _vm.SubmitAsync();
                    });
                });
            }
        }

        private async void LogOut_Clicked(object sender, EventArgs e)
        {
            if(DoOnce())
            {
                await _vm.LogOutAsync();
            }
        }

        private async void Fingerprint_Clicked(object sender, EventArgs e)
        {
            if(DoOnce())
            {
                await _vm.PromptFingerprintAsync();
            }
        }
    }
}
