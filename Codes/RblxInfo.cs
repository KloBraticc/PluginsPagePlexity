using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.IO;
using System.Windows.Controls;
using System.Windows.Threading;

public class TestPluginLogic
{
    private readonly Window window;
    private readonly TextBlock statusLabel;
    private readonly TextBlock serverIpLabel;
    private readonly TextBlock serverLocationLabel;
    private readonly TextBlock pingLabel;
    private readonly Timer robloxCheckTimer;
    private readonly HttpClient httpClient = new();

    private string? lastKnownIp = null;
    private readonly string configFilePath;
    private bool isChecking = false;

    // Title animation fields
    private readonly DispatcherTimer titleTimer;
    private readonly string baseTitle = "Server Info";
    private readonly string creditText = " - V1 Credit: Bratic";
    private int creditCharIndex = 0;
    private bool isAppending = true;

    public TestPluginLogic(Window window)
    {
        this.window = window;

        var grid = (Grid)window.Content;

        statusLabel = (TextBlock)grid.FindName("StatusLabel");
        serverIpLabel = (TextBlock)grid.FindName("ServerIpLabel");
        serverLocationLabel = (TextBlock)grid.FindName("ServerLocationLabel");
        pingLabel = (TextBlock)grid.FindName("PingLabel");

        var stayOnTopCheckbox = (CheckBox)grid.FindName("StayOnTopCheckbox");

        configFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "stayontop.txt");

        if (File.Exists(configFilePath))
        {
            try
            {
                bool.TryParse(File.ReadAllText(configFilePath).Trim(), out bool savedStayOnTop);
                stayOnTopCheckbox.IsChecked = savedStayOnTop;
                window.Topmost = savedStayOnTop;
            }
            catch { }
        }

        if (stayOnTopCheckbox != null)
        {
            stayOnTopCheckbox.Checked += (s, e) => SaveStayOnTop(window, true);
            stayOnTopCheckbox.Unchecked += (s, e) => SaveStayOnTop(window, false);
        }

        robloxCheckTimer = new Timer(3000);
        robloxCheckTimer.Elapsed += async (s, e) => await DispatcherSafeCheck();
        robloxCheckTimer.Start();

        // Setup title animation timer
        window.Title = baseTitle;

        titleTimer = new DispatcherTimer();
        titleTimer.Interval = TimeSpan.FromSeconds(1);
        titleTimer.Tick += TitleTimer_Tick;
        titleTimer.Start();
    }

    private void TitleTimer_Tick(object? sender, EventArgs e)
    {
        if (isAppending)
        {
            if (creditCharIndex < creditText.Length)
            {
                window.Title += creditText[creditCharIndex];
                creditCharIndex++;
            }
            else
            {
                isAppending = false;
            }
        }
        else
        {
            if (creditCharIndex > 0)
            {
                creditCharIndex--;
                window.Title = baseTitle + creditText.Substring(0, creditCharIndex);
            }
            else
            {
                isAppending = true;
            }
        }
    }

    private void SaveStayOnTop(Window window, bool stayOnTop)
    {
        window.Topmost = stayOnTop;
        try { File.WriteAllText(configFilePath, stayOnTop.ToString()); } catch { }
    }

    private async Task DispatcherSafeCheck()
    {
        if (isChecking) return;
        isChecking = true;

        await Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            UpdateUI("Refreshing...", "Refreshing...", "Refreshing...", -1);
            await CheckRobloxProcessAsync();
            isChecking = false;
        });
    }

    private async Task CheckRobloxProcessAsync()
    {
        var robloxProcess = Process.GetProcessesByName("RobloxPlayerBeta").FirstOrDefault();

        IPAddress? remoteIpAddress = null;

        if (robloxProcess != null)
        {
            var remoteIps = TcpHelper.GetRemoteIpsForProcess(robloxProcess.Id);
            remoteIpAddress = remoteIps?.FirstOrDefault(ip => !TcpHelper.IsPrivateIp(ip));
        }

        if (remoteIpAddress == null && lastKnownIp != null)
        {
            remoteIpAddress = IPAddress.TryParse(lastKnownIp, out var fallbackIp) ? fallbackIp : null;
        }

        if (remoteIpAddress == null)
        {
            UpdateUI("Roblox not running", "unknown", "unknown", -1);
            return;
        }

        string remoteIp = remoteIpAddress.ToString();
        lastKnownIp = remoteIp;

        string location = await GetGeoLocationAsync(remoteIp);
        int ping = await GetPingAsync(remoteIp);

        UpdateUI("Roblox is running", remoteIp, location, ping);
    }

    private void UpdateUI(string status, string ip, string location, int ping)
    {
        statusLabel.Text = status;
        serverIpLabel.Text = $"Server IP: {ip}";
        serverLocationLabel.Text = $"Server location: {location}";
        pingLabel.Text = $"Ping: {(ping >= 0 ? $"{ping}ms" : "unreachable")}";
    }

    private async Task<string> GetGeoLocationAsync(string ip)
    {
        try
        {
            var response = await httpClient.GetAsync($"http://ip-api.com/json/{ip}?fields=status,country,regionName,city,message");
            if (!response.IsSuccessStatusCode)
                return "Unknown location";

            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = json.RootElement;

            if (root.GetProperty("status").GetString() != "success")
                return "Unknown location";

            string? city = root.TryGetProperty("city", out var cityProp) ? cityProp.GetString() : null;
            string? region = root.TryGetProperty("regionName", out var regionProp) ? regionProp.GetString() : null;
            string? country = root.TryGetProperty("country", out var countryProp) ? countryProp.GetString() : null;

            string location = string.Join(", ", new[] { city, region, country }.Where(s => !string.IsNullOrWhiteSpace(s)));
            return string.IsNullOrWhiteSpace(location) ? "Unknown location" : location;
        }
        catch
        {
            return "Unknown location";
        }
    }

    private async Task<int> GetPingAsync(string ip)
    {
        try
        {
            using var client = new TcpClient();
            var sw = Stopwatch.StartNew();

            var connectTask = client.ConnectAsync(ip, 443);
            var timeout = Task.Delay(3000);
            var result = await Task.WhenAny(connectTask, timeout);

            sw.Stop();

            if (result == timeout || !client.Connected)
                return -1;

            return (int)sw.ElapsedMilliseconds;
        }
        catch
        {
            return -1;
        }
    }
}

