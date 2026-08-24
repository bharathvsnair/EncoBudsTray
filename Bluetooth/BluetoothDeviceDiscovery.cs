using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Devices.Enumeration;

namespace EncoBudsTray.Bluetooth;

/// <summary>
/// Finds the OPPO Enco Buds2 through the RFCOMM service documented by Gadgetbridge.
/// </summary>
public sealed class BluetoothDeviceDiscovery
{
    public const string DeviceName = "OPPO Enco Buds2";

    // Gadgetbridge OppoHeadphonesSupport.addSupportedService(...)
    public static readonly Guid ServiceUuid =
        Guid.Parse("0000079a-d102-11e1-9b23-00025b00a5a5");

    public async Task<RfcommDeviceService?> FindAsync(CancellationToken cancellationToken)
    {
        var selector = RfcommDeviceService.GetDeviceSelector(
            RfcommServiceId.FromUuid(ServiceUuid));

        var devices = await DeviceInformation.FindAllAsync(selector);

        foreach (var info in devices)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var service = await RfcommDeviceService.FromIdAsync(info.Id);
                if (service is null)
                    continue;

                // Verify the actual Bluetooth device name after resolving the
                // RFCOMM service; DeviceInformation.Name may describe the service.
                if (!string.Equals(service.Device.Name, DeviceName, StringComparison.OrdinalIgnoreCase))
                {
                    service.Dispose();
                    continue;
                }

                return service;
            }
            catch
            {
                // A disappearing service is normal while Bluetooth state changes.
            }
        }

        return null;
    }
}
