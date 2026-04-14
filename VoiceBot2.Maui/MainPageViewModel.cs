using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using H.NotifyIcon;
using System.Reactive.Linq;
using VoiceBot2.Core.Abstractions;

namespace VoiceBot2.Maui;

public sealed partial class MainPageViewModel : ObservableObject
{
    private readonly ISpeechPipeline _pipeline;
    private readonly IAudioSource _audio;

    public MainPageViewModel(ISpeechPipeline pipeline, IAudioSource audio)
    {
        _pipeline = pipeline;
        _pipeline.Voice
            .ObserveOn(SynchronizationContext.Current)
            .Subscribe(result =>
            {
                LastVoiceSegment = result.Result.Text;
            });
        _audio = audio;
    }

    public void LoadModel() => _pipeline.LoadModel();

    public void Start()
    {
        ResumeApp(Application.Current?.Windows[0]);
    }

    public void Stop()
    {
        PauseApp(Application.Current?.Windows[0]);
    }

    [ObservableProperty]
    public partial string IconPath { get; set; } = "on.ico";
    [ObservableProperty]
    public partial string LastVoiceSegment { get; set; } = "";

    private bool IsWindowVisible { get; set; } = true;

    [RelayCommand]
    public void ToggleActivation()
    {
        var window = Application.Current?.Windows[0];
        if (window is null)
            return;

        if (IsWindowVisible)
            PauseApp(window);
        else
            ResumeApp(window);

        IsWindowVisible = !IsWindowVisible;
    }

    private void ResumeApp(Window window)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            IconPath = "on.ico";
            window.Show();
            _pipeline.Start();
            _audio.Start();
        });
    }

    private void PauseApp(Window window)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            IconPath = "off.ico";
            window.Hide();
            _audio.Stop();
            _pipeline.Stop();
        });
    }

    [RelayCommand]
    public static void ExitApplication()
    {
        Application.Current?.Quit();
    }
}
