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
    private System.Threading.CancellationTokenSource? _cancellationTokenSource;

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
        await PlayEventDictAsync(dict, null!);
    }

    public async Task<string> PlayMmlAsync(string mmlString)
    {
        var parser = new MultiTrackMmlParser();
        var mmlData = parser.Parse(mmlString);
        var tracks = mmlData.Tracks;

        var expander = new TrackEventExpander();
        var trackEvents = new Dictionary<string, List<NoteEvent>>();

        double maxMs = 0;
        var log = new System.Text.StringBuilder();
        log.AppendLine($"[Parser] Found {tracks.Count} tracks.");
        log.AppendLine($"[Parser] Found {mmlData.VolumeEnvelopes.Count} volume envelopes.");

        foreach (var kvp in tracks)
        {
            var events = expander.Expand(kvp.Value);
            trackEvents[kvp.Key] = events;

            double totalMs = 0;
            foreach (var e in events) totalMs += e.DurationMs;
            if (totalMs > maxMs) maxMs = totalMs;

            log.AppendLine($"- Track '{kvp.Key}': {events.Count} events, duration {totalMs:F1}ms");
        }

        await PlayEventDictAsync(trackEvents, mmlData.VolumeEnvelopes, maxMs);
        return log.ToString();
    }

    public string ExportQdc(string mmlString, string filePath)
    {
        var parser = new MultiTrackMmlParser();
        var mmlData = parser.Parse(mmlString);
        var tracks = mmlData.Tracks;

        var expander = new TrackEventExpander();
        var compiler = new MmlToZ80Compiler();
        
        var musicAssembler = new Z80.MZ1500MusicAssembler();
        musicAssembler.VolumeEnvelopes = mmlData.VolumeEnvelopes; // Z80ドライバにエンベロープ辞書を渡す
        
        int trackIndex = 0;
        // 各トラックごとにMML展開 -> Z80コマンドコンパイル -> Channelオブジェクトとしてアセンブラに登録
        foreach (var kvp in tracks)
        {
            // トラック番号からPSGのIC(左右)とチャンネル(0〜2)を割り当てる
            // 0,1,2 : 左(0xF2) の ch 0,1,2
            // 3,4,5 : 右(0xF3) の ch 0,1,2
            byte psgChannel = (byte)(trackIndex % 3);
            byte ioPort = (byte)(trackIndex < 3 ? 0xF2 : 0xF3);

            var events = expander.Expand(kvp.Value);
            byte[] seqBin = compiler.CompileTrack(events, psgChannel);
            
            musicAssembler.AppendChannel(new Z80.Channel("track_" + kvp.Key, ioPort, seqBin));
            trackIndex++;
        }

        // Z80の再生ドライバ一式を含む完全なバイナリプログラムをビルド
        byte[] z80Bin = musicAssembler.Build();

        var qdcBuilder = new QdcImageBuilder();
        byte[] qdcData = qdcBuilder.BuildStandardExecutable("MZTUNE", z80Bin);

        System.IO.File.WriteAllBytes(filePath, qdcData);

        var log = new System.Text.StringBuilder();
        log.AppendLine($"[Export] QDC file generated at {filePath}");
        log.AppendLine($"- Compiled {tracks.Count} sound tracks.");
        log.AppendLine($"- Z80 Executable built: {z80Bin.Length} bytes");
        log.AppendLine($"- Total QDC image size: {qdcData.Length} bytes");
        return log.ToString();
    }

    private async Task PlayEventDictAsync(Dictionary<string, List<NoteEvent>> trackEvents, Dictionary<int, List<int>> envelopes = null!, double totalMs = 3000)
    {
        Stop(); // 前の再生を安全に停止

        _cancellationTokenSource = new System.Threading.CancellationTokenSource();
        var token = _cancellationTokenSource.Token;

        if (envelopes == null) envelopes = new Dictionary<int, List<int>>();
        _multiSequenceProvider = new MultiTrackSequenceProvider(trackEvents, envelopes);
        
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

        // 一番長いトラックに合わせて待機。キャンセル時は例外をキャッチして抜ける
        try
        {
            await Task.Delay((int)totalMs + 100, token); 
        }
        catch (TaskCanceledException)
        {
            // Stop()が呼ばれてキャンセルされた正常フロー
        }
        
        Stop();
    }

    public void Stop()
    {
        if (_cancellationTokenSource != null)
        {
            if (!_cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel();
            }
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
        }

        if (_waveOut != null)
        {
            _waveOut.Stop();
            _waveOut.Dispose();
            _waveOut = null;
        }
        _playbackCompletion?.TrySetResult(true);
    }
}
