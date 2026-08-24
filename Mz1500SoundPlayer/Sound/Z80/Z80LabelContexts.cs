using System;

namespace Mz1500SoundPlayer.Sound.Z80;

public class MainRoutineContext : AsmLabel.Context
{
    public AsmLabel Main { get; }
    public AsmLabel Main2 { get; }
    public AsmLabel Loop { get; }
    public AsmLabel Sound { get; }
    public AsmLabel ImageLoader { get; }
    public AsmLabel InitMZ1500Beep { get; }
    public AsmLabel InitMZ1500Beep_End { get; }
    public AsmLabel DataBeepFreqTable { get; }
    public AsmLabel DataPsgFreqTable { get; }
    public AsmLabel GlobalEnvTable { get; }
    public AsmLabel GlobalEnvDataEmpty { get; }
    public AsmLabel GlobalPEnvTable { get; }
    public AsmLabel GlobalPEnvDataEmpty { get; }

    public MainRoutineContext()
    {
        Main = CreateLabel("main:");
        Main2 = CreateLabel("main2:");
        Loop = CreateLabel("loop:");
        Sound = CreateLabel("sound:");
        ImageLoader = CreateLabel("ImageLoader");
        InitMZ1500Beep = CreateLabel("InitMZ1500Beep");
        InitMZ1500Beep_End = CreateLabel("InitMZ1500Beep_End");
        DataBeepFreqTable = CreateLabel("DataBeepFreqTable");
        DataPsgFreqTable = CreateLabel("DataPsgFreqTable");
        GlobalEnvTable = CreateLabel("global_env_table");
        GlobalEnvDataEmpty = CreateLabel("global_env_data_empty");
        GlobalPEnvTable = CreateLabel("global_penv_table");
        GlobalPEnvDataEmpty = CreateLabel("global_penv_data_empty");
    }

    public AsmLabel GetGlobalEnvData(object id) => CreateLabel($"global_env_data_{id}");
    public AsmLabel GetGlobalPEnvData(object id) => CreateLabel($"global_penv_data_{id}");
    public AsmLabel GetPCG(string name) => CreateLabel(name);
}

public class SharedRoutineContext : AsmLabel.Context
{
    private readonly string _prefix;
    public AsmLabel UpdateChannel { get; }
    
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

    public AsmLabel DataSong { get; }
    public AsmLabel DataSongEnd { get; }
    public AsmLabel DataEnvTableBase { get; }
    public AsmLabel DataPEnvTableBase { get; }

    public AsmLabel PrefixEnvOff { get; }
    public AsmLabel PrefixPenvOff { get; }
    public AsmLabel PrefixEnvDataEmpty { get; }
    public AsmLabel PrefixPenvDataEmpty { get; }

    public AsmLabel ReadShortLen { get; }
    public AsmLabel ReadLongLen { get; }
    public AsmLabel DecDurLower { get; }
    public AsmLabel HaltSong { get; }
    public AsmLabel ReadLoopMarker { get; }
    public AsmLabel ReadEnvData { get; }
    public AsmLabel ReadPEnvData { get; }
    public AsmLabel ReadToneData { get; }
    public AsmLabel ReadKyufuData { get; }
    public AsmLabel ReadNoise { get; }
    public AsmLabel ReadSyncNoise { get; }
    public AsmLabel ReadVolumeData { get; }
    public AsmLabel EndSong { get; }
    public AsmLabel ReadSongDataOne { get; }
    public AsmLabel OutputSoundByStatus { get; }
    public AsmLabel OutputEnd { get; }
    public AsmLabel OutputPenvCheck { get; }
    public AsmLabel EnvLoopEnd { get; }
    public AsmLabel EnvEnd { get; }
    public AsmLabel EnvVolMute { get; }
    public AsmLabel EnvVolApply { get; }
    public AsmLabel PenvLoopEnd { get; }
    public AsmLabel PenvEnd { get; }

