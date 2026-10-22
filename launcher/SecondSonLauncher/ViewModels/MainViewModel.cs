using System.IO;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using SecondSonLauncher.Core;

namespace SecondSonLauncher.ViewModels;

public sealed record IssueView(string Text, Brush Brush, string Icon);

/// Everything the two pages show. Each setting writes its file as soon as it changes; the emulator
/// reads the files when the game starts.
public sealed class MainViewModel : ObservableObject
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static readonly Brush GoodBrush = Frozen("#4CC38A");
    static readonly Brush WarnBrush = Frozen("#E5B53B");
    static readonly Brush BadBrush = Frozen("#E5484D");

    readonly FlagsFile _flags = FlagsFile.Load();
    readonly GameConfig _cfg = GameConfig.Load();
    readonly LauncherSettings _ls = LauncherSettings.Load();
    readonly DispatcherTimer _runningTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    CancellationTokenSource? _deepCheck;

    public MainViewModel()
    {
        _gameFolder = _ls.GameFolder ?? LauncherSettings.FolderFromGameTxt() ?? "";
        PlayCommand = new RelayCommand(() => Play(), () => CanPlay);
        GoHomeCommand = new RelayCommand(() => Page = "Home");
        GoSettingsCommand = new RelayCommand(p =>
        {
            if (p is string key)
                SectionKey = key;
            Page = "Settings";
        });
        BrowseGameCommand = new RelayCommand(BrowseGame);
        CheckGameCommand = new RelayCommand(() => { Inspect(); StatusMessage = "Game files checked."; });
        DeepCheckCommand = new RelayCommand(DeepCheck, () => Report.Found);
        OpenGameFolderCommand = new RelayCommand(() => GameRunner.OpenFolder(GameFolder));
        OpenDlssFolderCommand = new RelayCommand(OpenDlssFolder);
        OpenDlssDownloadCommand = new RelayCommand(() => GameRunner.OpenUrl(DlssCheck.DownloadPage));
        RecheckDlssCommand = new RelayCommand(RefreshDlss);
        BackupNowCommand = new RelayCommand(BackupNow);
        RestoreBackupCommand = new RelayCommand(RestoreBackup, () => SelectedBackup != null && !IsRunning);
        OpenSavesCommand = new RelayCommand(() => OpenOrSay(AppPaths.SavesDir, "There are no saves yet: play once first."));
        OpenBackupsCommand = new RelayCommand(() => OpenOrSay(AppPaths.BackupDir, "No backups yet."));
        ClearShaderCacheCommand = new RelayCommand(ClearShaderCache, () => !IsRunning);
        OpenLogsCommand = new RelayCommand(() => OpenOrSay(AppPaths.LogDir, "No logs yet: they appear after the first start."));
        OpenCrashReportCommand = new RelayCommand(() =>
        {
            var fatal = Path.Combine(AppPaths.LogDir, "fatal.txt");
            if (File.Exists(fatal))
                GameRunner.OpenFile(fatal);
            else
                StatusMessage = "No crash report: the game has not crashed.";
        });
        OpenFlagsFileCommand = new RelayCommand(() =>
        {
            if (!File.Exists(AppPaths.UserFlags))
                _flags.Save();
            Process.Start(new ProcessStartInfo("notepad.exe", $"\"{AppPaths.UserFlags}\"") { UseShellExecute = true });
        });
        OpenBuildFolderCommand = new RelayCommand(() => GameRunner.OpenFolder(AppPaths.Root));
        DesktopShortcutCommand = new RelayCommand(CreateShortcut);

        _runningTimer.Tick += (_, _) => IsRunning = GameRunner.IsRunning();
        _runningTimer.Start();
        _isRunning = GameRunner.IsRunning();

        Inspect();
        RefreshDlss();
        RefreshBackups();
    }

    // ---------------------------------------------------------------- navigation

    string _page = "Home";
    public string Page { get => _page; set => Set(ref _page, value); }

    public IReadOnlyList<Section> Sections { get; } = new[]
    {
        new Section("Visuals", "Visuals", "Display, image & DLSS", ""),
        new Section("Controls", "Controls", "Mouse, keyboard & controller", ""),
        new Section("Game", "Game & Saves", "Game files, language & saves", ""),
        new Section("System", "System", "Renderer, launcher & maintenance", ""),
    };

    string _sectionKey = "Visuals";
    public string SectionKey
    {
        get => _sectionKey;
        set
        {
            if (value != null && Set(ref _sectionKey, value))
                Raise(nameof(CurrentSection));
        }
    }

    public Section CurrentSection => Sections.First(s => s.Key == SectionKey);

    // ---------------------------------------------------------------- home

    public RelayCommand PlayCommand { get; }
    public RelayCommand GoHomeCommand { get; }
    public RelayCommand GoSettingsCommand { get; }

    public string Version => "v" + (typeof(MainViewModel).Assembly.GetName().Version?.ToString(3) ?? "0");

    bool _isRunning;
    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (Set(ref _isRunning, value))
            {
                RaiseAll(nameof(Heading), nameof(StatusText), nameof(StatusBrush), nameof(PlayLabel), nameof(CanPlay));
                if (!value)
                {
                    StatusMessage = "The game has closed.";
                    RefreshBackups();
                }
            }
        }
    }

    public bool CanPlay => !IsRunning && Report.Found && File.Exists(AppPaths.EmulatorExe);

    public string PlayLabel => IsRunning ? "Running" : "Play";

    public string Heading => IsRunning ? "The game is running"
        : !Report.Found ? "Choose your game folder"
        : Report.HasErrors ? "Check your game files"
        : "Ready to play";

    public string StatusText => IsRunning ? "Running" : Report.Ready ? (Report.HasWarnings ? "Ready, with notes" : "Ready") : "Needs attention";
    public Brush StatusBrush => IsRunning || (Report.Ready && !Report.HasWarnings) ? GoodBrush : Report.Ready ? WarnBrush : BadBrush;

    public string Summary
    {
        get
        {
            var output = DisplayMode switch
            {
                "Windowed" => $"Window {BootScreen}",
                "Fullscreen" => "Fullscreen",
                _ => "Borderless",
            };
            var aa = DlssEnabled && DlssReady ? "DLSS" : "Game AA";
            return $"Renders {CurrentRender} · {output} · {aa} · {FrameCap} FPS cap";
        }
    }

    public string GameLine => Report.Found
        ? $"{Report.TitleId} · version {Report.Version}" + (Report.UpdateFolder != null ? " · update found" : "")
        : "No game folder chosen yet";

    public string VisualsCard => PresentModes.First(o => o.Value == PresentMode).Label.Split(" - ")[0];
    public string ControlsCard => MouseAim ? "Mouse aim on" : "Controller";
    public string GameCard => !Report.Found ? "Not set" : Report.HasErrors ? "Needs attention" : Report.HasWarnings ? "Notes" : "Ready";
    public string SystemCard => CompatRenderer ? "Compatibility renderer" : "Default";

    string _statusMessage = "";
    public string StatusMessage { get => _statusMessage; set => Set(ref _statusMessage, value); }

    BitmapImage? _background;
    public BitmapImage? BackgroundImage { get => _background; private set => Set(ref _background, value); }

    // ---------------------------------------------------------------- Visuals

    public IReadOnlyList<Option> DisplayModes { get; } = new[]
    {
        new Option("Fullscreen (Borderless)", "Borderless fullscreen"),
        new Option("Fullscreen", "Exclusive fullscreen"),
        new Option("Windowed", "Window"),
    };

    public string DisplayMode
    {
        get
        {
            if (!_cfg.Get("GPU", "full_screen", false))
                return "Windowed";
            return _cfg.Get("GPU", "full_screen_mode", "") == "Fullscreen" ? "Fullscreen" : "Fullscreen (Borderless)";
        }
        set
        {
            _cfg.Set("GPU", "full_screen", value != "Windowed");
            _cfg.Set("GPU", "full_screen_mode", value);
            SaveConfig(nameof(DisplayMode), nameof(IsWindowed));
            RaiseRenderScales();
        }
    }

    public bool IsWindowed => DisplayMode == "Windowed";

    public IReadOnlyList<Option> WindowSizes { get; } =
        new[] { "1280x720", "1600x900", "1920x1080", "2560x1440", "3840x2160" }
            .Select(s => new Option(s, s.Replace("x", " × "))).ToList();

    public string WindowSize
    {
        get => $"{_cfg.Get("GPU", "window_width", 1280)}x{_cfg.Get("GPU", "window_height", 720)}";
        set
        {
            var p = value.Split('x');
            _cfg.Set("GPU", "window_width", int.Parse(p[0], Inv));
            _cfg.Set("GPU", "window_height", int.Parse(p[1], Inv));
            SaveConfig(nameof(WindowSize));
            RaiseRenderScales();
        }
    }

    public IReadOnlyList<Option> PresentModes { get; } = new[]
    {
        new Option("Mailbox", "Fast - no tearing, low latency"),
        new Option("Fifo", "V-Sync"),
        new Option("Fifo (half refresh)", "V-Sync half refresh - even motion without G-Sync"),
        new Option("Immediate", "Off - lowest latency, can tear"),
    };

    public string PresentMode
    {
        get
        {
            var v = _cfg.Get("GPU", "present_mode", "Mailbox");
            return PresentModes.Any(o => o.Value == v) ? v : "Mailbox";
        }
        set
        {
            _cfg.Set("GPU", "present_mode", value);
            SaveConfig(nameof(PresentMode), nameof(VisualsCard));
        }
    }

    public IReadOnlyList<Option> FrameCaps { get; } =
        new[] { 30, 60, 72, 90, 120, 144, 165, 240 }.Select(n => new Option(n.ToString(Inv), $"{n} FPS")).ToList();

    public string FrameCap
    {
        get => _cfg.Get("GPU", "vblank_frequency", 60).ToString(Inv);
        set
        {
            _cfg.Set("GPU", "vblank_frequency", int.Parse(value, Inv));
            SaveConfig(nameof(FrameCap));
        }
    }

    public bool HdrAllowed
    {
        get => _cfg.Get("GPU", "hdr_allowed", false);
        set
        {
            _cfg.Set("GPU", "hdr_allowed", value);
            SaveConfig(nameof(HdrAllowed));
        }
    }

    static readonly (int Percent, string Name)[] RenderPicks =
    {
        (33, "DLSS Ultra Performance"), (50, "DLSS Performance"), (58, "DLSS Balanced"), (67, "DLSS Quality"),
        (75, ""), (85, ""), (100, "native"), (125, "supersampling"), (150, "supersampling"),
    };

    /// Each choice with the size it renders at in the current display mode and screen shape.
    public IReadOnlyList<Option> RenderScales
    {
        get
        {
            var saved = RenderMath.SavedScreenPercent();
            var first = saved > 0
                ? new Option("", $"In-game pick ({saved:0}%) · {RenderFor(saved)}")
                : new Option("", $"Game default · {RenderFor(0)}");
            return new[] { first }
                .Concat(RenderPicks.Select(p => new Option(p.Percent.ToString(Inv),
                    $"{p.Percent}% · {RenderFor(p.Percent)}" + (p.Name.Length > 0 ? $" ({p.Name})" : ""))))
                .ToList();
        }
    }

    /// The emulator's own order: SHADPS4_SCREEN_PERCENT, else the in-game slider's saved value.
    double EffectivePercent
    {
        get
        {
            var flag = RenderMath.Atof(_flags.GetEffective("SHADPS4_SCREEN_PERCENT"));
            return flag > 0 ? Math.Clamp(flag, 10, 200) : RenderMath.SavedScreenPercent();
        }
    }

    public RenderSize CurrentRender => RenderFor(EffectivePercent);

    RenderSize RenderFor(double percent) =>
        RenderMath.Compute(_flags.GetEffective("SHADPS4_ASPECT"), BootScreen, Display,
                           RenderMath.Atof(_flags.GetEffective("SHADPS4_RENDER_HEIGHT")), percent);

    static RenderSize Display => new(ScreenWidth, ScreenHeight);

    /// The window the game opens: the window size, or the whole monitor in fullscreen.
    RenderSize BootScreen
    {
        get
        {
            if (!IsWindowed)
                return Display;
            var p = WindowSize.Split('x');
            return new RenderSize(int.Parse(p[0], Inv), int.Parse(p[1], Inv));
        }
    }

    public string RenderScale
    {
        get => UserFlag("SHADPS4_SCREEN_PERCENT");
        set
        {
            // null comes from the drop-down while RenderScales is rebuilt, not from the player.
            if (value != null)
                SetFlag("SHADPS4_SCREEN_PERCENT", value);
        }
    }

    public IReadOnlyList<Option> Aspects { get; } = new[]
    {
        new Option("", "Automatic - fill the screen"),
        new Option("off", "16:9 with black bars"),
        new Option("crop", "Fill, cut the sides"),
        new Option("21:9", "21:9"),
        new Option("32:9", "32:9"),
    };

    public string Aspect
    {
        get => UserFlag("SHADPS4_ASPECT");
        set
        {
            if (value == null)
                return;
            SetFlag("SHADPS4_ASPECT", value);
            RaiseRenderScales();
        }
    }

    public IReadOnlyList<Option> HudPositions { get; } = new[]
    {
        new Option("", "Centered 16:9"),
        new Option("full", "Screen edges"),
    };

    public string HudPosition { get => UserFlag("SHADPS4_HUD"); set => SetFlag("SHADPS4_HUD", value); }

    public IReadOnlyList<Option> PerfModes { get; } = new[]
    {
        new Option("", "Off - full quality"),
        new Option("low", "Low - simpler distance, half-res smoke"),
        new Option("office", "Office - lowest, fewer pedestrians"),
    };

    public string PerfMode { get => UserFlag("SHADPS4_PERF_MODE"); set => SetFlag("SHADPS4_PERF_MODE", value); }

    bool _dlssReady;
    public bool DlssReady { get => _dlssReady; private set => Set(ref _dlssReady, value); }

    string _dlssStatus = "";
    public string DlssStatus { get => _dlssStatus; private set => Set(ref _dlssStatus, value); }
    public Brush DlssStatusBrush => DlssReady ? GoodBrush : WarnBrush;

    public bool DlssEnabled
    {
        get => _flags.GetEffective("SHADPS4_DLSS") != "0";
        set => SetFlag("SHADPS4_DLSS", value ? "1" : "0");
    }

    public IReadOnlyList<Option> DlssPresets { get; } = new[]
    {
        new Option("", "Build default"),
        new Option("0", "DLSS's own choice"),
        new Option("11", "K - DLSS 4"),
        new Option("12", "L - DLSS 4.5, sharpest"),
        new Option("13", "M - DLSS 4.5, faster"),
    };

    public string DlssPreset { get => UserFlag("SHADPS4_DLSS_PRESET"); set => SetFlag("SHADPS4_DLSS_PRESET", value); }

    public bool DlssHalton
    {
        get => _flags.GetEffective("SHADPS4_DLSS_HALTON") != "0";
        set => SetFlag("SHADPS4_DLSS_HALTON", value ? null : "0");
    }

    public RelayCommand OpenDlssFolderCommand { get; }
    public RelayCommand OpenDlssDownloadCommand { get; }
    public RelayCommand RecheckDlssCommand { get; }

    // ---------------------------------------------------------------- Controls

    public bool MouseAim
    {
        get => _flags.GetEffective("SHADPS4_MOUSE_AIM") != "0";
        set => SetFlag("SHADPS4_MOUSE_AIM", value ? "1" : "0");
    }

    public bool CustomMouseSpeed
    {
        get => _flags.GetUser("SHADPS4_MOUSE_AIM_SENS") != null;
        set
        {
            SetFlag("SHADPS4_MOUSE_AIM_SENS", value ? MouseSpeed.ToString("0.##", Inv) : null);
            Raise(nameof(MouseSpeed));
        }
    }

    public double MouseSpeed
    {
        get => double.TryParse(_flags.GetUser("SHADPS4_MOUSE_AIM_SENS"), NumberStyles.Float, Inv, out var v) ? v : 5.12;
        set
        {
            if (CustomMouseSpeed)
                SetFlag("SHADPS4_MOUSE_AIM_SENS", Math.Round(value, 2).ToString("0.##", Inv));
        }
    }

    public IReadOnlyList<Option> ButtonIconModes { get; } = new[]
    {
        new Option("1", "Follow the device used last"),
        new Option("0", "Always PlayStation buttons"),
    };

    public string ButtonIcons
    {
        get => _flags.GetEffective("SHADPS4_TEX_REPLACE") == "0" ? "0" : "1";
        set => SetFlag("SHADPS4_TEX_REPLACE", value);
    }

    public double DeadzoneLeft
    {
        get => _cfg.Get("Input", "stick_deadzone_left", 7.5);
        set
        {
            _cfg.Set("Input", "stick_deadzone_left", Math.Round(value, 1));
            SaveConfig(nameof(DeadzoneLeft));
        }
    }

    public double DeadzoneRight
    {
        get => _cfg.Get("Input", "stick_deadzone_right", 7.5);
        set
        {
            _cfg.Set("Input", "stick_deadzone_right", Math.Round(value, 1));
            SaveConfig(nameof(DeadzoneRight));
        }
    }

    public bool TouchpadHalves
    {
        get => _flags.GetEffective("SHADPS4_PAD_TOUCH_HALVES") != "0";
        set => SetFlag("SHADPS4_PAD_TOUCH_HALVES", value ? null : "0");
    }

    public bool MotionControls
    {
        get => _cfg.Get("Input", "motion_controls_enabled", true);
        set
        {
            _cfg.Set("Input", "motion_controls_enabled", value);
            SaveConfig(nameof(MotionControls));
        }
    }

    public double PadSpeakerVolume
    {
        get => double.TryParse(_flags.GetEffective("SHADPS4_PADSPK_VOLUME"), NumberStyles.Float, Inv, out var v) ? v : 25;
        set => SetFlag("SHADPS4_PADSPK_VOLUME", ((int)Math.Round(value)).ToString(Inv));
    }

    // ---------------------------------------------------------------- Game & Saves

    public RelayCommand BrowseGameCommand { get; }
    public RelayCommand CheckGameCommand { get; }
    public RelayCommand DeepCheckCommand { get; }
    public RelayCommand OpenGameFolderCommand { get; }

    string _gameFolder;
    public string GameFolder
    {
        get => _gameFolder;
        private set
        {
            if (Set(ref _gameFolder, value))
            {
                _ls.GameFolder = value;
                _ls.Save();
                Inspect();
            }
        }
    }

    GameReport _report = GameInspector.Inspect(null);
    public GameReport Report
    {
        get => _report;
        private set
        {
            _report = value;
            RaiseAll(nameof(Report), nameof(Issues), nameof(Heading), nameof(StatusText), nameof(StatusBrush),
                  nameof(GameLine), nameof(GameCard), nameof(CanPlay), nameof(GameTitle));
        }
    }

    public string GameTitle => Report.Found ? Report.Title : "inFAMOUS Second Son";

    public IReadOnlyList<IssueView> Issues => Report.Issues.Select(i => i.Level switch
    {
        IssueLevel.Ok => new IssueView(i.Text, GoodBrush, ""),
        IssueLevel.Warning => new IssueView(i.Text, WarnBrush, ""),
        _ => new IssueView(i.Text, BadBrush, ""),
    }).ToList();

    string _deepCheckText = "";
    public string DeepCheckText { get => _deepCheckText; private set => Set(ref _deepCheckText, value); }

    public IReadOnlyList<Option> Languages { get; } = new[]
    {
        new Option("1", "English (United States)"),
        new Option("18", "English (United Kingdom)"),
        new Option("17", "Portuguese (Brazil)"),
        new Option("7", "Portuguese (Portugal)"),
        new Option("20", "Spanish (Latin America)"),
        new Option("3", "Spanish (Spain)"),
        new Option("2", "French"),
        new Option("4", "German"),
        new Option("5", "Italian"),
        new Option("6", "Dutch"),
        new Option("16", "Polish"),
        new Option("8", "Russian"),
        new Option("19", "Turkish"),
        new Option("14", "Danish"),
        new Option("12", "Finnish"),
        new Option("15", "Norwegian"),
        new Option("13", "Swedish"),
    };

    public string Language
    {
        get => _cfg.Get("General", "console_language", 1).ToString(Inv);
        set
        {
            _cfg.Set("General", "console_language", int.Parse(value, Inv));
            SaveConfig(nameof(Language));
        }
    }

    public double Volume
    {
        get => _cfg.Get("General", "volume_slider", 100);
        set
        {
            _cfg.Set("General", "volume_slider", (int)Math.Round(value));
            SaveConfig(nameof(Volume));
        }
    }

    public bool BackupBeforePlay
    {
        get => _ls.BackupBeforePlay;
        set
        {
            _ls.BackupBeforePlay = value;
            _ls.Save();
            Raise();
        }
    }

    IReadOnlyList<string> _backups = Array.Empty<string>();
    public IReadOnlyList<string> Backups { get => _backups; private set => Set(ref _backups, value); }

    string? _selectedBackup;
    public string? SelectedBackup { get => _selectedBackup; set => Set(ref _selectedBackup, value); }

    public RelayCommand BackupNowCommand { get; }
    public RelayCommand RestoreBackupCommand { get; }
    public RelayCommand OpenSavesCommand { get; }
    public RelayCommand OpenBackupsCommand { get; }

    // ---------------------------------------------------------------- System

    public IReadOnlyList<Option> GpuChoices { get; } = new[]
    {
        new Option("-1", "Automatic (dedicated card first)"),
        new Option("0", "First graphics card (0)"),
        new Option("1", "Second graphics card (1)"),
    };

    public string GpuId
    {
        get => _flags.GetEffective("SHADPS4_GPU_ID") ?? "-1";
        set => SetFlag("SHADPS4_GPU_ID", value);
    }

    public bool CompatRenderer
    {
        get => _flags.GetEffective("SHADPS4_A2") == "0";
        set
        {
            SetFlag("SHADPS4_A2", value ? "0" : "1");
            Raise(nameof(SystemCard));
        }
    }

    public bool UpdateCheck
    {
        get => _flags.GetEffective("SHADPS4_UPDATE_CHECK") != "0";
        set => SetFlag("SHADPS4_UPDATE_CHECK", value ? null : "0");
    }

    public bool StartDirectly
    {
        get => _ls.StartDirectly;
        set
        {
            _ls.StartDirectly = value;
            _ls.Save();
            Raise();
        }
    }

    public bool CloseOnPlay
    {
        get => _ls.CloseOnPlay;
        set
        {
            _ls.CloseOnPlay = value;
            _ls.Save();
            Raise();
        }
    }

    public RelayCommand ClearShaderCacheCommand { get; }
    public RelayCommand OpenLogsCommand { get; }
    public RelayCommand OpenCrashReportCommand { get; }
    public RelayCommand OpenFlagsFileCommand { get; }
    public RelayCommand OpenBuildFolderCommand { get; }
    public RelayCommand DesktopShortcutCommand { get; }

    // ---------------------------------------------------------------- actions

    /// "Start the game directly": true when the launcher should start the game and close.
    public bool ShouldStartDirectly(bool modifierHeld) =>
        _ls.StartDirectly && !modifierHeld && Report.Ready && !IsRunning && File.Exists(AppPaths.EmulatorExe);

    public bool Play()
    {
        if (!CanPlay)
            return false;
        if (Report.HasErrors &&
            MessageBox.Show(string.Join("\n\n", Report.Issues.Where(i => i.Level == IssueLevel.Error).Select(i => i.Text)) +
                            "\n\nStart anyway?", "Game files", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return false;
        try
        {
            var backup = BackupBeforePlay ? SaveBackups.Create() : null;
            GameRunner.Start(Report.Folder);
            IsRunning = true;
            StatusMessage = backup != null ? $"Saves backed up ({backup}). Have fun!" : "Starting the game...";
            RefreshBackups();
            if (CloseOnPlay)
                Application.Current.Shutdown();
            return true;
        }
        catch (Exception e)
        {
            MessageBox.Show("The game could not start:\n\n" + e.Message, "Play", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    void Inspect()
    {
        Report = GameInspector.Inspect(GameFolder);
        BackgroundImage = LoadImage(Report.Background);
    }

    void BrowseGame()
    {
        var dialog = new OpenFolderDialog { Title = "Choose the game folder (the one with eboot.bin)" };
        if (Directory.Exists(GameFolder))
            dialog.InitialDirectory = GameFolder;
        if (dialog.ShowDialog() != true)
            return;
        var folder = dialog.FolderName;
        // A parent folder holding CUSA00004 works too.
        if (!File.Exists(Path.Combine(folder, "eboot.bin")))
        {
            var inner = GameInspector.SupportedSerials.Select(s => Path.Combine(folder, s))
                .FirstOrDefault(d => File.Exists(Path.Combine(d, "eboot.bin")));
            if (inner != null)
                folder = inner;
        }
        GameFolder = folder;
        StatusMessage = Report.Ready ? "Game folder saved." : "Game folder saved - see the notes under Game & Saves.";
    }

    async void DeepCheck()
    {
        if (_deepCheck != null)
        {
            _deepCheck.Cancel();
            return;
        }
        _deepCheck = new CancellationTokenSource();
        try
        {
            var progress = new Progress<string>(t => DeepCheckText = t);
            var empty = await GameInspector.FindEmptyFilesAsync(Report.Folder, progress, _deepCheck.Token);
            DeepCheckText = empty.Count == 0
                ? "Full check done: every file has data."
                : $"{empty.Count} empty files, for example {string.Join(", ", empty.Take(3))}. Extract the PKGs again with PKG Extractor 1.1 or newer.";
        }
        catch (OperationCanceledException)
        {
            DeepCheckText = "Full check stopped.";
        }
        finally
        {
            _deepCheck.Dispose();
            _deepCheck = null;
        }
    }

    void RefreshDlss()
    {
        var (ready, text) = DlssCheck.Check();
        DlssReady = ready;
        DlssStatus = text;
        RaiseAll(nameof(DlssStatusBrush), nameof(Summary));
    }

    void OpenDlssFolder()
    {
        Directory.CreateDirectory(AppPaths.DlssDir);
        GameRunner.OpenFolder(AppPaths.DlssDir);
    }

    void RefreshBackups()
    {
        Backups = SaveBackups.List();
        SelectedBackup = Backups.FirstOrDefault();
    }

    void BackupNow()
    {
        try
        {
            var name = SaveBackups.Create();
            StatusMessage = name == null ? "There are no saves to back up yet." : $"Saves backed up ({name}).";
            RefreshBackups();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(e.Message, "Back up saves", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    void RestoreBackup()
    {
        if (SelectedBackup == null)
            return;
        if (MessageBox.Show($"Replace your current saves with the backup from {SelectedBackup}?\n\n" +
                            "Your current saves are backed up first.", "Restore saves",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        try
        {
            var name = SelectedBackup;
            SaveBackups.Restore(name);
            RefreshBackups();
            StatusMessage = $"Saves restored from {name}.";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            MessageBox.Show(e.Message, "Restore saves", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    void ClearShaderCache()
    {
        var dirs = Directory.Exists(AppPaths.CacheDir)
            ? Directory.GetDirectories(AppPaths.CacheDir, AppPaths.Serial + "*")
            : Array.Empty<string>();
        if (dirs.Length == 0)
        {
            StatusMessage = "The shader cache is already empty.";
            return;
        }
        if (MessageBox.Show("Delete the saved shaders? The next start prepares them again (a few minutes) and the " +
                            "first minutes of play may stutter.", "Clear shader cache",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        foreach (var d in dirs)
            Directory.Delete(d, true);
        StatusMessage = "Shader cache cleared.";
    }

    void CreateShortcut()
    {
        try
        {
            GameRunner.CreateDesktopShortcut();
            StatusMessage = "Shortcut created on the desktop.";
        }
        catch (Exception e)
        {
            MessageBox.Show(e.Message, "Desktop shortcut", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    void OpenOrSay(string folder, string message)
    {
        if (Directory.Exists(folder))
            GameRunner.OpenFolder(folder);
        else
            StatusMessage = message;
    }

    // ---------------------------------------------------------------- helpers

    string UserFlag(string name) => _flags.GetUser(name) ?? "";

    void SetFlag(string name, string? value, [System.Runtime.CompilerServices.CallerMemberName] string? prop = null)
    {
        // Unchanged (a drop-down handing back its own value): no write, no notifications.
        var before = _flags.GetUser(name);
        _flags.Set(name, string.IsNullOrEmpty(value) ? null : value);
        if (before == _flags.GetUser(name))
            return;
        try
        {
            _flags.Save();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            StatusMessage = "Could not save shadps4-flags-user.txt: " + e.Message;
        }
        Raise(prop!);
        RaiseSummaries();
    }

    void SaveConfig(params string[] props)
    {
        try
        {
            _cfg.Save();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            StatusMessage = "Could not save the game settings: " + e.Message;
        }
        RaiseAll(props);
        RaiseSummaries();
    }

    void RaiseSummaries() =>
        RaiseAll(nameof(Summary), nameof(VisualsCard), nameof(ControlsCard), nameof(SystemCard),
                 nameof(CurrentRender));

    /// The render sizes in the Render size list depend on the window and the screen shape only.
    void RaiseRenderScales() => RaiseAll(nameof(RenderScales), nameof(RenderScale));

    static BitmapImage? LoadImage(string? path)
    {
        if (path == null)
            return null;
        try
        {
            var img = new BitmapImage();
            img.BeginInit();
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.UriSource = new Uri(path);
            img.EndInit();
            img.Freeze();
            return img;
        }
        catch (Exception e) when (e is IOException or NotSupportedException or UriFormatException)
        {
            return null;
        }
    }

    static Brush Frozen(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }

    [DllImport("user32.dll")]
    static extern int GetSystemMetrics(int index);

    static int ScreenWidth => GetSystemMetrics(0);
    static int ScreenHeight => GetSystemMetrics(1);
}
