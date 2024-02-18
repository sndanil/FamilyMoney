using Avalonia.Media;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace FamilyMoney.ViewModels
{
    public class AccountViewModel : ViewModelBase
    {
        private decimal _amount = 0;
        private string _name = string.Empty;
        private IImage? _image = null;
        private ObservableCollection<AccountViewModel> _children = new();
        private readonly MainWindowViewModel _mainWindowViewModel;

        public ICommand SelectCommand { get; }

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
        }

        private void MainViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainWindowViewModel.SelectedAccount))
            {
                this.RaisePropertyChanged(nameof(IsSelected));
            }
        }
    }
}
