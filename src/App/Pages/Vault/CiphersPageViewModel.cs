﻿using Bit.App.Abstractions;
using Bit.App.Resources;
using Bit.Core.Abstractions;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.View;
using Bit.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace Bit.App.Pages
{
    public class CiphersPageViewModel : BaseViewModel
    {
        private readonly IPlatformUtilsService _platformUtilsService;
        private readonly ICipherService _cipherService;
        private readonly ISearchService _searchService;
        private readonly IDeviceActionService _deviceActionService;
        private CancellationTokenSource _searchCancellationTokenSource;

        private string _searchText;
        private bool _showNoData;
        private bool _showList;

        public CiphersPageViewModel()
        {
            _platformUtilsService = ServiceContainer.Resolve<IPlatformUtilsService>("platformUtilsService");
            _cipherService = ServiceContainer.Resolve<ICipherService>("cipherService");
            _searchService = ServiceContainer.Resolve<ISearchService>("searchService");
            _deviceActionService = ServiceContainer.Resolve<IDeviceActionService>("deviceActionService");

            Ciphers = new ExtendedObservableCollection<CipherView>();
            CipherOptionsCommand = new Command<CipherView>(CipherOptionsAsync);
        }

        public Command CipherOptionsCommand { get; set; }
        public ExtendedObservableCollection<CipherView> Ciphers { get; set; }
        public Func<CipherView, bool> Filter { get; set; }
        public string AutofillUrl { get; set; }

        public string SearchText
        {
            get => _searchText;
            set => SetProperty(ref _searchText, value);
        }

        public bool ShowNoData
        {
            get => _showNoData;
            set => SetProperty(ref _showNoData, value);
        }

        public bool ShowList
        {
            get => _showList;
            set => SetProperty(ref _showList, value);
        }

        public void Search(string searchText, int? timeout = null)
        {
            var previousCts = _searchCancellationTokenSource;
            var cts = new CancellationTokenSource();
            Task.Run(async () =>
            {
                List<CipherView> ciphers = null;
                var searchable = !string.IsNullOrWhiteSpace(searchText) && searchText.Length > 1;
                if(searchable)
                {
                    if(timeout != null)
                    {
                        await Task.Delay(timeout.Value);
                    }
                    if(searchText != (Page as CiphersPage).SearchBar.Text)
                    {
                        return;
                    }
                    else
                    {
                        previousCts?.Cancel();
                    }
                    try
                    {
                        ciphers = await _searchService.SearchCiphersAsync(searchText, Filter, null, cts.Token);
                        cts.Token.ThrowIfCancellationRequested();
                        Ciphers.ResetWithRange(ciphers);
                        ShowNoData = Ciphers.Count == 0;
                    }
                    catch(OperationCanceledException)
                    {
                        ciphers = new List<CipherView>();
                    }
                }
                if(ciphers == null)
                {
                    ciphers = new List<CipherView>();
                }
                Ciphers.ResetWithRange(ciphers);
                ShowNoData = searchable && Ciphers.Count == 0;
                ShowList = searchable && !ShowNoData;
            }, cts.Token);
            _searchCancellationTokenSource = cts;
        }

        public async Task SelectCipherAsync(CipherView cipher)
        {
            string selection = null;
            if(!string.IsNullOrWhiteSpace(AutofillUrl))
            {
                var options = new List<string> { AppResources.Autofill };
                if(cipher.Type == CipherType.Login)
                {
                    options.Add(AppResources.AutofillAndSave);
                }
                options.Add(AppResources.View);
                selection = await Page.DisplayActionSheet(AppResources.AutofillOrView, AppResources.Cancel, null,
                    options.ToArray());
            }
            if(selection == AppResources.View || string.IsNullOrWhiteSpace(AutofillUrl))
            {
                var page = new ViewPage(cipher.Id);
                await Page.Navigation.PushModalAsync(new NavigationPage(page));
            }
            else if(selection == AppResources.Autofill || selection == AppResources.AutofillAndSave)
            {
                if(selection == AppResources.AutofillAndSave)
                {
                    var uris = cipher.Login?.Uris?.ToList();
                    if(uris == null)
                    {
                        uris = new List<LoginUriView>();
                    }
                    uris.Add(new LoginUriView
                    {
                        Uri = AutofillUrl,
                        Match = null
                    });
                    cipher.Login.Uris = uris;
                    try
                    {
                        await _deviceActionService.ShowLoadingAsync(AppResources.Saving);
                        await _cipherService.SaveWithServerAsync(await _cipherService.EncryptAsync(cipher));
                        await _deviceActionService.HideLoadingAsync();
                    }
                    catch(ApiException e)
                    {
                        await _deviceActionService.HideLoadingAsync();
                        await Page.DisplayAlert(AppResources.AnErrorHasOccurred, e.Error.GetSingleMessage(),
                            AppResources.Ok);
                    }
                }
                if(_deviceActionService.SystemMajorVersion() < 21)
                {
                    // TODO
                }
                else
                {
                    _deviceActionService.Autofill(cipher);
                }
            }
        }

        private async void CipherOptionsAsync(CipherView cipher)
        {
            if(!(Page as BaseContentPage).DoOnce())
            {
                return;
            }
            var option = await Page.DisplayActionSheet(cipher.Name, AppResources.Cancel, null, "1", "2");
            if(option == AppResources.Cancel)
            {
                return;
            }
            // TODO: process options
        }
    }
}
