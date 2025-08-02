using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Threading;

public class AutoClickerLogic
{
    private readonly TextBlock _statusTextBlock;
    private readonly TextBlock _intervalTextBlock;
    private readonly Button _toggleButton;
    private readonly Slider _clickSpeedSlider;
    private readonly TextBox _hotkeyTextBox;

    private System.Threading.Timer _clickThreadTimer;

    private bool _isClickerEnabled = false;
    private Key _hotkey = Key.F6;
    private ModifierKeys _hotkeyModifiers = ModifierKeys.None;

    private readonly string _settingsPath;
    private readonly Window _window;
    private HwndSource _hwndSource;

    private const int HOTKEY_ID = 9000;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public int type;
        public MOUSEINPUT mi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private const int INPUT_MOUSE = 0;
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;

    private const uint MOD_NONE = 0x0000;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;

    public AutoClickerLogic(Window window)
    {
        _window = window;

        _statusTextBlock = (TextBlock)((Grid)window.Content).FindName("MyTextBlock");
        _intervalTextBlock = (TextBlock)((Grid)window.Content).FindName("TopPlaytimeTextBlock");
        _toggleButton = (Button)((Grid)window.Content).FindName("ToggleClickerButton");
        _clickSpeedSlider = (Slider)((Grid)window.Content).FindName("ClickSpeedSlider");
        _hotkeyTextBox = (TextBox)((Grid)window.Content).FindName("HotkeyTextBox");

        _toggleButton.Click += ToggleButton_Click;
        _clickSpeedSlider.ValueChanged += ClickSpeedSlider_ValueChanged;

        _hotkeyTextBox.PreviewKeyDown += HotkeyTextBox_PreviewKeyDown;
        _hotkeyTextBox.GotFocus += HotkeyTextBox_GotFocus;
        _hotkeyTextBox.LostFocus += HotkeyTextBox_LostFocus;

        _settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AutoClickerSettings.txt");

        LoadSettings();
        UpdateClickInterval();

        window.Loaded += Window_Loaded;
        window.Closed += Window_Closed;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        IntPtr handle = GetHandle();
        _hwndSource = HwndSource.FromHwnd(handle);
        _hwndSource.AddHook(HwndHook);
        RegisterGlobalHotkey();
    }

    private void Window_Closed(object sender, EventArgs e)
    {
        UnregisterHotKey(GetHandle(), HOTKEY_ID);
        if (_hwndSource != null)
        {
            _hwndSource.RemoveHook(HwndHook);
            _hwndSource = null;
        }
        _clickThreadTimer?.Dispose();
    }

    private IntPtr GetHandle()
    {
        return new WindowInteropHelper(_window).Handle;
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_HOTKEY = 0x0312;
        if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            ToggleClicker();
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void RegisterGlobalHotkey()
    {
        IntPtr handle = GetHandle();
        UnregisterHotKey(handle, HOTKEY_ID); // Unregister previous if any

        bool registered = RegisterHotKey(
            handle,
            HOTKEY_ID,
            GetModFromModifierKeys(_hotkeyModifiers),
            (uint)KeyInterop.VirtualKeyFromKey(_hotkey));

        if (!registered)
        {
            _window.Dispatcher.Invoke(() =>
            {
                _statusTextBlock.Text = "Failed to register hotkey.";
                _statusTextBlock.Foreground = Brushes.Red;
            });
        }
    }

    private uint GetModFromModifierKeys(ModifierKeys modifiers)
    {
        uint mod = 0;
        if (modifiers.HasFlag(ModifierKeys.Alt)) mod |= MOD_ALT;
        if (modifiers.HasFlag(ModifierKeys.Control)) mod |= MOD_CONTROL;
        if (modifiers.HasFlag(ModifierKeys.Shift)) mod |= MOD_SHIFT;
        if (modifiers.HasFlag(ModifierKeys.Windows)) mod |= MOD_WIN;
        if (mod == 0) mod = MOD_NONE;
        return mod;
    }

    private void ToggleButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleClicker();
    }

