# VoiceBot2

## Description
This is an attempt at a voice controlled AI Agent.
It handles voice commands using Speech-To-Text.
Speech-To-Text is based on [Whisper.net](https://github.com/sandrohanea/whisper.net) using the ggml-medium.gguf [Whisper model](https://huggingface.co/ggerganov/whisper.cpp/tree/main)

## Not implemented yet
Spoken text is then processed, looking for a know command in the following format
```
"OK, <COMMAND> <COMMAND_ARGS>, Stop"
```

Known commands
  - "écrit"
   
## Architecture

Built using [System.Reactive](https://www.nuget.org/packages/system.reactive/) pipelines
Usefull (but a bit old) resource about reactive [Tamir Dresher — Reactive Extensions (Rx) 101](https://youtu.be/OAUHDwwGGM0?si=dIsVJyiT7f_6JxwU)
