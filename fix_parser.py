import re

file_path = r"c:\tools\mz1500_sound_driver\Mz1500SoundPlayer\Sound\Z80\MZ1500SoundDriverGenerator.cs"
with open(file_path, "r", encoding="utf-8") as f:
    content = f.read()

# 1. Fix ReadEnvData
# It should lookup the address in DataEnvTableBase using the EnvId
target_env = r"""        // -- Read Envelope Command --
        asm.Label(GetSharedCtx(isBeep).ReadEnvData);
        asm.LD(asm.A, asm.DEref); // EnvelopeId (or 0xFF for Off)
        asm.INC(asm.DE);
        
        // Save pos
        asm.LD(asm.IXref(IxStatSongDataPosition), asm.E);
        asm.LD(asm.IXref((sbyte)(IxStatSongDataPosition + 1)), asm.D);

        // Check if Off (0xFF)
        asm.LD(asm.B, asm.A);
        asm.LD(asm.A, 0xFF);
        asm.CP(asm.B);
        asm.JP(asm.Z, asm.LabelRef(GetSharedCtx(isBeep).PrefixEnvOff));
        asm.LD(asm.IXref(IxStatEnvDataPtr), asm.E);
        asm.LD(asm.IXref((sbyte)(IxStatEnvDataPtr + 1)), asm.D);

        asm.JP(asm.LabelRef(GetSharedCtx(isBeep).ReadSongDataOne));"""

replacement_env = r"""        // -- Read Envelope Command --
        asm.Label(GetSharedCtx(isBeep).ReadEnvData);
        asm.LD(asm.A, asm.DEref); // EnvelopeId (or 0xFF for Off)
        asm.INC(asm.DE);
        
        // Save pos
        asm.LD(asm.IXref(IxStatSongDataPosition), asm.E);
        asm.LD(asm.IXref((sbyte)(IxStatSongDataPosition + 1)), asm.D);

        // Check if Off (0xFF)
        asm.LD(asm.B, asm.A); // Save ID to B
        asm.LD(asm.A, 0xFF);
        asm.CP(asm.B);
        asm.JP(asm.Z, asm.LabelRef(GetSharedCtx(isBeep).PrefixEnvOff));
        
        // Lookup pointer from table
        asm.LD(asm.HL, asm.LabelRef(GetSharedCtx(isBeep).DataEnvTableBase));
        asm.LD(asm.A, asm.B); // Restore ID
        asm.ADD(asm.A, asm.A); // ID * 2
        asm.LD(asm.C, asm.A);
        asm.LD(asm.B, 0);
        asm.ADD(asm.HL, asm.BC);
        
        // Read pointer into DE
        asm.LD(asm.E, asm.HLref);
        asm.INC(asm.HL);
        asm.LD(asm.D, asm.HLref);

        asm.LD(asm.IXref(IxStatEnvDataPtr), asm.E);
        asm.LD(asm.IXref((sbyte)(IxStatEnvDataPtr + 1)), asm.D);
        asm.LD(asm.IXref(IxStatEnvActive), asm.Value((byte)0x01)); // Active

        asm.JP(asm.LabelRef(GetSharedCtx(isBeep).ReadSongDataOne));"""

content = content.replace(target_env, replacement_env)

# 2. Fix ReadPEnvData
target_penv = r"""        // -- Read Pitch Envelope Command --
        asm.Label(GetSharedCtx(isBeep).ReadPEnvData);
        asm.LD(asm.A, asm.DEref); // EnvId (or 0xFF for Off)
        asm.INC(asm.DE);
        
        // Save pos
        asm.LD(asm.IXref(IxStatSongDataPosition), asm.E);
        asm.LD(asm.IXref((sbyte)(IxStatSongDataPosition + 1)), asm.D);

        // Check if Off (0xFF)
        asm.LD(asm.B, asm.A);
        asm.LD(asm.A, 0xFF);
        asm.CP(asm.B);
        asm.JP(asm.Z, asm.LabelRef(GetSharedCtx(isBeep).PrefixPenvOff));
        asm.LD(asm.IXref(IxStatPEnvDataPtr), asm.E);
        asm.LD(asm.IXref((sbyte)(IxStatPEnvDataPtr + 1)), asm.D);

        asm.JP(asm.LabelRef(GetSharedCtx(isBeep).ReadSongDataOne));"""

