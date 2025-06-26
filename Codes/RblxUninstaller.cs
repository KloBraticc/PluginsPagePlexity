using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

public class TestPluginLogic
{
    private Window _window;
    private Button _button;
    private ProgressBar _loadingBar;

    public TestPluginLogic(Window window)
    {
        _window = window;

        var grid = (Grid)_window.Content;
        _button = (Button)grid.FindName("MyButton");
        _loadingBar = (ProgressBar)grid.FindName("LoadingBar");

        if (_button != null)
        {
            _button.Click += async (s, e) => await UninstallButton_Click();
        }
    }

    private async Task UninstallButton_Click()
    {
        var result = MessageBox.Show(
            "This will permanently remove Roblox and all its data. Are you sure you want to continue?",
            "Confirmation",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        _button.IsEnabled = false;
        _button.Content = "Uninstalling...";
        _loadingBar.Visibility = Visibility.Visible;
        _loadingBar.Value = 0;

        try
        {
            await Task.Run(() => TryKillRobloxProcesses());
            _loadingBar.Value = 20;

            await Task.Run(() => TryRunOfficialUninstaller());
            _loadingBar.Value = 40;

            await Task.Run(() => TryDeleteRobloxFolders());
            _loadingBar.Value = 60;

            await Task.Run(() => TryCleanRegistry());
            _loadingBar.Value = 80;

            await Task.Run(() => TryDeleteShortcuts());
            _loadingBar.Value = 100;

            _button.Content = "Finished!";
            MessageBox.Show("Roblox has been completely uninstalled.\nIt is recommended to restart your computer.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"An error occurred during uninstallation:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _loadingBar.Visibility = Visibility.Collapsed;
            _button.IsEnabled = true;
        }
    }

    private void TryKillRobloxProcesses()
    {
        string[] processNames = { "RobloxPlayerBeta", "RobloxPlayerLauncher", "RobloxStudioBeta", "RobloxStudioLauncher" };
        foreach (var name in processNames)
        {
            try
            {
                var procs = Process.GetProcessesByName(name);
                foreach (var proc in procs)
                {
                    proc.Kill();
                    proc.WaitForExit();
                }
            }
            catch { }
        }
    }

    private void TryRunOfficialUninstaller()
    {
        try
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string robloxVersionsPath = Path.Combine(localAppData, "Roblox", "Versions");
            if (!Directory.Exists(robloxVersionsPath)) return;

            var uninstallerPath = Directory.GetFiles(robloxVersionsPath, "RobloxPlayerUninstaller.exe", SearchOption.AllDirectories).FirstOrDefault();
            if (uninstallerPath != null)
            {
                var proc = Process.Start(uninstallerPath);
                proc?.WaitForExit(30000);
                if (proc != null && !proc.HasExited) proc.Kill();
            }
        }
        catch { }
    }

    private void TryDeleteRobloxFolders()
    {
        var folders = new List<string>()
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Roblox"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "RbxLogs"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Roblox"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Roblox"),

            // Additional folder to delete
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Plexity", "Downloads")
        };

        foreach (var folder in folders)
        {
            try
            {
                if (Directory.Exists(folder))
                    Directory.Delete(folder, true);
            }
            catch { }
        }
    }

    private void TryCleanRegistry()
    {
        try
        {
            using (var key = Registry.CurrentUser.OpenSubKey("Software", true))
            {
                DeleteRegistryKeySafe(key, "Roblox");
                DeleteRegistryKeySafe(key, "Roblox Corporation");
            }

            using (var root = Registry.ClassesRoot)
            {
                DeleteRegistryKeySafe(root, "roblox-player");
            }
        }
        catch { }
    }

    private void DeleteRegistryKeySafe(RegistryKey parentKey, string keyName)
    {
        try
        {
            if (parentKey.OpenSubKey(keyName) != null)
                parentKey.DeleteSubKeyTree(keyName);
        }
        catch { }
    }

    private void TryDeleteShortcuts()
    {
        var shortcuts = new List<string>()
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Roblox Player.lnk"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Roblox Studio.lnk"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "Roblox"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), "Roblox Player.lnk"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), "Roblox Studio.lnk")
        };

        foreach (var path in shortcuts)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
                else if (Directory.Exists(path)) Directory.Delete(path, true);
            }
            catch { }
        }
    }
}
