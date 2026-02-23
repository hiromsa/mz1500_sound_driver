using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NAudio.Wave;

namespace Mz1500SoundPlayer.Sound;

public class MmlPlayerModel
{
    private IWavePlayer? _waveOut;
    private MultiTrackSequenceProvider? _multiSequenceProvider;
    private TaskCompletionSource<bool>? _playbackCompletion;

    public MmlPlayerModel()
    {
    }

    public async Task PlaySequenceAsync()
    {
        var demo = new List<NoteEvent>
        {
            new NoteEvent(261.63, 500, 0.2, 500),
            new NoteEvent(0, 50, 0, 0),
            new NoteEvent(293.66, 500, 0.2, 500),
            new NoteEvent(0, 50, 0, 0),
            new NoteEvent(329.63, 500, 0.2, 500)
        };
        var dict = new Dictionary<string, List<NoteEvent>> { { "A", demo } };
        await PlayEventDictAsync(dict);
    }

    public async Task<string> PlayMmlAsync(string mmlString)
    {
        var parser = new MultiTrackMmlParser();
        var tracks = parser.Parse(mmlString);

        var expander = new TrackEventExpander();
        var trackEvents = new Dictionary<string, List<NoteEvent>>();

        double maxMs = 0;
        var log = new System.Text.StringBuilder();
        log.AppendLine($"[Parser] Found {tracks.Count} tracks.");

        foreach (var kvp in tracks)
        {
            var events = expander.Expand(kvp.Value);
            trackEvents[kvp.Key] = events;

            double totalMs = 0;
            foreach (var e in events) totalMs += e.DurationMs;
            if (totalMs > maxMs) maxMs = totalMs;

            log.AppendLine($"- Track '{kvp.Key}': {events.Count} events, duration {totalMs:F1}ms");
        }

        await PlayEventDictAsync(trackEvents, maxMs);
        return log.ToString();
    }

    private async Task PlayEventDictAsync(Dictionary<string, List<NoteEvent>> trackEvents, double totalMs = 3000)
    {
        Stop(); // 前の再生を安全に停止

        _multiSequenceProvider = new MultiTrackSequenceProvider(trackEvents);
        
        // Windows環境でMONO 1chのままストリーミングするとドライバによってループ(スタッターエコー)するバグを防ぐため、常にStereoに拡張する
        var stereoProvider = new NAudio.Wave.SampleProviders.MonoToStereoSampleProvider(_multiSequenceProvider);

        // レガシーなWaveOutEventではなく、安定したWasapiOutを利用する
        _waveOut = new WasapiOut(NAudio.CoreAudioApi.AudioClientShareMode.Shared, 50);
        _waveOut.Init(stereoProvider);

        _playbackCompletion = new TaskCompletionSource<bool>();
        _waveOut.PlaybackStopped += (s, e) => 
        {
            _playbackCompletion?.TrySetResult(true);
        };

        _waveOut.Play();

        // 一番長いトラックに合わせて待機
        await Task.Delay((int)totalMs + 100); 
        Stop();
    }

    public void Stop()
    {
        if (_waveOut != null)
        {
            _waveOut.Stop();
            _waveOut.Dispose();
            _waveOut = null;
        }
        _playbackCompletion?.TrySetResult(true);
    }
}