replacement_penv = r"""        // -- Read Pitch Envelope Command --
        asm.Label(GetSharedCtx(isBeep).ReadPEnvData);
        asm.LD(asm.A, asm.DEref); // EnvId (or 0xFF for Off)
        asm.INC(asm.DE);
        
        // Save pos
        asm.LD(asm.IXref(IxStatSongDataPosition), asm.E);
        asm.LD(asm.IXref((sbyte)(IxStatSongDataPosition + 1)), asm.D);

        // Check if Off (0xFF)
        asm.LD(asm.B, asm.A);
        asm.LD(asm.A, 0xFF);
        asm.CP(asm.B);
        asm.JP(asm.Z, asm.LabelRef(GetSharedCtx(isBeep).PrefixPenvOff));
        
        // Lookup pointer from table
        asm.LD(asm.HL, asm.LabelRef(GetSharedCtx(isBeep).DataPEnvTableBase));
        asm.LD(asm.A, asm.B);
        asm.ADD(asm.A, asm.A); // ID * 2
        asm.LD(asm.C, asm.A);
        asm.LD(asm.B, 0);
        asm.ADD(asm.HL, asm.BC);
        
        asm.LD(asm.E, asm.HLref);
        asm.INC(asm.HL);
        asm.LD(asm.D, asm.HLref);

        asm.LD(asm.IXref(IxStatPEnvDataPtr), asm.E);
        asm.LD(asm.IXref((sbyte)(IxStatPEnvDataPtr + 1)), asm.D);
        asm.LD(asm.IXref(IxStatPEnvActive), asm.Value((byte)0x01)); // Active

        asm.JP(asm.LabelRef(GetSharedCtx(isBeep).ReadSongDataOne));"""

content = content.replace(target_penv, replacement_penv)


# 3. Fix Tone data overwriting HL, and jumping to ReadSongDataOne instead of UpdateChannel
target_tone = r"""        asm.LD(asm.A, asm.HLref); // low byte
        
        if (isBeep) {
            // Write to 0xE004 twice
            asm.LD(asm.HL, (ushort)0xE004);
            asm.LD(asm.HLref, asm.A);
            
            asm.LD(asm.A, asm.HLref); // high byte
            asm.LD(asm.HLref, asm.A);
        } else {
            // Load PSG Channel bits from IX
            asm.LD(asm.B, asm.A); // Save low byte to B
            asm.LD(asm.A, asm.IXref(IxPsgChannelBits)); // 1000 0000 etc
            asm.OR(asm.B);
            asm.LD(asm.C, asm.IXref(IxPortType));
            asm.OUT(asm.C, asm.A);
            
            asm.INC(asm.HL);
            asm.LD(asm.A, asm.HLref); // high byte
            asm.LD(asm.B, asm.A);
            asm.LD(asm.A, asm.IXref(IxPsgChannelBits));
            asm.OR((byte)0x0F); // 0000 1111 (register selection?) wait: 0x0F is for high byte? No, it's to zero the data bits?
            asm.AND((byte)0xF0); // 1000 0000 (channel) + 0000 0000 (0)
            asm.OR(asm.B);
            asm.OUT(asm.C, asm.A);
        }

        // Save pos & Read Next
        asm.LD(asm.IXref(IxStatSongDataPosition), asm.E);
        asm.LD(asm.IXref((sbyte)(IxStatSongDataPosition + 1)), asm.D);
        asm.JP(asm.LabelRef(GetSharedCtx(isBeep).ReadSongDataOne));"""

replacement_tone = r"""        asm.LD(asm.A, asm.HLref); // low byte
        
        if (isBeep) {
            // Write to 0xE004 twice
            asm.PUSH(asm.HL);
            asm.LD(asm.HL, (ushort)0xE004);
            asm.LD(asm.HLref, asm.A);
            asm.POP(asm.HL);
            
            asm.INC(asm.HL);
            asm.LD(asm.A, asm.HLref); // high byte
            
            asm.PUSH(asm.HL);
            asm.LD(asm.HL, (ushort)0xE004);
            asm.LD(asm.HLref, asm.A);
            asm.POP(asm.HL);
        } else {
            // Load PSG Channel bits from IX
            asm.LD(asm.B, asm.A); // Save low byte to B
            asm.LD(asm.A, asm.IXref(IxPsgChannelBits)); // 1000 0000 etc
            asm.OR(asm.B);
            asm.LD(asm.C, asm.IXref(IxPortType));
            asm.OUT(asm.C, asm.A);
            
            asm.INC(asm.HL);
            asm.LD(asm.A, asm.HLref); // high byte
            asm.LD(asm.B, asm.A);
            asm.LD(asm.A, asm.IXref(IxPsgChannelBits));
            asm.AND((byte)0xF0);
            asm.OR(asm.B);
            asm.OUT(asm.C, asm.A);
        }

        // Save pos & Yield to Channel Loop
        asm.LD(asm.IXref(IxStatSongDataPosition), asm.E);
        asm.LD(asm.IXref((sbyte)(IxStatSongDataPosition + 1)), asm.D);
        asm.JP(asm.LabelRef(GetSharedCtx(isBeep).UpdateChannel));"""

content = content.replace(target_tone, replacement_tone)

with open(file_path, "w", encoding="utf-8") as f:
    f.write(content)

print("Fixed parsing bugs")
