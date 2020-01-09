﻿using GalaSoft.MvvmLight;
using Microsoft.AppCenter.Crashes;
using Microsoft.Graph;
using MoneyFox.Application.Common;
using MoneyFox.Application.Common.Adapters;
using MoneyFox.Application.Common.CloudBackup;
using MoneyFox.Application.Common.Facades;
using MoneyFox.Application.Common.Interfaces;
using MoneyFox.Application.Resources;
using MoneyFox.Domain.Exceptions;
using MoneyFox.Presentation.Commands;
using NLog;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MoneyFox.Presentation.ViewModels
{
    public interface IBackupViewModel
    {
        /// <summary>
        /// Initialize View Model.
        /// </summary>
        AsyncCommand InitializeCommand { get; }

        /// <summary>
        /// Makes the first login and sets the setting for the future navigation to this page.
        /// </summary>
        AsyncCommand LoginCommand { get; }

        /// <summary>
        /// Logs the user out from the backup service.
        /// </summary>
        AsyncCommand LogoutCommand { get; }

        /// <summary>
        /// Will create a backup of the database and upload it to OneDrive
        /// </summary>
        AsyncCommand BackupCommand { get; }

        /// <summary>
        /// Will download the database backup from OneDrive and overwrite the     local database with the downloaded.     All
        /// data models are then reloaded.
        /// </summary>
        AsyncCommand RestoreCommand { get; }

        DateTime BackupLastModified { get; }

        bool IsLoadingBackupAvailability { get; }

        bool IsLoggedIn { get; }

        bool BackupAvailable { get; }
    }

    /// <summary>
    /// Representation of the backup view.
    /// </summary>
    public class BackupViewModel : ViewModelBase, IBackupViewModel
    {
        private Logger logger = LogManager.GetCurrentClassLogger();

        private readonly IBackupService backupService;
        private readonly IConnectivityAdapter connectivity;
        private readonly IDialogService dialogService;
        private readonly ISettingsFacade settingsFacade;
        private bool backupAvailable;

        private DateTime backupLastModified;
        private bool isLoadingBackupAvailability;

        public BackupViewModel(IBackupService backupService,
                               IDialogService dialogService,
                               IConnectivityAdapter connectivity,
                               ISettingsFacade settingsFacade)
        {
            this.backupService = backupService;
            this.dialogService = dialogService;
            this.connectivity = connectivity;
            this.settingsFacade = settingsFacade;
        }

        /// <inheritdoc/>
        public AsyncCommand InitializeCommand => new AsyncCommand(InitializeAsync);

        /// <inheritdoc/>
        public AsyncCommand LoginCommand => new AsyncCommand(LoginAsync);

        /// <inheritdoc/>
        public AsyncCommand LogoutCommand => new AsyncCommand(LogoutAsync);

        /// <inheritdoc/>
        public AsyncCommand BackupCommand => new AsyncCommand(CreateBackupAsync);

        /// <inheritdoc/>
        public AsyncCommand RestoreCommand => new AsyncCommand(RestoreBackupAsync);

        /// <summary>
        /// The Date when the backup was modified the last time.
        /// </summary>
        public DateTime BackupLastModified
        {
            get => backupLastModified;
            private set
            {
                if(backupLastModified == value)
                    return;
                backupLastModified = value;
                RaisePropertyChanged();
            }
        }

        /// <summary>
        /// Indicator that the app is checking if backups available.
        /// </summary>
        public bool IsLoadingBackupAvailability
        {
            get => isLoadingBackupAvailability;
            private set
            {
                if(isLoadingBackupAvailability == value)
                    return;
                isLoadingBackupAvailability = value;
                RaisePropertyChanged();
            }
        }

        /// <summary>
        /// Indicator that the user logged in to the backup service.
        /// </summary>
        public bool IsLoggedIn => settingsFacade.IsLoggedInToBackupService;

        /// <summary>
        /// Indicates if a backup is available for restore.
        /// </summary>
        public bool BackupAvailable
        {
            get => backupAvailable;
            private set
            {
                if(backupAvailable == value)
                    return;
                backupAvailable = value;
                RaisePropertyChanged();
            }
        }

        private async Task InitializeAsync()
        {
            await LoadedAsync();
        }

        private async Task LoadedAsync()
        {
            if(!IsLoggedIn)
                return;

            if(!connectivity.IsConnected)
                await dialogService.ShowMessage(Strings.NoNetworkTitle, Strings.NoNetworkMessage);

            IsLoadingBackupAvailability = true;
            try
            {
                BackupAvailable = await backupService.IsBackupExistingAsync();
                BackupLastModified = await backupService.GetBackupDateAsync();
            }
            catch(BackupAuthenticationFailedException ex)
            {
                logger.Error(ex, "Issue during Login process.");
                await backupService.LogoutAsync();
                await dialogService.ShowMessage(Strings.AuthenticationFailedTitle, Strings.ErrorMessageAuthenticationFailed);
            }
            catch(ServiceException ex)
            {
                if(ex.Error.Code == "4f37.717b")
                {
                    await backupService.LogoutAsync();
                    await dialogService.ShowMessage(Strings.AuthenticationFailedTitle, Strings.ErrorMessageAuthenticationFailed);
                }
            }

            IsLoadingBackupAvailability = false;
        }

        private async Task LoginAsync()
        {
            if(!connectivity.IsConnected)
                await dialogService.ShowMessage(Strings.NoNetworkTitle, Strings.NoNetworkMessage);

            try
            {
                await backupService.LoginAsync();
            }
            catch(BackupOperationCanceledException)
            {
                await dialogService.ShowMessage(Strings.CanceledTitle, Strings.LoginCanceledMessage);
            }
            catch(Exception ex)
            {
                logger.Error(ex, "Login Failed.");
                await dialogService.ShowMessage(Strings.LoginFailedTitle, string.Format(Strings.UnknownErrorMessage, ex.Message));
            }

            RaisePropertyChanged(nameof(IsLoggedIn));
            await LoadedAsync();
        }

        private async Task LogoutAsync()
        {
            try
            {
                await backupService.LogoutAsync();
            }
            catch(BackupOperationCanceledException)
            {
                await dialogService.ShowMessage(Strings.CanceledTitle, Strings.LogoutCanceledMessage);
            }
            catch(Exception ex)
            {
                logger.Error(ex, "Logout Failed.");
                await dialogService.ShowMessage(Strings.GeneralErrorTitle, ex.Message);
            }

            // ReSharper disable once ExplicitCallerInfoArgument
            RaisePropertyChanged(nameof(IsLoggedIn));
        }

        private async Task CreateBackupAsync()
        {
            if(!await ShowOverwriteBackupInfoAsync())
                return;

            await dialogService.ShowLoadingDialogAsync();

            try
            {
                await backupService.UploadBackupAsync(BackupMode.Manual);

                BackupLastModified = DateTime.Now;
            }
            catch(BackupOperationCanceledException)
            {
                await dialogService.ShowMessage(Strings.CanceledTitle, Strings.UploadBackupCanceledMessage);
            }
            catch(Exception ex)
            {
                logger.Error(ex, "Create Backup failed.");
                await dialogService.ShowMessage(Strings.BackupFailedTitle, ex.Message);
            }

            await dialogService.HideLoadingDialogAsync();
            await ShowCompletionNoteAsync();
        }

        private async Task RestoreBackupAsync()
        {
            if(!await ShowOverwriteDataInfoAsync())
                return;

            await dialogService.ShowLoadingDialogAsync();
            DateTime backupDate = await backupService.GetBackupDateAsync();
            if (settingsFacade.LastDatabaseUpdate > backupDate && !await ShowForceOverrideConfirmationAsync()) return;

            await dialogService.ShowLoadingDialogAsync();

            try
            {
                await backupService.RestoreBackupAsync();
                await ShowCompletionNoteAsync();
            }
            catch(BackupOperationCanceledException)
            {
                await dialogService.ShowMessage(Strings.CanceledTitle, Strings.RestoreBackupCanceledMessage);
            }
            catch(Exception ex)
            {
                logger.Error(ex, "Restore Backup failed.");
                await dialogService.ShowMessage(Strings.BackupFailedTitle, ex.Message);
            }
        }

        private async Task<bool> ShowOverwriteBackupInfoAsync()
        {
            return await dialogService.ShowConfirmMessageAsync(Strings.OverwriteTitle,
                                                               Strings.OverwriteBackupMessage,
                                                               Strings.YesLabel,
                                                               Strings.NoLabel);
        }

        private async Task<bool> ShowOverwriteDataInfoAsync()
        {
            return await dialogService.ShowConfirmMessageAsync(Strings.OverwriteTitle,
                                                               Strings.OverwriteDataMessage,
                                                               Strings.YesLabel,
                                                               Strings.NoLabel);
        }

        private async Task<bool> ShowForceOverrideConfirmationAsync()
        {
            return await dialogService.ShowConfirmMessageAsync(Strings.ForceOverrideBackupTitle,
                                                               Strings.ForceOverrideBackupMessage,
                                                               Strings.YesLabel,
                                                               Strings.NoLabel);
        }

        private async Task ShowCompletionNoteAsync()
        {
            await dialogService.ShowMessage(Strings.SuccessTitle, Strings.TaskSuccessfulMessage);
        }
    }
}
