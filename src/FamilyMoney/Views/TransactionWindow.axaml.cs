using Avalonia.ReactiveUI;
using FamilyMoney.ViewModels;
using ReactiveUI;
using System;


namespace FamilyMoney.Views
{
    public partial class TransactionWindow : ReactiveWindow<TransactionViewModel>
    {
        public TransactionWindow()
        {
            InitializeComponent();

            this.WhenActivated(action => action(ViewModel!.OkCommand.Subscribe(Close)));
            this.WhenActivated(action => action(ViewModel!.CancelCommand.Subscribe(Close)));
        }
    }
}