public static class TcpHelper
{
    private enum TcpTableClass { TCP_TABLE_OWNER_PID_ALL = 5 }

    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_TCPROW_OWNER_PID
    {
        public uint state, localAddr, localPort, remoteAddr, remotePort, owningPid;
        public IPAddress RemoteAddress => new IPAddress(remoteAddr);
        public TcpState State => (TcpState)state;
    }

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(
        IntPtr pTcpTable, ref int dwOutBufLen, bool sort, int ipVersion,
        TcpTableClass tblClass, int reserved);

    public static IPAddress[]? GetRemoteIpsForProcess(int pid)
    {
        int bufferSize = 0;
        GetExtendedTcpTable(IntPtr.Zero, ref bufferSize, true, 2, TcpTableClass.TCP_TABLE_OWNER_PID_ALL, 0);
        IntPtr tablePtr = Marshal.AllocHGlobal(bufferSize);

        try
        {
            if (GetExtendedTcpTable(tablePtr, ref bufferSize, true, 2, TcpTableClass.TCP_TABLE_OWNER_PID_ALL, 0) != 0)
                return null;

            int rowCount = Marshal.ReadInt32(tablePtr);
            IntPtr rowPtr = IntPtr.Add(tablePtr, 4);
            int rowSize = Marshal.SizeOf<MIB_TCPROW_OWNER_PID>();
            var ipList = new System.Collections.Generic.List<IPAddress>();

            for (int i = 0; i < rowCount; i++)
            {
                var row = Marshal.PtrToStructure<MIB_TCPROW_OWNER_PID>(rowPtr);
                if (row.State == TcpState.Established && row.owningPid == (uint)pid)
                    ipList.Add(row.RemoteAddress);

                rowPtr = IntPtr.Add(rowPtr, rowSize);
            }

            return ipList.ToArray();
        }
        finally
        {
            Marshal.FreeHGlobal(tablePtr);
        }
    }

    public static bool IsPrivateIp(IPAddress ip)
    {
        if (ip.AddressFamily != AddressFamily.InterNetwork) return false;
        byte[] bytes = ip.GetAddressBytes();
        return (bytes[0] == 10) || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) || (bytes[0] == 192 && bytes[1] == 168);
    }
}

public enum TcpState
{
    Closed = 1, Listen = 2, SynSent = 3, SynReceived = 4, Established = 5,
    FinWait1 = 6, FinWait2 = 7, CloseWait = 8, Closing = 9,
    LastAck = 10, TimeWait = 11, DeleteTcb = 12
}
