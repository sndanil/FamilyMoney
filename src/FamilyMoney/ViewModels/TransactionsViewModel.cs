using FamilyMoney.DataAccess;
using ReactiveUI;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Windows.Input;

namespace FamilyMoney.ViewModels;

public class TransactionsViewModel : ViewModelBase
{
    private readonly IRepository _repository;

    public ICommand AddDebetCommand { get; }

    public ICommand AddCreditCommand { get; }

    public ICommand AddTransferCommand { get; }

    public ICommand EditCommand { get; }

    public ICommand DeleteCommand { get; }

    public TransactionsViewModel(IRepository repository)
    {
        _repository = repository;

        AddDebetCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var transaction = new TransactionViewModel { };
            var result = await TransactionViewModel.ShowDialog.Handle(transaction);
            if (result != null)
            {
            }
        });

        AddCreditCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var transaction = new TransactionViewModel();
            var result = await TransactionViewModel.ShowDialog.Handle(transaction);
            if (result != null)
            {
            }
        });

        AddTransferCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var transaction = new TransactionViewModel();
            var result = await TransactionViewModel.ShowDialog.Handle(transaction);
            if (result != null)
            {
            }
        });

        EditCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var transaction = new TransactionViewModel()
            {
            };
            var result = await TransactionViewModel.ShowDialog.Handle(transaction);
            if (result != null)
            {
            }
        });

        DeleteCommand = ReactiveCommand.CreateFromTask(async () =>
        {
        });
    }
}
