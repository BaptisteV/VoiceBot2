using VoiceBot2.Core;
using VoiceBot2.Core.Audio;
using VoiceBot2.Core.SpeechToText;

using var audio = new NAudioSource();
const string modelSmall = @"C:\Users\Bapt\Downloads\ggml-small-fp16.bin";
const string modelMedium = @"C:\Users\Bapt\Downloads\ggml-medium.bin";
const string modelTiny = @"C:\Users\Bapt\Downloads\ggml-tiny-fp16.bin";
using var whisper = new WhisperService(modelMedium, "fr");

var pipeline = new SpeechPipeline(audio, whisper);

audio.Start();
pipeline.Start();

Console.WriteLine("Press ENTER to stop...");
Console.ReadLine();

pipeline.Stop();
audio.Stop();