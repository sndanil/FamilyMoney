using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace FamilyMoney.ViewModels;

public class AccountViewModel : ViewModelBase
{
    private decimal _amount = 0;
    private string _name = string.Empty;
    private IImage? _image = null;
    private ObservableCollection<AccountViewModel> _children = new();
    private readonly MainWindowViewModel _mainWindowViewModel;

    public ICommand SelectCommand { get; }

    public ICommand AddCommand { get; }

    public ICommand EditCommand { get; }

    public ICommand ChangeImageCommand { get; }

    public ReactiveCommand<AccountViewModel, AccountViewModel?> OkCommand { get; }

    public ReactiveCommand<Unit, AccountViewModel?> CancelCommand { get; }

    public static Interaction<AccountViewModel, AccountViewModel?> ShowDialog { get; } = new ();

    public decimal Amount
    {
        get => _amount;
        set => this.RaiseAndSetIfChanged(ref _amount, value);
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
            }
        });

        EditCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var account = new AccountViewModel(_mainWindowViewModel)
            {
                Name = this.Name,
                Image = this.Image,
            };
            var result = await ShowDialog.Handle(account);
            if (result != null)
            {
                Name = result.Name;
                Image = result.Image;
            }
        });

        OkCommand = ReactiveCommand.Create<AccountViewModel, AccountViewModel?>((model) =>
        {
            return (AccountViewModel?)model;
        });

        CancelCommand = ReactiveCommand.Create(() =>
        {
            return (AccountViewModel?)null;
        });

        ChangeImageCommand = ReactiveCommand.CreateFromTask(ChangeImage());
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
                FileTypeFilter = new[] { new("Изображения") { Patterns = new[] { "*.png", "*.jpg" }, MimeTypes = new[] { "*/*" } }, FilePickerFileTypes.All }
            });

            if (files.Any())
            {
                await using var stream = await files.Single().OpenReadAsync();
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
}