    public SharedRoutineContext(bool isBeep)
    {
        _prefix = isBeep ? "UpdateBeepChannel" : "UpdatePSGChannel";
        UpdateChannel = CreateLabel(_prefix);

        StatSongDataPosition = CreateLabel($"{_prefix}_StatSongDataPosition");
        StatLoopPosition = CreateLabel($"{_prefix}_StatLoopPosition");
        StatLengthRemain = CreateLabel($"{_prefix}_StatLengthRemain");
        StatLastLength = CreateLabel($"{_prefix}_StatLastLength");
        StatGateRemain = CreateLabel($"{_prefix}_StatGateRemain");
        StatNoteOn = CreateLabel($"{_prefix}_StatNoteOn");
        StatHwVolume = CreateLabel($"{_prefix}_StatHwVolume");
        StatEnvActive = CreateLabel($"{_prefix}_StatEnvActive");
        StatEnvDataPtr = CreateLabel($"{_prefix}_StatEnvDataPtr");
        StatEnvPosOffset = CreateLabel($"{_prefix}_StatEnvPosOffset");
        StatPEnvActive = CreateLabel($"{_prefix}_StatPEnvActive");
        StatPEnvDataPtr = CreateLabel($"{_prefix}_StatPEnvDataPtr");
        StatPEnvPosOffset = CreateLabel($"{_prefix}_StatPEnvPosOffset");

        DataSong = CreateLabel($"{_prefix}_DataSong");
        DataSongEnd = CreateLabel($"{_prefix}_DataSongEnd");
        DataEnvTableBase = CreateLabel($"{_prefix}_DataEnvTableBase");
        DataPEnvTableBase = CreateLabel($"{_prefix}_DataPEnvTableBase");

        PrefixEnvOff = CreateLabel($"{_prefix}_Prefix_env_off");
        PrefixPenvOff = CreateLabel($"{_prefix}_Prefix_penv_off");
        PrefixEnvDataEmpty = CreateLabel($"{_prefix}_Prefix_env_data_empty");
        PrefixPenvDataEmpty = CreateLabel($"{_prefix}_Prefix_penv_data_empty");

        ReadShortLen = CreateLabel($"{_prefix}_ReadShortLen");
        ReadLongLen = CreateLabel($"{_prefix}_ReadLongLen");
        DecDurLower = CreateLabel($"{_prefix}_DecDurLower");
        HaltSong = CreateLabel($"{_prefix}_HaltSong");
        ReadLoopMarker = CreateLabel($"{_prefix}_ReadLoopMarker");
        ReadEnvData = CreateLabel($"{_prefix}_ReadEnvData");
        ReadPEnvData = CreateLabel($"{_prefix}_ReadPEnvData");
        ReadToneData = CreateLabel($"{_prefix}_ReadToneData");
        ReadKyufuData = CreateLabel($"{_prefix}_ReadKyufuData");
        ReadNoise = CreateLabel($"{_prefix}_ReadNoise");
        ReadSyncNoise = CreateLabel($"{_prefix}_ReadSyncNoise");
        ReadVolumeData = CreateLabel($"{_prefix}_ReadVolumeData");
        EndSong = CreateLabel($"{_prefix}_EndSong");
        ReadSongDataOne = CreateLabel($"{_prefix}_ReadSongDataOne");
        OutputSoundByStatus = CreateLabel($"{_prefix}_OutputSoundByStatus");
        OutputEnd = CreateLabel($"{_prefix}_OutputEnd");
        OutputPenvCheck = CreateLabel($"{_prefix}_OutputPenvCheck");
        EnvLoopEnd = CreateLabel($"{_prefix}_EnvLoopEnd");
        EnvEnd = CreateLabel($"{_prefix}_EnvEnd");
        EnvVolMute = CreateLabel($"{_prefix}_EnvVolMute");
        EnvVolApply = CreateLabel($"{_prefix}_EnvVolApply");
        PenvLoopEnd = CreateLabel($"{_prefix}_PenvLoopEnd");
        PenvEnd = CreateLabel($"{_prefix}_PenvEnd");
    }

    public AsmLabel GetEnvData(object id) => CreateLabel($"{_prefix}_Prefix_env_data_{id}");
    public AsmLabel GetPEnvData(object id) => CreateLabel($"{_prefix}_Prefix_penv_data_{id}");
}

public class ChannelDataContext : AsmLabel.Context
{
    public string ChannelName { get; }
    public AsmLabel DataBlock { get; }
    public AsmLabel RepeatStack { get; }
    public AsmLabel RepeatStackPtr { get; }
    public AsmLabel SongData { get; }
    public AsmLabel SongDataEnd { get; }

    public ChannelDataContext(string channelName)
    {
        ChannelName = channelName;
        DataBlock = CreateLabel($"{channelName}_DataBlock");
        RepeatStack = CreateLabel($"{channelName}_RepeatStack");
        RepeatStackPtr = CreateLabel($"{channelName}_RepeatStackPtr");
        SongData = CreateLabel($"{channelName}_SongData");
        SongDataEnd = CreateLabel($"{channelName}_SongDataEnd");
    }
}
