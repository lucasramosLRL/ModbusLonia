using CommunityToolkit.Mvvm.ComponentModel;
using Modbus.Desktop.Services;
using System;

namespace Modbus.Desktop.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly LocalizationService _loc;

    public AppLanguage[] AvailableLanguages { get; } = Enum.GetValues<AppLanguage>();

    public AppLanguage SelectedLanguage
    {
        get => _loc.CurrentLanguage;
        set
        {
            if (_loc.CurrentLanguage == value) return;
            _loc.CurrentLanguage = value;
            OnPropertyChanged();
        }
    }

    public SettingsViewModel(LocalizationService loc)
    {
        _loc = loc;
    }
}