    private void ClickSpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateClickInterval();
        SaveSettings();
    }

    private void HotkeyTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        _hotkeyTextBox.Text = "Press a key...";
    }

    private void HotkeyTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        _hotkeyTextBox.Text = GetHotkeyString();
    }

    private void HotkeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;

        _hotkeyModifiers = Keyboard.Modifiers;

        Key key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Ignore only modifier keys by themselves
        if (key == Key.LeftCtrl || key == Key.RightCtrl ||
            key == Key.LeftAlt || key == Key.RightAlt ||
            key == Key.LeftShift || key == Key.RightShift ||
            key == Key.LWin || key == Key.RWin)
        {
            return;
        }

        _hotkey = key;

        _hotkeyTextBox.Text = GetHotkeyString();

        RegisterGlobalHotkey();
        SaveSettings();

        Keyboard.ClearFocus();
    }

    private string GetHotkeyString()
    {
        string modStr = "";
        if (_hotkeyModifiers.HasFlag(ModifierKeys.Control)) modStr += "Ctrl+";
        if (_hotkeyModifiers.HasFlag(ModifierKeys.Alt)) modStr += "Alt+";
        if (_hotkeyModifiers.HasFlag(ModifierKeys.Shift)) modStr += "Shift+";
        if (_hotkeyModifiers.HasFlag(ModifierKeys.Windows)) modStr += "Win+";

        return modStr + _hotkey.ToString();
    }

    private void UpdateClickInterval()
    {
        int interval = Math.Max(1, (int)_clickSpeedSlider.Value);

        _clickThreadTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        _clickThreadTimer?.Dispose();
        _clickThreadTimer = new Timer(ClickTimerCallback, null, Timeout.Infinite, Timeout.Infinite);

        if (_isClickerEnabled)
        {
            _clickThreadTimer.Change(0, interval);
        }

        _window.Dispatcher.Invoke(() =>
        {
            _intervalTextBlock.Text = $"Auto Click Speed: {interval} ms";
        });
    }

    private void ToggleClicker()
    {
        _isClickerEnabled = !_isClickerEnabled;

        if (_isClickerEnabled)
        {
            int interval = Math.Max(1, (int)_clickSpeedSlider.Value);
            _clickThreadTimer.Change(0, interval);

            _window.Dispatcher.Invoke(() =>
            {
                _toggleButton.Content = "Stop Clicker";
                _statusTextBlock.Text = "Clicking Roblox...";
                _statusTextBlock.Foreground = Brushes.LimeGreen;
            });
        }
        else
        {
            _clickThreadTimer.Change(Timeout.Infinite, Timeout.Infinite);

            _window.Dispatcher.Invoke(() =>
            {
                _toggleButton.Content = "Start Clicker";
                _statusTextBlock.Text = "Auto Clicker Paused.";
                _statusTextBlock.Foreground = Brushes.Gray;
            });
        }

        SaveSettings();
    }

    private void ClickTimerCallback(object? state)
    {
        if (!_isClickerEnabled) return;

        var process = Process.GetProcessesByName("RobloxPlayerBeta").FirstOrDefault();
        if (process != null)
        {
            SimulateLeftClick();

            _window.Dispatcher.Invoke(() =>
            {
                _statusTextBlock.Text = "Clicking Roblox...";
                _statusTextBlock.Foreground = Brushes.LimeGreen;
            });
        }
        else
        {
            _window.Dispatcher.Invoke(() =>
            {
                _statusTextBlock.Text = "Roblox not running.";
                _statusTextBlock.Foreground = Brushes.Red;
            });
        }
    }

    private void SimulateLeftClick()
    {
        INPUT[] inputs = new INPUT[2];

        inputs[0].type = INPUT_MOUSE;
        inputs[0].mi.dwFlags = MOUSEEVENTF_LEFTDOWN;

        inputs[1].type = INPUT_MOUSE;
        inputs[1].mi.dwFlags = MOUSEEVENTF_LEFTUP;

        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
    }

    private void LoadSettings()
    {
        if (File.Exists(_settingsPath))
        {
            var lines = File.ReadAllLines(_settingsPath);
            if (lines.Length >= 3)
            {
                if (int.TryParse(lines[0], out int hotkeyVal))
                {
                    _hotkey = (Key)hotkeyVal;
                }
                if (int.TryParse(lines[1], out int modVal))
                {
                    _hotkeyModifiers = (ModifierKeys)modVal;
                }
                if (int.TryParse(lines[2], out int speed))
                {
                    _clickSpeedSlider.Value = Math.Max(1, speed);
                }

                _hotkeyTextBox.Text = GetHotkeyString();
                UpdateClickInterval();
            }
        }
        else
        {
            _hotkeyTextBox.Text = GetHotkeyString();
        }
    }

    private void SaveSettings()
    {
        File.WriteAllText(_settingsPath, $"{(int)_hotkey}\n{(int)_hotkeyModifiers}\n{(int)_clickSpeedSlider.Value}");
    }
}
