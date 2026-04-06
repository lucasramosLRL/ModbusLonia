using Modbus.Core.Services.Scanning;
using Modbus.Desktop.Services;

namespace Modbus.Desktop.ViewModels;

public class ScanResultViewModel
{
    public DeviceScanResult Result { get; }

    public ScanResultViewModel(DeviceScanResult result)
    {
        Result = result;
    }

    public string DisplayName => Result.SuggestedName;
    public string ModelText   => Result.ModelName ?? LocalizationService.Instance["UnknownModel"];
    public string SlaveIdText => string.Format(LocalizationService.Instance["AddrPrefix"], Result.SlaveId);
    public string FirmwareText => Result.FirmwareVersionText;
    public string SerialText   => Result.SerialNumberText;
    public string AddressText  => Result.Tcp?.IpAddress ?? Result.Rtu?.PortName ?? "—";
}
