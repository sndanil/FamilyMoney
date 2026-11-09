using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FamilyMoney.Messages;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FamilyMoney.ViewModels;

public sealed partial class MergeSubCategoriesViewModel : ViewModelBase
{
    public MergeSubCategoriesViewModel(IReadOnlyList<(Guid Id, string Name, int TransactionCount)> candidates)
    {
        Candidates = candidates
            .Select(candidate => new MergeSubCategoryCandidateViewModel(this, candidate.Id, candidate.Name, candidate.TransactionCount))
            .ToList();

        if (Candidates.Count > 0)
        {
            Select(Candidates[0]);
        }
    }

    public IReadOnlyList<MergeSubCategoryCandidateViewModel> Candidates { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OkCommand))]
    public partial MergeSubCategoryCandidateViewModel? SelectedCandidate { get; set; }

    public void Select(MergeSubCategoryCandidateViewModel candidate)
    {
        SelectedCandidate = candidate;
        foreach (var item in Candidates)
        {
            item.SetChosen(ReferenceEquals(item, candidate));
        }
    }

    private bool CanOk() => SelectedCandidate != null;

    [RelayCommand(CanExecute = nameof(CanOk))]
    private void Ok()
    {
        WeakReferenceMessenger.Default.Send(new ModelCloseMessage<MergeSubCategoriesViewModel>(this));
    }

    [RelayCommand]
    private void Cancel()
    {
        WeakReferenceMessenger.Default.Send(new ModelCloseMessage<MergeSubCategoriesViewModel>(null));
    }
}

public partial class MergeSubCategoryCandidateViewModel : ViewModelBase
{
    private readonly MergeSubCategoriesViewModel _owner;

    public MergeSubCategoryCandidateViewModel(MergeSubCategoriesViewModel owner, Guid id, string name, int transactionCount)
    {
        _owner = owner;
        Id = id;
        Name = name;
        TransactionCount = transactionCount;
    }

    public Guid Id { get; }

    public string Name { get; }

    public int TransactionCount { get; }

    public string TransactionCountText => FormatTransactionCount(TransactionCount);

    [ObservableProperty]
    public partial bool IsChosen { get; set; }

    public void SetChosen(bool value)
    {
        if (IsChosen == value)
        {
            return;
        }

        IsChosen = value;
    }

    partial void OnIsChosenChanged(bool value)
    {
        if (value && !ReferenceEquals(_owner.SelectedCandidate, this))
        {
            _owner.Select(this);
        }
    }

    private static string FormatTransactionCount(int count)
    {
        var absolute = Math.Abs(count);
        var mod100 = absolute % 100;
        var mod10 = absolute % 10;
        var word = mod100 is >= 11 and <= 14
            ? "транзакций"
            : mod10 switch
            {
                1 => "транзакция",
                >= 2 and <= 4 => "транзакции",
                _ => "транзакций",
            };

        return $"{count} {word}";
    }
}
