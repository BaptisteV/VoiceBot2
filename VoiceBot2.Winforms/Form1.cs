using Microsoft.Extensions.Logging;
using System.Reactive.Linq;
using VoiceBot2.Win.Voices;

namespace VoiceBot2.Win
{
    public partial class Form1 : Form
    {
        private readonly IVoiceListener _micListener;
        private readonly ILogger<Form1> _logger;

        public Form1(IVoiceListener micListener, ILogger<Form1> logger)
        {
            InitializeComponent();
            _micListener = micListener;
            _logger = logger;
            _micListener.VoiceChunks().Subscribe(chunk =>
            {
                // For demonstration, we'll just write the chunks to the console.
                // In a real application, you might want to update the UI or process the chunks differently.
                Console.WriteLine($"Received voice chunk: {chunk}");
            });
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
        }
    }
}
