using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using H.NotifyIcon;
using System.Reactive.Linq;
using VoiceBot2.Core.Abstractions;
using VoiceBot2.Core.Model;

namespace VoiceBot2.Maui;

public class AppendVoiceObserver : IObserver<TimedResult>
{
    private readonly List<TimedResult> _recentResults = [];

    public void OnCompleted() { }
    public void OnError(Exception error) { }

    private void LimitToLast1000()
    {
        while (_recentResults.Sum(r => r.Result.Text.Length) > 1000)
        {
            _recentResults.RemoveAt(0);
        }
    }

    public void OnNext(TimedResult value)
    {
        _recentResults.Add(value);
        LimitToLast1000();
    }

    public string GetBuffer() => string.Concat(_recentResults.SelectMany(r => r.Result.Text));
}

public sealed partial class MainPageViewModel : ObservableObject
{
    private readonly ISpeechPipeline _pipeline;
    private readonly IAudioSource _audio;
    private readonly AppendVoiceObserver _voiceObserver = new();

    public MainPageViewModel(ISpeechPipeline pipeline, IAudioSource audio)
    {
        _pipeline = pipeline;
        _pipeline.Voice
            .ObserveOn(SynchronizationContext.Current!)
            .Subscribe(_voiceObserver);
        _pipeline.Voice
            .ObserveOn(SynchronizationContext.Current!)
            .Subscribe(result =>
            {
                LastVoiceSegment = result.Result.Text;
                FullVoice = _voiceObserver.GetBuffer();
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
    [ObservableProperty]
    public partial string FullVoice { get; set; } = "";

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
