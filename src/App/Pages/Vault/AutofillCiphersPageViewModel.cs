﻿using Bit.App.Abstractions;
using Bit.App.Models;
using Bit.App.Resources;
using Bit.Core;
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
    public class AutofillCiphersPageViewModel : BaseViewModel
    {
        private readonly IPlatformUtilsService _platformUtilsService;
        private readonly IDeviceActionService _deviceActionService;
        private readonly ICipherService _cipherService;

        private AppOptions _appOptions;
        private bool _showList;
        private string _noDataText;

        public AutofillCiphersPageViewModel()
        {
            _platformUtilsService = ServiceContainer.Resolve<IPlatformUtilsService>("platformUtilsService");
            _cipherService = ServiceContainer.Resolve<ICipherService>("cipherService");
            _deviceActionService = ServiceContainer.Resolve<IDeviceActionService>("deviceActionService");

            GroupedItems = new ExtendedObservableCollection<GroupingsPageListGroup>();
            CipherOptionsCommand = new Command<CipherView>(CipherOptionsAsync);
        }

        public string Name { get; set; }
        public string Uri { get; set; }
        public Command CipherOptionsCommand { get; set; }
        public ExtendedObservableCollection<GroupingsPageListGroup> GroupedItems { get; set; }

        public bool ShowList
        {
            get => _showList;
            set => SetProperty(ref _showList, value);
        }

        public string NoDataText
        {
            get => _noDataText;
            set => SetProperty(ref _noDataText, value);
        }

        public void Init(AppOptions appOptions)
        {
            _appOptions = appOptions;
            Uri = appOptions.Uri;
            string name = null;
            if(Uri.StartsWith(Constants.AndroidAppProtocol))
            {
                name = Uri.Substring(Constants.AndroidAppProtocol.Length);
            }
            else if(!System.Uri.TryCreate(Uri, UriKind.Absolute, out Uri uri) ||
                !DomainName.TryParseBaseDomain(uri.Host, out name))
            {
                name = "--";
            }
            Name = name;
            PageTitle = string.Format(AppResources.ItemsForUri, Name ?? "--");
            NoDataText = string.Format(AppResources.NoItemsForUri, Name ?? "--");
        }

        public async Task LoadAsync()
        {
            ShowList = false;
            var groupedItems = new List<GroupingsPageListGroup>();
            var ciphers = await _cipherService.GetAllDecryptedByUrlAsync(Uri, null);
            var matching = ciphers.Item1?.Select(c => new GroupingsPageListItem { Cipher = c }).ToList();
            if(matching?.Any() ?? false)
            {
                groupedItems.Add(
                    new GroupingsPageListGroup(matching, AppResources.MatchingItems, matching.Count, false));
            }
            var fuzzy = ciphers.Item2?.Select(c =>
                new GroupingsPageListItem { Cipher = c, FuzzyAutofill = true }).ToList();
            if(fuzzy?.Any() ?? false)
            {
                groupedItems.Add(
                    new GroupingsPageListGroup(fuzzy, AppResources.PossibleMatchingItems, fuzzy.Count, false));
            }
            GroupedItems.ResetWithRange(groupedItems);
            ShowList = groupedItems.Any();
        }

        public async Task SelectCipherAsync(CipherView cipher, bool fuzzy)
        {
            if(_deviceActionService.SystemMajorVersion() < 21)
            {
                // TODO
            }
            else
            {
                var autofillResponse = AppResources.Yes;
                if(fuzzy)
                {
                    var options = new List<string> { AppResources.Yes };
                    if(cipher.Type == CipherType.Login)
                    {
                        options.Add(AppResources.YesAndSave);
                    }
                    autofillResponse = await _deviceActionService.DisplayAlertAsync(null,
                        string.Format(AppResources.BitwardenAutofillServiceMatchConfirm, Name), AppResources.No,
                        options.ToArray());
                }
                if(autofillResponse == AppResources.YesAndSave && cipher.Type == CipherType.Login)
                {
                    var uris = cipher.Login?.Uris?.ToList();
                    if(uris == null)
                    {
                        uris = new List<LoginUriView>();
                    }
                    uris.Add(new LoginUriView
                    {
                        Uri = Uri,
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
                if(autofillResponse == AppResources.Yes || autofillResponse == AppResources.YesAndSave)
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
