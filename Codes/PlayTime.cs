using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

public class TestPluginLogic
{
    private readonly TextBlock _currentTextBlock;
    private readonly TextBlock _topTextBlock;
    private readonly DispatcherTimer _timer;
    private DateTime? _processStartTime;
    private TimeSpan _topPlaytime = TimeSpan.Zero;
    private readonly string _saveFilePath;

    public TestPluginLogic(Window window)
    {
        _currentTextBlock = (TextBlock)((Grid)window.Content).FindName("MyTextBlock");
        _topTextBlock = (TextBlock)((Grid)window.Content).FindName("TopPlaytimeTextBlock");

        _saveFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "LongestPlaytime.txt");
        LoadTopPlaytime();

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += Timer_Tick;
        _timer.Start();
    }

    private void Timer_Tick(object sender, EventArgs e)
    {
        var process = Process.GetProcessesByName("RobloxPlayerBeta").FirstOrDefault();

        if (process != null)
        {
            if (_processStartTime == null)
            {
                _processStartTime = process.StartTime;
            }

            TimeSpan elapsed = DateTime.Now - _processStartTime.Value;

            if (_currentTextBlock != null)
            {
                _currentTextBlock.Text = $"Your Top Most Playtime: {elapsed:hh\\:mm\\:ss}";
                _currentTextBlock.Foreground = Brushes.LimeGreen;
            }

            if (elapsed > _topPlaytime)
            {
                _topPlaytime = elapsed;
                SaveTopPlaytime();

                if (_topTextBlock != null)
                {
                    _topTextBlock.Text = $"Longest Session: {_topPlaytime:hh\\:mm\\:ss}";
                    _topTextBlock.Foreground = Brushes.Gold; // Highlight when new record
                }
            }
            else
            {
                if (_topTextBlock != null)
                {
                    _topTextBlock.Foreground = Brushes.White;
                }
            }
        }
        else
        {
            _processStartTime = null;

            if (_currentTextBlock != null)
            {
                _currentTextBlock.Text = "Roblox not running.";
                _currentTextBlock.Foreground = Brushes.Red;
            }

            if (_topTextBlock != null)
            {
                _topTextBlock.Foreground = Brushes.Gray;
            }
        }
    }

    private void LoadTopPlaytime()
    {
        if (File.Exists(_saveFilePath))
        {
            string text = File.ReadAllText(_saveFilePath);
            if (TimeSpan.TryParse(text, out var loadedTime))
            {
                _topPlaytime = loadedTime;

                if (_topTextBlock != null)
                {
                    _topTextBlock.Text = $"Longest Session: {_topPlaytime:hh\\:mm\\:ss}";
                    _topTextBlock.Foreground = Brushes.White;
                }
            }
        }
    }

    private void SaveTopPlaytime()
    {
        File.WriteAllText(_saveFilePath, _topPlaytime.ToString());
    }
}
