namespace VoiceBot2.Maui.Platforms.Windows;

public class WindowMessageEventArgs : EventArgs
{
    public int MessageId { get; set; }
    public IntPtr WParam { get; set; }
    public IntPtr LParam { get; set; }
}