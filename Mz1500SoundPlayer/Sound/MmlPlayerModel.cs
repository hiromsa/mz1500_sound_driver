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
    private NAudio.Wave.SampleProviders.VolumeSampleProvider? _volumeProvider;

    private HashSet<string> _activeChannels = new HashSet<string>(new[] { "A", "B", "C", "D", "E", "F", "G", "H", "P" });
    public HashSet<string> ActiveChannels
    {
        get => _activeChannels;
        set
        {
            _activeChannels = value;
            if (_multiSequenceProvider != null)
            {
                _multiSequenceProvider.ActiveChannels = _activeChannels;
            }
        }
    }

    private float _masterVolume = 0.5f;
    public float MasterVolume 
    { 
        get => _masterVolume; 
        set 
        {
            _masterVolume = value;
            if (_volumeProvider != null)
            {
                _volumeProvider.Volume = _masterVolume;
            }
        }
    }

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
        var compiler = new MmlToZ80Compiler();
        var bin = compiler.CompileTrack(demo, 0);
        var dict = new Dictionary<string, byte[]> { { "A", bin } };
        await PlayBytecodeDictAsync(dict, null!, null!);
    }

    public async Task<string> PlayMmlAsync(string mmlString)
    {
        var parser = new MultiTrackMmlParser();
        var mmlData = parser.Parse(mmlString);
        var tracks = mmlData.Tracks;

        var expander = new TrackEventExpander();
        var compiler = new MmlToZ80Compiler();
        compiler.PitchEnvelopes = mmlData.PitchEnvelopes;
        var trackBinaries = new Dictionary<string, byte[]>();

        double maxMs = 0;
        bool hasInfiniteLoop = false;
        var log = new System.Text.StringBuilder();
        log.AppendLine($"[Parser] Found {tracks.Count} tracks.");
        log.AppendLine($"[Parser] Found {mmlData.VolumeEnvelopes.Count} volume envelopes.");
        log.AppendLine($"[Parser] Found {mmlData.PitchEnvelopes.Count} pitch envelopes.");

        foreach (var kvp in tracks)
        {
            var events = expander.Expand(kvp.Value);
            if (System.Linq.Enumerable.Any(events, e => e.IsLoopPoint)) hasInfiniteLoop = true;
            
            byte psgChannel = 0;
            switch (kvp.Key.ToUpperInvariant())
            {
                case "A": case "E": psgChannel = 0; break;
                case "B": case "F": psgChannel = 1; break;
                case "C": case "G": psgChannel = 2; break;
                case "D": case "H": psgChannel = 3; break; // Noise
                case "P":           psgChannel = 0; break; // BEEP
                default: psgChannel = 0; break;
            }
            bool isBeep = kvp.Key.ToUpperInvariant() == "P";
            byte[] seqBin = compiler.CompileTrack(events, psgChannel, isBeep);
            trackBinaries[kvp.Key] = seqBin;

            double totalMs = 0;
            foreach (var e in events) totalMs += e.DurationMs;
            if (totalMs > maxMs) maxMs = totalMs;

            log.AppendLine($"- Track '{kvp.Key}': {events.Count} events, compiled size {seqBin.Length} bytes, duration {totalMs:F1}ms");
        }

        await PlayBytecodeDictAsync(trackBinaries, mmlData.VolumeEnvelopes, compiler.HwPitchEnvelopes, maxMs, hasInfiniteLoop);
        return log.ToString();
    }

    public string ExportQdc(string mmlString, string filePath)
    {
        var parser = new MultiTrackMmlParser();
        var mmlData = parser.Parse(mmlString);
        var tracks = mmlData.Tracks;

        var expander = new TrackEventExpander();
        var compiler = new MmlToZ80Compiler();
        compiler.PitchEnvelopes = mmlData.PitchEnvelopes;
        
        var musicAssembler = new Z80.MZ1500MusicAssembler();
        musicAssembler.VolumeEnvelopes = mmlData.VolumeEnvelopes; // Z80ドライバにエンベロープ辞書を渡す
        
        // 各トラックごとにMML展開 -> Z80コマンドコンパイル -> Channelオブジェクトとしてアセンブラに登録
        foreach (var kvp in tracks)
        {
            // トラック番号からPSGのIC(左右)とチャンネル(0〜2)を割り当てる
            // 0,1,2 : 左(0xF2) の ch 0,1,2 (Track A, B, C)
            // 3,4,5 : 右(0xF3) の ch 0,1,2 (Track E, F, G)
            byte psgChannel = 0;
            byte ioPort = 0xF2;
            switch (kvp.Key.ToUpperInvariant())
            {
                case "A": psgChannel = 0; ioPort = 0xF2; break;
                case "B": psgChannel = 1; ioPort = 0xF2; break;
                case "C": psgChannel = 2; ioPort = 0xF2; break;
                case "D": psgChannel = 3; ioPort = 0xF2; break; // PSG1 Noise
                case "E": psgChannel = 0; ioPort = 0xF3; break;
                case "F": psgChannel = 1; ioPort = 0xF3; break;
                case "G": psgChannel = 2; ioPort = 0xF3; break;
                case "H": psgChannel = 3; ioPort = 0xF3; break; // PSG2 Noise
                case "P": psgChannel = 0; ioPort = 0xE0; break; // BEEP
                default:  psgChannel = 0; ioPort = 0xF2; break;
            }

            var events = expander.Expand(kvp.Value);
            bool isBeep = kvp.Key.ToUpperInvariant() == "P";
            byte[] seqBin = compiler.CompileTrack(events, psgChannel, isBeep);
            
            musicAssembler.AppendChannel(new Z80.Channel("track_" + kvp.Key, ioPort, seqBin));
        }

        musicAssembler.HwPitchEnvelopes = compiler.HwPitchEnvelopes; // Z80ドライバにハードウェアピッチエンベロープを渡す

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

    private async Task PlayBytecodeDictAsync(Dictionary<string, byte[]> trackBinaries, Dictionary<int, EnvelopeData> volumeEnvelopes = null!, List<MmlToZ80Compiler.HwPitchEnvData> hwPitchEnvelopes = null!, double totalMs = 3000, bool hasInfiniteLoop = false)
    {
        Stop(); // 前の再生を安全に停止

        _cancellationTokenSource = new System.Threading.CancellationTokenSource();
        var token = _cancellationTokenSource.Token;

        if (volumeEnvelopes == null) volumeEnvelopes = new Dictionary<int, EnvelopeData>();
        if (hwPitchEnvelopes == null) hwPitchEnvelopes = new List<MmlToZ80Compiler.HwPitchEnvData>();
        
        _multiSequenceProvider = new MultiTrackSequenceProvider(trackBinaries, volumeEnvelopes, hwPitchEnvelopes);
        _multiSequenceProvider.ActiveChannels = _activeChannels;
        
        // Windows環境でMONO 1chのままストリーミングするとドライバによってループ(スタッターエコー)するバグを防ぐため、常にStereoに拡張する
        var stereoProvider = new NAudio.Wave.SampleProviders.MonoToStereoSampleProvider(_multiSequenceProvider);

        // マスターボリュームの適用
        _volumeProvider = new NAudio.Wave.SampleProviders.VolumeSampleProvider(stereoProvider)
        {
            Volume = MasterVolume
        };

        // レガシーなWaveOutEventではなく、安定したWasapiOutを利用する
        _waveOut = new WasapiOut(NAudio.CoreAudioApi.AudioClientShareMode.Shared, 50);
        _waveOut.Init(_volumeProvider);

        _playbackCompletion = new TaskCompletionSource<bool>();
        _waveOut.PlaybackStopped += (s, e) => 
        {
            _playbackCompletion?.TrySetResult(true);
        };

        _waveOut.Play();

        // 一番長いトラックに合わせて待機。キャンセル時は例外をキャッチして抜ける
        try
        {
            if (hasInfiniteLoop)
            {
                await Task.Delay(System.Threading.Timeout.Infinite, token);
            }
            else
            {
                await Task.Delay((int)totalMs + 100, token); 
            }
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
