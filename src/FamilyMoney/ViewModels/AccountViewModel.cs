using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using DynamicData;
using FamilyMoney.DataAccess;
using ReactiveUI;
using Splat;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace FamilyMoney.ViewModels;

public class AccountViewModel : ViewModelBase
{
    private decimal _amount = 0;
    private Guid? _id = null;
    private Guid? _parentId = null;
    private string _name = string.Empty;
    private IImage? _image = null;
    private ObservableCollection<AccountViewModel> _children = new();
    private readonly MainWindowViewModel _mainWindowViewModel;

    public ICommand SelectCommand { get; }

    public ICommand AddCommand { get; }

    public ICommand EditCommand { get; }

    public ICommand ChangeImageCommand { get; }

    public ReactiveCommand<Unit, AccountViewModel?> OkCommand { get; }

    public ReactiveCommand<Unit, AccountViewModel?> CancelCommand { get; }

    public static Interaction<AccountViewModel, AccountViewModel?> ShowDialog { get; } = new ();

    public decimal Amount
    {
        get => _amount;
        set => this.RaiseAndSetIfChanged(ref _amount, value);
    }

    public Guid? Id
    {
        get => _id;
        set => this.RaiseAndSetIfChanged(ref _id, value);
    }

    public Guid? ParentId
    {
        get => _parentId;
        set => this.RaiseAndSetIfChanged(ref _parentId, value);
    }

    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    public IImage? Image
    {
        get => _image;
        set => this.RaiseAndSetIfChanged(ref _image, value);
    }

    public bool IsSelected
    {
        get => _mainWindowViewModel?.SelectedAccount == this;
    }

    public ObservableCollection<AccountViewModel> Children { get => _children; }

    public AccountViewModel(MainWindowViewModel main)
    {
        _mainWindowViewModel = main;
        _mainWindowViewModel.PropertyChanged += MainViewModelPropertyChanged;

        SelectCommand = ReactiveCommand.CreateFromTask(() =>
        {
            _mainWindowViewModel.SelectedAccount = this;

            return Task.CompletedTask;
        });

        AddCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var account = new AccountViewModel(_mainWindowViewModel);
            var result = await ShowDialog.Handle(account);
            if (result != null)
            {
                this.Children.Add(result);
                result.Id = Guid.NewGuid();
                result.ParentId = this.Id;
                Save(null, result);
            }
        });

        EditCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var account = new AccountViewModel(_mainWindowViewModel)
            {
                Id = this.Id,
                ParentId = this.ParentId,
                Name = this.Name,
                Image = this.Image,
            };
            var result = await ShowDialog.Handle(account);
            if (result != null)
            {
                Save(this, result);
            }
        });

        var canExecute = this.WhenAnyValue(x => x.Name, (name) => !string.IsNullOrEmpty(name));
        OkCommand = ReactiveCommand.Create(() =>
        {
            return (AccountViewModel?)this;
        }, 
        canExecute);

        CancelCommand = ReactiveCommand.Create(() =>
        {
            return (AccountViewModel?)null;
        });

        ChangeImageCommand = ReactiveCommand.CreateFromTask(ChangeImage());
    }

    public void AddFromAccount(IRepository repository, IEnumerable<Models.Account> accounts)
    {
        Children.AddRange(accounts.Where(a => a.ParentId == Id).Select(a => new AccountViewModel(_mainWindowViewModel)
        {
            Id = a.Id,
            Name = a.Name,
            ParentId = a.ParentId,
        }));

        foreach (var account in Children)
        {
            var imageStream = repository.TryGetImage(account.Id!.Value);
            if (imageStream != null)
            {
                account.Image = Bitmap.DecodeToWidth(imageStream, 400);
            }

            account.AddFromAccount(repository, accounts);
        }
    }

    private System.Func<Avalonia.Visual, Task> ChangeImage()
    {
        return async (Avalonia.Visual visual) =>
        {
            var topLevel = TopLevel.GetTopLevel(visual);
            var files = await topLevel!.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Выбор изображения",
                AllowMultiple = false,
                FileTypeFilter = [ 
                    new ("Изображения") { Patterns = [ "*.png", "*.jpg" ], MimeTypes = [ "*/*" ] }, 
                    FilePickerFileTypes.All 
                    ]
            });

            if (files.Any())
            {
                var file = files.Single();
                await using var stream = await file.OpenReadAsync();
                Image = Bitmap.DecodeToWidth(stream, 400);
            }
        };
    }

    private void MainViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.SelectedAccount))
        {
            this.RaisePropertyChanged(nameof(IsSelected));
        }
    }

    private static void Save(AccountViewModel? one, AccountViewModel other)
    {
        ArgumentNullException.ThrowIfNull(other, nameof(other));
        ArgumentNullException.ThrowIfNull(other.Id, $"{nameof(other)}.{nameof(other.Id)}");

        var repository = Locator.Current.GetService<IRepository>();

        if (one?.Image != other.Image)
        {
            var stream = new MemoryStream();
            ((Bitmap)other.Image!).Save(stream);
            stream.Position = 0;
            repository!.UpdateImage(other.Id.Value, other.Name, stream);
        }

        if (one != null)
        {
            one.Name = other.Name;
            one.Image = other.Image;
        }

        repository!.UpdateAccount(new Models.Account 
        { 
            Id = other.Id.Value,
            ParentId = other.ParentId,
            Name = other.Name,
        });
    }
}

