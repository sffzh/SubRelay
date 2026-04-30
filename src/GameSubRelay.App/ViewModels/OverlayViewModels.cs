using System.Collections.ObjectModel;
using GameSubRelay.Core.Audio;
using GameSubRelay.Core.Captions;
using GameSubRelay.Core.Translation;

namespace GameSubRelay.App.ViewModels;

public sealed class OverlayRenderSettings : ViewModelBase
{
    private double _left = 120;
    private double _top = 720;
    private double _width = 760;
    private double _height = 240;
    private double _opacity = 0.65;
    private double _fontSize = 22;
    private int _maxLines = 6;
    private bool _visible;

    public double Left
    {
        get => _left;
        set => SetProperty(ref _left, value);
    }

    public double Top
    {
        get => _top;
        set => SetProperty(ref _top, value);
    }

    public double Width
    {
        get => _width;
        set => SetProperty(ref _width, value);
    }

    public double Height
    {
        get => _height;
        set => SetProperty(ref _height, value);
    }

    public double Opacity
    {
        get => _opacity;
        set => SetProperty(ref _opacity, value);
    }

    public double FontSize
    {
        get => _fontSize;
        set => SetProperty(ref _fontSize, value);
    }

    public int MaxLines
    {
        get => _maxLines;
        set => SetProperty(ref _maxLines, value);
    }

    public bool Visible
    {
        get => _visible;
        set => SetProperty(ref _visible, value);
    }
}

public sealed class CaptionLineViewModel : ViewModelBase
{
    private string _channelLabel = "Mic";
    private string _sourceText = string.Empty;
    private string _translatedText = string.Empty;
    private bool _isFinal;

    public string ChannelLabel
    {
        get => _channelLabel;
        set => SetProperty(ref _channelLabel, value);
    }

    public string SourceText
    {
        get => _sourceText;
        set
        {
            if (SetProperty(ref _sourceText, value))
            {
                OnPropertyChanged(nameof(PrimaryText));
            }
        }
    }

    public string TranslatedText
    {
        get => _translatedText;
        set
        {
            if (SetProperty(ref _translatedText, value))
            {
                OnPropertyChanged(nameof(PrimaryText));
            }
        }
    }

    public string PrimaryText => string.IsNullOrWhiteSpace(SourceText) ? TranslatedText : SourceText;

    public bool IsFinal
    {
        get => _isFinal;
        set => SetProperty(ref _isFinal, value);
    }

    public void Clear()
    {
        SourceText = string.Empty;
        TranslatedText = string.Empty;
        IsFinal = false;
    }

    public void Apply(CaptionLine line, string? channelLabel = null)
    {
        ChannelLabel = string.IsNullOrWhiteSpace(channelLabel) ? line.ChannelLabel : channelLabel;
        SourceText = line.SourceText;
        TranslatedText = line.TranslatedText;
        IsFinal = line.Stability == SegmentStability.Final;
    }
}

public sealed class OverlayViewModel : ViewModelBase
{
    private readonly OverlayRenderSettings _settings;
    private bool _isEditMode;
    private bool _isVisible;

    public OverlayViewModel(OverlayRenderSettings? settings = null)
    {
        _settings = settings ?? new OverlayRenderSettings();
        _isVisible = _settings.Visible;
        Captions = new ObservableCollection<CaptionLineViewModel>();
        MicrophoneCaption = new CaptionLineViewModel { ChannelLabel = "麦克风" };
        MonitorCaption = new CaptionLineViewModel { ChannelLabel = "系统声音" };

        ToggleVisibilityCommand = new RelayCommand(_ => ToggleOverlayVisibility());
        ToggleEditModeCommand = new RelayCommand(_ => ToggleEditMode());
        ClearCaptionsCommand = new RelayCommand(_ => ClearCaptions());
    }

    public OverlayRenderSettings Settings => _settings;

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (SetProperty(ref _isVisible, value))
            {
                _settings.Visible = value;
                OnPropertyChanged(nameof(IsWindowVisible));
            }
        }
    }

    public bool IsEditMode
    {
        get => _isEditMode;
        set
        {
            if (SetProperty(ref _isEditMode, value))
            {
                OnPropertyChanged(nameof(EditModeText));
            }
        }
    }

    public string EditModeText => IsEditMode ? "Exit edit mode" : "Enter edit mode";

    public bool IsWindowVisible => IsVisible && Settings.Visible;

    public ObservableCollection<CaptionLineViewModel> Captions { get; }

    public CaptionLineViewModel MicrophoneCaption { get; }

    public CaptionLineViewModel MonitorCaption { get; }

    public RelayCommand ToggleVisibilityCommand { get; set; }
    public RelayCommand ToggleEditModeCommand { get; set; }
    public RelayCommand ClearCaptionsCommand { get; set; }

    public void ToggleOverlayVisibility()
    {
        IsVisible = !IsVisible;
        OnPropertyChanged(nameof(IsWindowVisible));
    }

    public void ToggleEditMode()
    {
        IsEditMode = !IsEditMode;
        OnPropertyChanged(nameof(EditModeText));
    }

    public void ClearCaptions()
    {
        Captions.Clear();
        MicrophoneCaption.Clear();
        MonitorCaption.Clear();
    }

    public void ApplyCaptionSnapshot(IReadOnlyList<CaptionLine> lines)
    {
        Captions.Clear();
        foreach (var line in lines)
        {
            Captions.Add(new CaptionLineViewModel
            {
                ChannelLabel = line.ChannelLabel,
                SourceText = line.SourceText,
                TranslatedText = line.TranslatedText,
                IsFinal = line.Stability == SegmentStability.Final
            });
        }

        EnsureMaxLines();
        ApplyLatestChannelCaption(lines, AudioChannelId.Microphone, MicrophoneCaption, "麦克风");
        ApplyLatestChannelCaption(lines, AudioChannelId.Monitor, MonitorCaption, "系统声音");
    }

    private static void ApplyLatestChannelCaption(
        IEnumerable<CaptionLine> lines,
        AudioChannelId channelId,
        CaptionLineViewModel target,
        string channelLabel)
    {
        var latest = lines
            .Where(line => line.ChannelId == channelId)
            .OrderByDescending(line => line.UpdatedAt)
            .FirstOrDefault(line =>
                !string.IsNullOrWhiteSpace(line.SourceText) ||
                !string.IsNullOrWhiteSpace(line.TranslatedText));

        if (latest is null)
        {
            target.Clear();
            target.ChannelLabel = channelLabel;
            return;
        }

        target.Apply(latest, channelLabel);
    }

    public void EnsureMaxLines()
    {
        var maxLines = Math.Clamp(Settings.MaxLines, 1, 12);
        while (Captions.Count > maxLines)
        {
            Captions.RemoveAt(0);
        }
    }

    public void OnMaxLinesChanged()
    {
        EnsureMaxLines();
        OnPropertyChanged(nameof(Settings));
    }

    public void RefreshCommands()
    {
        ToggleVisibilityCommand.RaiseCanExecuteChanged();
        ToggleEditModeCommand.RaiseCanExecuteChanged();
        ClearCaptionsCommand.RaiseCanExecuteChanged();
    }

    public OverlayViewModel()
        : this(new OverlayRenderSettings())
    {
    }
}
