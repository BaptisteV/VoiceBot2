using Microsoft.Extensions.Logging;
using VoiceBot2.Core.Abstractions;
using VoiceBot2.Core.Model;

namespace VoiceBot2.Maui;

public sealed partial class MainPage : ContentPage, IDisposable
{
    private readonly MainPageViewModel _vm;
    private readonly ILogger<MainPage> _logger;

    public MainPage(MainPageViewModel vm, ILogger<MainPage> logger, ISpeechPipeline pipeline)
    {
        _vm = vm;
        _logger = logger;
        InitializeComponent();
        pipeline.Voice.Subscribe(OnVoiceMainPage);
        BindingContext = _vm;
    }

    private void OnVoiceMainPage(TimedResult result)
    {
    }

    private void ContentPage_Loaded(object? sender, EventArgs e)
    {
        _vm.LoadModel();
        _vm.Start();
        //_vm.ShowHideWindow();
    }

    public void Dispose()
    {
        _vm.Stop();
    }
}
