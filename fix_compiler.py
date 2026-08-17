import re

with open('c:/tools/mz1500_sound_driver/Mz1500SoundPlayer/Sound/Z80SequenceCompiler.cs', 'r', encoding='utf-8') as f:
    content = f.read()

# We need to rewrite CompileTrack to output Note ON command
# Let's write a completely new CompileTrack method body.

new_compile_track = '''
    public byte[] CompileTrack(List<NoteEvent> events, byte psgChannel = 0, bool isBeep = false)
    {
        var output = new List<byte>();
        
        int currentVol = -1; // -1 means uninitialized
        int currentEnvId = -1;
        int currentPEnvId = -1;
        double currentTimeMs = 0;
        int currentFrame = 0;
        int currentReleaseEnvPos = -1;
        
        int currentLength = -1;
        Action<int> emitLength = (len) => {
            if (len == currentLength) return;
            if (len >= 1 && len <= 16) {
                output.Add((byte)((int)Z80SequenceCommand.ShortLengthBase + (len - 1)));
            } else {
                output.Add((byte)Z80SequenceCommand.LongLength);
                output.Add((byte)(len & 0xFF));
                output.Add((byte)((len >> 8) & 0xFF));
            }
            currentLength = len;
        };

        foreach (var ev in events)
        {
            if (ev.IsLoopPoint)
            {
                output.Add((byte)Z80SequenceCommand.LoopMarker);
            }

            double nextTimeMs = currentTimeMs + ev.DurationMs;
            int nextFrame = (int)Math.Round(nextTimeMs * 60.0 / 1000.0);
            int totalFrames = nextFrame - currentFrame;
            if (totalFrames < 1) totalFrames = 1;

            double gateEndTimeMs = currentTimeMs + ev.GateTimeMs;
            int gateEndFrame = (int)Math.Round(gateEndTimeMs * 60.0 / 1000.0);
            int gateFrames = gateEndFrame - currentFrame;
            if (gateFrames > totalFrames) gateFrames = totalFrames;
            if (gateFrames < 1 && ev.Frequency > 0) gateFrames = 1;

            if (ev.EnvelopeId >= 0 && ev.EnvelopeId != currentEnvId)
            {
                output.Add((byte)Z80SequenceCommand.SetVoice);
                output.Add((byte)ev.EnvelopeId);
                currentEnvId = ev.EnvelopeId;
            }
            else if (ev.EnvelopeId < 0 && currentEnvId >= 0)
            {
                if (!VolumeEnvelopes.TryGetValue(currentEnvId, out var envDataOff) || envDataOff.ReleaseValues.Count == 0)
                {
                    output.Add((byte)Z80SequenceCommand.SetVoice);
                    output.Add(0xFF);
                    currentEnvId = -1;
                    currentVol = -1;
                }
            }

            if (ev.Frequency == 0 || ev.Volume == 0 || gateFrames <= 0)
            {
                if (currentEnvId >= 0 && VolumeEnvelopes.TryGetValue(currentEnvId, out var relEnvData) && relEnvData.ReleaseValues.Count > 0 && currentReleaseEnvPos >= 0)
                {
                    // Fall back to Rest ˆ— below to continue release phase
                }
                else
                {
                    if (currentVol != 15)
                    {
                        output.Add((byte)Z80SequenceCommand.SetVolume);
                        output.Add(15);
                        currentVol = 15;
                    }
                    currentReleaseEnvPos = -1;
                }

                ushort durationUnits = (ushort)(totalFrames - 1);
                
                if (currentReleaseEnvPos >= 0 && currentEnvId >= 0 && VolumeEnvelopes.TryGetValue(currentEnvId, out var envDataR) && envDataR.ReleaseValues.Count > 0)
                {
                    output.Add((byte)Z80SequenceCommand.SetVoice);
                    output.Add(0xFF);
                    currentEnvId = -1;

                    for (int frm = 0; frm < totalFrames; frm++)
                    {
                        if (currentReleaseEnvPos >= 0 && currentReleaseEnvPos < envDataR.ReleaseValues.Count)
                        {
                            int relVal = envDataR.ReleaseValues[currentReleaseEnvPos++];
                            int relVol15 = relVal;
                            if (relVol15 < 0) relVol15 = 0;
                            if (relVol15 > 15) relVol15 = 15;
                            byte hwVol = (byte)(15 - relVol15);

                            if (currentVol != hwVol)
                            {
                                output.Add((byte)Z80SequenceCommand.SetVolume);
                                output.Add(hwVol);
                                currentVol = hwVol;
                            }
                        }
                        else
                        {
                            if (currentVol != 15)
                            {
                                output.Add((byte)Z80SequenceCommand.SetVolume);
                                output.Add(15);
                                currentVol = 15;
                            }
                            currentReleaseEnvPos = -1;
                        }
                        
                        emitLength(1);
                        output.Add((byte)Z80SequenceCommand.Rest);
                    }
                }
                else
                {
                    emitLength(durationUnits + 1);
                    output.Add((byte)Z80SequenceCommand.Rest);
                }
            }
            else
            {
                currentReleaseEnvPos = 0; 

                int vol15 = (int)Math.Round((ev.Volume / 0.15) * 15.0);
                if (vol15 < 0) vol15 = 0;
                if (vol15 > 15) vol15 = 15;
                byte hwVol = (byte)(15 - vol15);

                if (currentVol != hwVol)
                {
                    output.Add((byte)Z80SequenceCommand.SetVolume);
                    output.Add(hwVol);
                    currentVol = hwVol;
                }

                if (ev.PitchEnvelopeId >= 0 && PitchEnvelopes.ContainsKey(ev.PitchEnvelopeId))
                {
                    // For now, simplify and just set the pitch envelope ID. The hardware pitch envelop will be calculated in Z80.
                    // But wait, the Z80 side will handle pitch envelopes differently? Yes, but for this phase we just output the ID.
                    if (ev.PitchEnvelopeId != currentPEnvId)
                    {
                        output.Add((byte)Z80SequenceCommand.SetHardwareEnvelope);
                        output.Add((byte)ev.PitchEnvelopeId);
                        output.Add(0); // placeholder for sweep/detune data if needed
                        currentPEnvId = ev.PitchEnvelopeId;
                    }
                }
                else if (ev.PitchEnvelopeId < 0 && currentPEnvId >= 0)
                {
                    output.Add((byte)Z80SequenceCommand.SetHardwareEnvelope);
                    output.Add(0xFF);
                    output.Add(0);
                    currentPEnvId = -1;
                }

                if (psgChannel == 3)
                {
                    // Noise
                    emitLength(gateFrames);
                    byte shiftRate = (ev.Frequency < 300) ? (byte)2 : (ev.Frequency < 350) ? (byte)1 : (byte)0;
                    byte feedback = (byte)(ev.NoiseWaveMode & 0x01);
                    byte noiseCmd = (byte)(0xE0 | (feedback << 2) | shiftRate);
                    output.Add((byte)((int)Z80SequenceCommand.NoiseBase + (noiseCmd & 0x0F))); // Just mapping to NOISE base for now
                }
                else
                {
                    // TONE (0x00 - 0x5F)
                    emitLength(gateFrames);
                    int noteNum = ev.NoteNumber;
                    if (noteNum < 0) noteNum = 0;
                    if (noteNum > 95) noteNum = 95;
                    output.Add((byte)((int)Z80SequenceCommand.ToneBase + noteNum));
                }

                int restFrames = totalFrames - gateFrames;
                if (restFrames > 0)
                {
                    if (currentReleaseEnvPos >= 0 && currentEnvId >= 0 && VolumeEnvelopes.TryGetValue(currentEnvId, out var envDataR) && envDataR.ReleaseValues.Count > 0)
                    {
                        output.Add((byte)Z80SequenceCommand.SetVoice);
                        output.Add(0xFF);
                        int activeEnvId = currentEnvId;
                        currentEnvId = -1;

                        for (int frm = 0; frm < restFrames; frm++)
                        {
                            if (currentReleaseEnvPos >= 0 && currentReleaseEnvPos < envDataR.ReleaseValues.Count)
                            {
                                int relVal = envDataR.ReleaseValues[currentReleaseEnvPos++];
                                int relVol15 = relVal;
                                if (relVol15 < 0) relVol15 = 0;
                                if (relVol15 > 15) relVol15 = 15;
                                byte relHwVol = (byte)(15 - relVol15);

                                if (currentVol != relHwVol)
                                {
                                    output.Add((byte)Z80SequenceCommand.SetVolume);
                                    output.Add(relHwVol);
                                    currentVol = relHwVol;
                                }
                            }
                            else
                            {
                                if (currentVol != 15)
                                {
                                    output.Add((byte)Z80SequenceCommand.SetVolume);
                                    output.Add(15);
                                    currentVol = 15;
                                }
                                currentReleaseEnvPos = -1;
                            }
                            
                            emitLength(1);
                            output.Add((byte)Z80SequenceCommand.Rest);
                        }
                    }
                    else
                    {
                        if (currentVol != 15) {
                            output.Add((byte)Z80SequenceCommand.SetVolume);
                            output.Add(15);
                            currentVol = 15;
                        }
                        
                        emitLength(restFrames);
                        output.Add((byte)Z80SequenceCommand.Rest);
                    }
                }
            }

            currentTimeMs = nextTimeMs;
            currentFrame = nextFrame;
        }
        
        output.Add((byte)Z80SequenceCommand.TrackEnd);

        return output.ToArray();
    }
'''

content = re.sub(r'public byte\[\] CompileTrack\(List<NoteEvent> events, byte psgChannel = 0, bool isBeep = false\)[\s\S]*?return output\.ToArray\(\);\s*\}', new_compile_track.strip(), content)

with open('c:/tools/mz1500_sound_driver/Mz1500SoundPlayer/Sound/Z80SequenceCompiler.cs', 'w', encoding='utf-8') as f:
    f.write(content)
