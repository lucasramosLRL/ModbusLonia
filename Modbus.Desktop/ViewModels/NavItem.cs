using CommunityToolkit.Mvvm.ComponentModel;

namespace Modbus.Desktop.ViewModels;

public partial class NavItem : ObservableObject
{
    public required string Key  { get; init; }
    public required string Icon { get; init; }

    [ObservableProperty]
    private string _title = "";
}
