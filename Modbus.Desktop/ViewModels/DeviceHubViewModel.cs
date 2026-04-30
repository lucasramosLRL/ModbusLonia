using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Modbus.Core.Domain.Repositories;
using Modbus.Core.Polling;
using System;

namespace Modbus.Desktop.ViewModels;

public partial class DeviceHubViewModel : ObservableObject
{
    private readonly IRegisterValueRepository _registerValueRepository;
    private readonly IPollingEngine _pollingEngine;
    private readonly DeviceListViewModel _parent;

    public event EventHandler<object>? NavigationRequested;

    public DeviceItemViewModel Device { get; }

    public DeviceHubViewModel(
        DeviceItemViewModel device,
        IRegisterValueRepository registerValueRepository,
        IPollingEngine pollingEngine,
        DeviceListViewModel parent)
    {
        Device = device;
        _registerValueRepository = registerValueRepository;
        _pollingEngine = pollingEngine;
        _parent = parent;
    }

    [RelayCommand]
    private void GoBack() => _parent.NavigateBack();

    [RelayCommand]
    private void OpenReadings()
    {
        var detail = new DeviceDetailViewModel(
            Device,
            _registerValueRepository,
            _pollingEngine,
            onGoBack: () => NavigationRequested?.Invoke(this, this));

        _ = detail.LoadValuesAsync();
        NavigationRequested?.Invoke(this, detail);
    }

    [RelayCommand]
    private void OpenConfigure()
    {
        var configure = new DeviceConfigureViewModel(
            Device,
            onGoBack: () => NavigationRequested?.Invoke(this, this));

        NavigationRequested?.Invoke(this, configure);
    }
}
