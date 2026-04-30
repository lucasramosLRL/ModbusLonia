using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;

namespace Modbus.Desktop.ViewModels;

public partial class DeviceConfigureViewModel : ObservableObject
{
    private readonly Action _onGoBack;

    public DeviceItemViewModel Device { get; }

    // ── Section navigation ───────────────────────────────────────────────────
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

    // ── Device info strip (always visible) ──────────────────────────────────
    public string SerialNumberText =>
        Device.Device.SerialNumber is uint s ? $"{s:D7}" : "—";

    public string FirmwareText => Device.Device.FirmwareVersion is byte fw
        ? $"v{fw / 10}.{fw % 10}"
        : "—";

    public string HardwareText => "—";

    // ── General section fields ───────────────────────────────────────────────
    [ObservableProperty] private string _description;

    public string DeviceCodeDisplay =>
        Device.Device.DeviceModel?.DeviceCode is byte code ? $"{code:X2}" : "—";

    [ObservableProperty] private int _editableSlaveId;

    // ── Seq. PF (swap logic: selecting a value exchanges it with the position
    //    that currently holds that value, so all 4 are always distinct) ───────
    public IReadOnlyList<string> AllPfBytes { get; } = ["F0", "F1", "F2", "EXP"];

    private readonly string[] _pfPos = ["F2", "F1", "F0", "EXP"];

    public string PfPos0 { get => _pfPos[0]; set => SetPfPos(0, value); }
    public string PfPos1 { get => _pfPos[1]; set => SetPfPos(1, value); }
    public string PfPos2 { get => _pfPos[2]; set => SetPfPos(2, value); }
    public string PfPos3 { get => _pfPos[3]; set => SetPfPos(3, value); }

    private void SetPfPos(int index, string newValue)
    {
        if (newValue == _pfPos[index]) return;

        // Find which other position holds newValue and swap it with the displaced one
        int conflictIndex = Array.IndexOf(_pfPos, newValue);
        if (conflictIndex >= 0 && conflictIndex != index)
        {
            _pfPos[conflictIndex] = _pfPos[index];
            OnPropertyChanged($"PfPos{conflictIndex}");
        }

        _pfPos[index] = newValue;
        OnPropertyChanged($"PfPos{index}");
    }

    // ── Constructor / Commands ───────────────────────────────────────────────
    public DeviceConfigureViewModel(DeviceItemViewModel device, Action onGoBack)
    {
        Device = device;
        _onGoBack = onGoBack;
        _editableSlaveId = device.SlaveId;
        _description = device.Name;
    }

    [RelayCommand]
    private void GoBack() => _onGoBack();
}
