using System;

namespace Mz1500SoundPlayer.Sound.Z80;

public class ChannelContext
{
    public string Prefix { get; }
    public AsmLabel PrefixLabel { get; }
    
    // Status Variables
    public AsmLabel StatSongDataPosition { get; }
    public AsmLabel StatLoopPosition { get; }
    public AsmLabel StatLengthRemain { get; }
    public AsmLabel StatLastLength { get; }
    public AsmLabel StatGateRemain { get; }
    public AsmLabel StatNoteOn { get; }
    public AsmLabel StatHwVolume { get; }
    public AsmLabel StatEnvActive { get; }
    public AsmLabel StatEnvDataPtr { get; }
    public AsmLabel StatEnvPosOffset { get; }
    public AsmLabel StatPEnvActive { get; }
    public AsmLabel StatPEnvDataPtr { get; }
    public AsmLabel StatPEnvPosOffset { get; }

    // Routines & Data
    public AsmLabel OutputSoundByStatus { get; }
    public AsmLabel ReadSongDataOne { get; }
    public AsmLabel ReadToneData { get; }
    public AsmLabel ReadVolumeData { get; }
    public AsmLabel ReadEnvData { get; }
    public AsmLabel ReadPEnvData { get; }
    public AsmLabel ReadKyufuData { get; }
    public AsmLabel DataSong { get; }
    public AsmLabel DataSongEnd { get; }
    public AsmLabel DataEnvTableBase { get; }
    public AsmLabel DataPEnvTableBase { get; }
    
    // Internal labels for specific flows
    public AsmLabel DecDurLower { get; }
    public AsmLabel EndSong { get; }
    public AsmLabel ReadLongLen { get; }
    public AsmLabel ReadShortLen { get; }
    public AsmLabel ReadLoopMarker { get; }
    public AsmLabel ReadNoise { get; }
    public AsmLabel ReadSyncNoise { get; }
    
    public AsmLabel EnvLoopEnd { get; }
    public AsmLabel EnvEnd { get; }
    public AsmLabel EnvVolMute { get; }
    public AsmLabel EnvVolApply { get; }
    public AsmLabel OutputPenvCheck { get; }
    public AsmLabel PenvLoopEnd { get; }
    public AsmLabel PenvEnd { get; }
    public AsmLabel OutputEnd { get; }

    public ChannelContext(string prefix)
    {
        Prefix = prefix;
        PrefixLabel = new AsmLabel(prefix);
        
        StatSongDataPosition = new AsmLabel($"{prefix}_StatSongDataPosition");
        StatLoopPosition = new AsmLabel($"{prefix}_StatLoopPosition");
        StatLengthRemain = new AsmLabel($"{prefix}_StatLengthRemain");
        StatLastLength = new AsmLabel($"{prefix}_StatLastLength");
        StatGateRemain = new AsmLabel($"{prefix}_StatGateRemain");
        StatNoteOn = new AsmLabel($"{prefix}_StatNoteOn");
        StatHwVolume = new AsmLabel($"{prefix}_StatHwVolume");
        StatEnvActive = new AsmLabel($"{prefix}_StatEnvActive");
        StatEnvDataPtr = new AsmLabel($"{prefix}_StatEnvDataPtr");
        StatEnvPosOffset = new AsmLabel($"{prefix}_StatEnvPosOffset");
        StatPEnvActive = new AsmLabel($"{prefix}_StatPEnvActive");
        StatPEnvDataPtr = new AsmLabel($"{prefix}_StatPEnvDataPtr");
        StatPEnvPosOffset = new AsmLabel($"{prefix}_StatPEnvPosOffset");

        OutputSoundByStatus = new AsmLabel($"{prefix}_OutputSoundByStatus");
        ReadSongDataOne = new AsmLabel($"{prefix}_ReadSongDataOne");
        ReadToneData = new AsmLabel($"{prefix}_ReadToneData");
        ReadVolumeData = new AsmLabel($"{prefix}_ReadVolumeData");
        ReadEnvData = new AsmLabel($"{prefix}_ReadEnvData");
        ReadPEnvData = new AsmLabel($"{prefix}_ReadPEnvData");
        ReadKyufuData = new AsmLabel($"{prefix}_ReadKyufuData");
        DataSong = new AsmLabel($"{prefix}_DataSong");
        DataSongEnd = new AsmLabel($"{prefix}_data_song_end");
        DataEnvTableBase = new AsmLabel($"{prefix}_DataEnvTableBase");
        DataPEnvTableBase = new AsmLabel($"{prefix}_DataPEnvTableBase");

        DecDurLower = new AsmLabel($"{prefix}_dec_dur_lower");
        EndSong = new AsmLabel($"{prefix}_end_song");
        ReadLongLen = new AsmLabel($"{prefix}_read_long_len");
        ReadShortLen = new AsmLabel($"{prefix}_read_short_len");
        ReadLoopMarker = new AsmLabel($"{prefix}_read_loop_marker");
        ReadNoise = new AsmLabel($"{prefix}_read_noise");
        ReadSyncNoise = new AsmLabel($"{prefix}_read_sync_noise");
        
        EnvLoopEnd = new AsmLabel($"{prefix}_env_loop_end");
        EnvEnd = new AsmLabel($"{prefix}_env_end");
        EnvVolMute = new AsmLabel($"{prefix}_env_vol_mute");
        EnvVolApply = new AsmLabel($"{prefix}_env_vol_apply");
        OutputPenvCheck = new AsmLabel($"{prefix}_output_penv_check");
        PenvLoopEnd = new AsmLabel($"{prefix}_penv_loop_end");
        PenvEnd = new AsmLabel($"{prefix}_penv_end");
        OutputEnd = new AsmLabel($"{prefix}_output_end");
    }
}
