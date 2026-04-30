using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

namespace Modbus.Desktop.ViewModels;

public partial class DeviceConfigureViewModel : ObservableObject
{
    private readonly Action _onGoBack;

    public DeviceItemViewModel Device { get; }

    [ObservableProperty] private int _selectedSectionIndex;

    public bool IsGeneral       => SelectedSectionIndex == 0;
    public bool IsEthernet      => SelectedSectionIndex == 1;
    public bool IsWireless      => SelectedSectionIndex == 2;
    public bool IsSntp          => SelectedSectionIndex == 3;
    public bool IsIot           => SelectedSectionIndex == 4;
    public bool IsClock         => SelectedSectionIndex == 5;
    public bool IsInputsOutputs => SelectedSectionIndex == 6;

    partial void OnSelectedSectionIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsGeneral));
        OnPropertyChanged(nameof(IsEthernet));
        OnPropertyChanged(nameof(IsWireless));
        OnPropertyChanged(nameof(IsSntp));
        OnPropertyChanged(nameof(IsIot));
        OnPropertyChanged(nameof(IsClock));
        OnPropertyChanged(nameof(IsInputsOutputs));
    }

    public DeviceConfigureViewModel(DeviceItemViewModel device, Action onGoBack)
    {
        Device = device;
        _onGoBack = onGoBack;
    }

    [RelayCommand]
    private void GoBack() => _onGoBack();
}
