namespace VoiceBot2.Maui;

public partial class MainPage : ContentPage
{
    private readonly MainPageViewModel _vm;

    public MainPage(MainPageViewModel vm)
    {
        _vm = vm;
        InitializeComponent();
        BindingContext = _vm;
    }

    private void ContentPage_Loaded(object? sender, EventArgs e)
    {
        _vm.ShowHideWindow();
    }
}
