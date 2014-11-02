﻿using Microsoft.Practices.ServiceLocation;
using MoneyManager.Business.Logic;
using MoneyManager.Business.ViewModels;
using MoneyManager.DataAccess.DataAccess;
using MoneyManager.DataAccess.Model;
using MoneyManager.Views;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;

namespace MoneyManager.UserControls
{
    public partial class TransactionListUserControl
    {
        public TransactionListUserControl()
        {
            InitializeComponent();

            ServiceLocator.Current.GetInstance<BalanceViewModel>().IsTransactionView = true;
        }

        #region Properties
        
        public TransactionDataAccess TransactionData
        {
            get { return ServiceLocator.Current.GetInstance<TransactionDataAccess>(); }
        }

        public AddTransactionViewModel AddTransactionView
        {
            get { return ServiceLocator.Current.GetInstance<AddTransactionViewModel>(); }
        }

        public BalanceViewModel BalanceView
        {
            get { return ServiceLocator.Current.GetInstance<BalanceViewModel>(); }
        }        

        #endregion

        private void EditTransaction(object sender, RoutedEventArgs e)
        {
            var element = (FrameworkElement)sender;
            var transaction = element.DataContext as FinancialTransaction;
            if (transaction == null) return;

            TransactionLogic.PrepareEdit(transaction);
            ((Frame)Window.Current.Content).Navigate(typeof(AddTransaction));
        }

        private void DeleteTransaction(object sender, RoutedEventArgs e)
        {
            var element = (FrameworkElement)sender;
            var transaction = element.DataContext as FinancialTransaction;
            if (transaction == null) return;

            TransactionLogic.DeleteTransaction(transaction);
        }

        private void OpenContextMenu(object sender, HoldingRoutedEventArgs e)
        {
            var senderElement = sender as FrameworkElement;
            var flyoutBase = FlyoutBase.GetAttachedFlyout(senderElement);

            flyoutBase.ShowAt(senderElement);
        }

        private void UnloadPage(object sender, RoutedEventArgs e)
        {
            BalanceView.IsTransactionView = false;
            BalanceView.UpdateBalance();
        }
    }
}