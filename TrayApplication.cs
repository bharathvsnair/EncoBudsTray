using System.Diagnostics;
using System.Reflection;
using EncoBudsTray.Bluetooth;
using EncoBudsTray.Models;
using EncoBudsTray.Protocol;

namespace EncoBudsTray;

public sealed class TrayApplication : ApplicationContext
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan BatteryResponseTimeout = TimeSpan.FromSeconds(5);

    private readonly NotifyIcon _trayIcon;
    private readonly ToolStripMenuItem _refreshItem;
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _refreshSignal = new(0, 1);
    private readonly SynchronizationContext _uiContext;

    private readonly BluetoothDeviceDiscovery _discovery = new();
    private readonly OppoBatteryProtocol _protocol = new();

    private OppoEncoBudsConnection? _connection;
    private BatteryState _battery = BatteryState.Unknown;
    private bool _deviceConnected;
    private bool _stopping;

    public TrayApplication()
    {
        _uiContext = SynchronizationContext.Current
            ?? new WindowsFormsSynchronizationContext();

        _refreshItem = new ToolStripMenuItem("Refresh");
        _refreshItem.Click += (_, _) => SignalRefresh();

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExitApplication();

        var menu = new ContextMenuStrip();
        menu.Items.Add(_refreshItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        _trayIcon = new NotifyIcon
        {
            Icon = LoadIcon(),
            Visible = true,
            Text = "OPPO Enco Buds2 — Searching...",
            ContextMenuStrip = menu
        };

        _trayIcon.MouseDoubleClick += (_, _) => SignalRefresh();

        _ = MonitorAsync(_cts.Token);
    }

    private async Task MonitorAsync(CancellationToken cancellationToken)
    {
        var reconnectDelay = TimeSpan.FromSeconds(5);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (_connection is null)
                {
                    SetStatus("Searching...");

                    try
                    {
                        var service = await _discovery.FindAsync(cancellationToken);
                        if (service is null)
                        {
                            await WaitOrRefreshAsync(reconnectDelay, cancellationToken);
                            reconnectDelay = TimeSpan.FromSeconds(
                                Math.Min(reconnectDelay.TotalSeconds * 2, 30));
                            continue;
                        }

                        SetStatus("Connecting...");
                        var connection = new OppoEncoBudsConnection(service, _protocol);
                        await connection.ConnectAsync(cancellationToken);

                        _connection = connection;
                        _deviceConnected = true;
                        reconnectDelay = TimeSpan.FromSeconds(5);

                        SetStatus("Connected");
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Log($"Connection failed: {ex.GetType().Name}: {ex.Message}");
                        _deviceConnected = false;
                        await DisposeConnectionAsync();
                        SetStatus("Disconnected");
                        await WaitOrRefreshAsync(reconnectDelay, cancellationToken);
                        reconnectDelay = TimeSpan.FromSeconds(
                            Math.Min(reconnectDelay.TotalSeconds * 2, 30));
                        continue;
                    }
                }

                try
                {
                    SetStatus("Updating battery...");
                    var state = await ReadBatteryWithRetriesAsync(cancellationToken);

                    if (state is null)
                    {
                        Log("Battery request failed after 3 attempts.");
                        SetStatus("Disconnected");
                        await DisposeConnectionAsync();
                        continue;
                    }

                    _battery = _battery.Merge(state);
                    _deviceConnected = true;
                    Log($"Battery: L={FormatLogValue(_battery.Left)} " +
                        $"R={FormatLogValue(_battery.Right)} " +
                        $"Case={FormatLogValue(_battery.Case)}");
                    UpdateTooltip();

                    await WaitOrRefreshAsync(PollInterval, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Log($"Communication failed: {ex.GetType().Name}: {ex.Message}");
                    _deviceConnected = false;
                    await DisposeConnectionAsync();
                    SetStatus("Disconnected");
                    await WaitOrRefreshAsync(reconnectDelay, cancellationToken);
                }
            }
        }
        finally
        {
            await DisposeConnectionAsync();
        }
    }

    private async Task<BatteryState?> ReadBatteryWithRetriesAsync(CancellationToken cancellationToken)
    {
        // Gadgetbridge's OppoHeadphonesSupport retries a battery request twice,
        // two seconds apart, because some devices do not answer the first request.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var state = await _connection!.ReadBatteryAsync(
                BatteryResponseTimeout, cancellationToken);

            if (state is not null)
                return state;

            if (attempt < 2)
            {
                Log($"Battery request retry {attempt + 1}.");
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }

        return null;
    }

    private async Task WaitOrRefreshAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        var delayTask = Task.Delay(delay, cancellationToken);
        var refreshTask = _refreshSignal.WaitAsync(cancellationToken);
        await Task.WhenAny(delayTask, refreshTask);

        if (!delayTask.IsCompleted)
        {
            // Consume the signal if it won the race.
            await refreshTask;
        }
    }

    private void SignalRefresh()
    {
        try
        {
            if (_refreshSignal.CurrentCount == 0)
                _refreshSignal.Release();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void SetStatus(string status)
    {
        PostToUi(() =>
        {
            _trayIcon.Text = status switch
            {
                "Searching..." => "OPPO Enco Buds2 — Searching...",
                "Connecting..." => "OPPO Enco Buds2 — Connecting...",
                "Connected" => BuildTooltip(),
                "Updating battery..." => BuildTooltip(),
                "Disconnected" => "OPPO Enco Buds2 — Disconnected",
                _ => $"OPPO Enco Buds2 — {status}"
            };
        });
    }

    private void UpdateTooltip()
        => PostToUi(() => _trayIcon.Text = BuildTooltip());

    private string BuildTooltip()
    {
        if (!_deviceConnected)
            return "OPPO Enco Buds2 — Disconnected";

        return $"OPPO Enco Buds2\n" +
    $"Left: {_battery.Left}%\n" +
    $"Right: {_battery.Right}%\n" +
    $"Case: {_battery.Case}%";
    }

    private static string FormatValue(int? value) => value.HasValue ? $"{value.Value}%" : "—";
    private static string FormatLogValue(int? value) => value?.ToString() ?? "unknown";

    private void PostToUi(Action action)
    {
        if (_stopping)
            return;

        _uiContext.Post(_ =>
        {
            if (!_stopping)
                action();
        }, null);
    }

    private static Icon LoadIcon()
    {
        var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("EncoBudsTray.Resources.enco.ico");

        if (stream is null)
            return SystemIcons.Application;

        using (stream)
            return new Icon(stream);
    }

    private async Task DisposeConnectionAsync()
    {
        var connection = Interlocked.Exchange(ref _connection, null);
        if (connection is not null)
        {
            try
            {
                await connection.DisposeAsync();
            }
            catch (Exception ex)
            {
                Log($"Connection cleanup failed: {ex.Message}");
            }
        }
    }

    private void ExitApplication()
    {
        if (_stopping)
            return;

        _stopping = true;
        _cts.Cancel();

        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _refreshSignal.Dispose();
        _cts.Dispose();

        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_stopping)
            ExitApplication();

        base.Dispose(disposing);
    }

    private static void Log(string message)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "EncoBudsTray");

            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "enco.log");

            File.AppendAllText(
                path,
                $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");

            var info = new FileInfo(path);
            if (info.Length > 1_000_000)
            {
                var old = path + ".old";
                File.Copy(path, old, true);
                File.WriteAllText(path, string.Empty);
            }
        }
        catch
        {
            // Logging must never take down the tray utility.
        }
    }
}
