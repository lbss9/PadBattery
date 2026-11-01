using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Windows.Gaming.Input;
using Windows.System.Power;

namespace PadBattery;

static class Controllers
{
    public static List<PadStatus> Read()
    {
        var list = new List<PadStatus>();
        list.AddRange(DualSense.ReadAll());
        var xbox = Xbox.ReadAll();
        list.AddRange(xbox.Count > 0 ? xbox : XInput.ReadAll());
        return list;
    }
}

static class DualSense
{
    const string SonyVendorId = "054c";
    const int UsbReportLength = 64;
    const int UsbBatteryOffset = 53;
    const int BluetoothBatteryOffset = 54;
    const byte BasicReport = 0x01;
    const byte BluetoothFullReport = 0x31;
    const byte CalibrationFeature = 0x05;
    const int CalibrationFeatureLength = 41;
    const int StateCharging = 1;
    const int StateFull = 2;

    static readonly (string ProductId, string Name)[] Models =
    {
        ("0ce6", "DualSense"),
        ("0df2", "DualSense Edge"),
    };

    public static IEnumerable<PadStatus> ReadAll()
    {
        foreach (var path in Hid.DevicePaths())
        {
            string lower = path.ToLowerInvariant();
            if (!lower.Contains(SonyVendorId)) continue;
            var model = Models.FirstOrDefault(m => lower.Contains(m.ProductId));
            if (model.Name == null) continue;

            var status = TryRead(path, model.Name);
            if (status != null) yield return status;
        }
    }

    static PadStatus? TryRead(string path, string name)
    {
        try
        {
            using var handle = Hid.Open(path, overlapped: true);
            if (handle.IsInvalid) return null;
            using var stream = new FileStream(handle, FileAccess.Read, 0, isAsync: true);
            var report = new byte[128];

            for (int attempt = 0; attempt < 6; attempt++)
            {
                using var timeout = new CancellationTokenSource(1000);
                int length = stream.ReadAsync(report, timeout.Token).AsTask().GetAwaiter().GetResult();

                if (report[0] == BluetoothFullReport && length > BluetoothBatteryOffset)
                    return Status($"{name} (Bluetooth)", report[BluetoothBatteryOffset]);
                if (report[0] == BasicReport && length == UsbReportLength)
                    return Status($"{name} (USB)", report[UsbBatteryOffset]);
                if (report[0] == BasicReport)
                    Hid.RequestFeature(path, CalibrationFeature, CalibrationFeatureLength);
            }
        }
        catch
        {
        }
        return null;
    }

    static PadStatus Status(string name, byte battery)
    {
        int level = battery & 0x0F;
        int state = battery >> 4;
        int percent = state == StateFull ? 100 : Math.Min(level * 10 + 5, 100);
        return new PadStatus(name, percent, state == StateCharging);
    }
}

static class Xbox
{
    public static List<PadStatus> ReadAll()
    {
        var list = new List<PadStatus>();
        foreach (var pad in Gamepad.Gamepads)
        {
            var report = pad.TryGetBatteryReport();
            if (report == null || report.Status == BatteryStatus.NotPresent) continue;
            if (report.FullChargeCapacityInMilliwattHours is not int full || full <= 0) continue;
            if (report.RemainingCapacityInMilliwattHours is not int remaining) continue;

            int percent = Math.Clamp(remaining * 100 / full, 0, 100);
            list.Add(new PadStatus("Controle Xbox", percent, report.Status == BatteryStatus.Charging));
        }
        return list;
    }
}

static class XInput
{
    const byte DeviceGamepad = 0;
    const byte TypeDisconnected = 0;
    const byte TypeWired = 1;

    [StructLayout(LayoutKind.Sequential)]
    struct BatteryInformation
    {
        public byte Type;
        public byte Level;
    }

    [DllImport("xinput1_4.dll")]
    static extern int XInputGetBatteryInformation(int user, byte deviceType, out BatteryInformation info);

    public static List<PadStatus> ReadAll()
    {
        var list = new List<PadStatus>();
        for (int user = 0; user < 4; user++)
        {
            try
            {
                if (XInputGetBatteryInformation(user, DeviceGamepad, out var info) != 0) continue;
                if (info.Type is TypeDisconnected or TypeWired) continue;
                int percent = info.Level switch { 0 => 5, 1 => 30, 2 => 65, _ => 100 };
                list.Add(new PadStatus("Controle Xbox", percent, false));
            }
            catch
            {
            }
        }
        return list;
    }
}

static class Hid
{
    const uint GenericReadWrite = 0xC0000000;
    const uint ShareReadWrite = 3;
    const uint OpenExisting = 3;
    const uint FlagOverlapped = 0x40000000;

    [DllImport("hid.dll")]
    static extern void HidD_GetHidGuid(out Guid guid);

    [DllImport("hid.dll")]
    static extern bool HidD_GetFeature(SafeFileHandle handle, byte[] buffer, int length);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    static extern int CM_Get_Device_Interface_List_Size(out int length, ref Guid guid, string? deviceId, int flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    static extern int CM_Get_Device_Interface_List(ref Guid guid, string? deviceId, char[] buffer, int length, int flags);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);

    public static string[] DevicePaths()
    {
        HidD_GetHidGuid(out var guid);
        if (CM_Get_Device_Interface_List_Size(out int length, ref guid, null, 0) != 0 || length <= 1) return Array.Empty<string>();
        var buffer = new char[length];
        if (CM_Get_Device_Interface_List(ref guid, null, buffer, length, 0) != 0) return Array.Empty<string>();
        return new string(buffer).Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }

    public static SafeFileHandle Open(string path, bool overlapped) =>
        CreateFile(path, GenericReadWrite, ShareReadWrite, IntPtr.Zero, OpenExisting, overlapped ? FlagOverlapped : 0, IntPtr.Zero);

    public static void RequestFeature(string path, byte reportId, int length)
    {
        using var handle = Open(path, overlapped: false);
        if (handle.IsInvalid) return;
        var buffer = new byte[length];
        buffer[0] = reportId;
        HidD_GetFeature(handle, buffer, length);
    }
}
