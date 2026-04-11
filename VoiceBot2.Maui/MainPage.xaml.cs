using CommunityToolkit.Mvvm.Input;
using H.NotifyIcon;

namespace VoiceBot2.Maui
{
    public partial class MainPage : ContentPage
    {
        private bool IsWindowVisible { get; set; } = true;

        public MainPage()
        {
            InitializeComponent();
            BindingContext = this;
        }

        [RelayCommand]
        public void ShowHideWindow()
        {
            var window = Application.Current?.Windows[0];
            if (window == null)
            {
                return;
            }

            if (IsWindowVisible)
            {
                window.Hide();
            }
            else
            {
                window.Show();
            }
            IsWindowVisible = !IsWindowVisible;
        }

        [RelayCommand]
        public static void ExitApplication()
        {
            Application.Current?.Quit();
        }

        private void ContentPage_Loaded(object sender, EventArgs e)
        {
            ShowHideWindow();
        }
    }
}
