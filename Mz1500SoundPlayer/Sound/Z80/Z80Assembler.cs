using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Mz1500SoundPlayer.Sound.Z80;

public class MnemonicByteData
{
    public byte[] ByteData { get; }
    public string Description { get; }
    public MnemonicByteData(byte[] byteData, string description)
    {
        ByteData = byteData;
        Description = description;
    }
}

public class MnemonicByteDataMap
{
    private readonly Dictionary<MnemonicKey, MnemonicByteData> _map = new();

    public void Add(Z80Part[] parts, byte[] bytes)
    {
        var key = new MnemonicKey(parts);
        var descBuilder = new List<string>();
        foreach (var p in parts)
        {
            if (p is OpCodePart) descBuilder.Insert(0, p.GetInfo());
            else descBuilder.Add(p.GetInfo());
        }
        string desc = string.Join(" ", descBuilder);

        // Custom Equals/HashCode in MnemonicKey doesn't strictly guarantee Dictionary match 
        // without overriding GetHashCode flawlessly for arrays, so we use a simpler key matching in TryGet
        _map[key] = new MnemonicByteData(bytes, desc);
    }

    public bool TryGet(MnemonicKey key, out MnemonicByteData? data)
    {
        var match = _map.FirstOrDefault(kv => kv.Key.Equals(key));
        if (match.Key != null)
        {
            data = match.Value;
            return true;
        }
        data = null;
        return false;
    }
}

public class Z80Assembler
{
    private readonly List<AssemblerData> _dataList = new();
    private readonly MnemonicByteDataMap _mnemonicMap = new();
    private ushort _startAddress = 0;

    // Registers
    public Register A { get; } = new("A");
    public Register B { get; } = new("B");
    public Register C { get; } = new("C");
    public Register D { get; } = new("D");
    public Register E { get; } = new("E");
    public Register H { get; } = new("H");
    public Register L { get; } = new("L");
    public Register BC { get; } = new("BC");
    public Register DE { get; } = new("DE");
    public Register HL { get; } = new("HL");
    public Register AF { get; } = new("AF");
    public Register SP { get; } = new("SP");
    public Register IX { get; } = new("IX");
    public Register IY { get; } = new("IY");
    public Register Z { get; } = new("Z");
    public Register NZ { get; } = new("NZ");
    
    // Register Refs
    public Register DEref { get; } = new("(DE)");
    public Register HLref { get; } = new("(HL)");

    // Opcodes
    public OpCodePart OpCodeADD { get; } = new("ADD");
    public OpCodePart OpCodeADC { get; } = new("ADC");
    public OpCodePart OpCodeAND { get; } = new("AND");
    public OpCodePart OpCodeCALL { get; } = new("CALL");
    public OpCodePart OpCodeCP { get; } = new("CP");
    public OpCodePart OpCodeDEC { get; } = new("DEC");
    public OpCodePart OpCodeDI { get; } = new("DI");
    public OpCodePart OpCodeEI { get; } = new("EI");
    public OpCodePart OpCodeIM_1 { get; } = new("IM 1");
    public OpCodePart OpCodeINC { get; } = new("INC");
    public OpCodePart OpCodeJP { get; } = new("JP");
    public OpCodePart OpCodeLD { get; } = new("LD");
    public OpCodePart OpCodeLDIR { get; } = new("LDIR");
    public OpCodePart OpCodeOR { get; } = new("OR");
    public OpCodePart OpCodeOUT { get; } = new("OUT");
    public OpCodePart OpCodePOP { get; } = new("POP");
    public OpCodePart OpCodePUSH { get; } = new("PUSH");
    public OpCodePart OpCodeRET { get; } = new("RET");
    public OpCodePart OpCodeSBC { get; } = new("SBC");
    public OpCodePart OpCodeSLA { get; } = new("SLA");
    public OpCodePart OpCodeSRL { get; } = new("SRL");
    public OpCodePart OpCodeSUB { get; } = new("SUB");
    public OpCodePart OpCodeXOR { get; } = new("XOR");

        public OpCodePart OpCodeJR { get; } = new("JR");
    public OpCodePart OpCodeDJNZ { get; } = new("DJNZ");
    public OpCodePart OpCodeEX { get; } = new("EX");
    public OpCodePart OpCodeEXX { get; } = new("EXX");
    public OpCodePart OpCodeIN { get; } = new("IN");
    public OpCodePart OpCodeRLC { get; } = new("RLC");
    public OpCodePart OpCodeRRC { get; } = new("RRC");
    public OpCodePart OpCodeRL { get; } = new("RL");
    public OpCodePart OpCodeRR { get; } = new("RR");
    public OpCodePart OpCodeSRA { get; } = new("SRA");
    public OpCodePart OpCodeSLL { get; } = new("SLL");

    public Register NC { get; } = new("NC");
    public Register PO { get; } = new("PO");
    public Register PE { get; } = new("PE");
    public Register P { get; } = new("P");
    public Register M { get; } = new("M");
    public Register AF_PRIME { get; } = new("AF'");

    public List<string> Errors { get; } = new();
    public Z80Assembler()
    {
        InitMap();
        InitMapExtended();
    }

    private void InitMap()
    {
        // 鬯・ｽｻ驛｢竏壺・闖ｴ・ｿ郢ｧ荳奇ｽ檎ｹｧ謚80陷ｻ・ｽ闔会ｽ､邵ｺ・ｮ郢晁・縺・ｹ昜ｺ･繝ｻ陞ｳ螟ゑｽｾ・ｩ (VB霑壼現ﾂｰ郢ｧ閾･・ｧ・ｻ隶繝ｻ
        _mnemonicMap.Add(new Z80Part[]{ LabelRef(AsmLabel.Dummy) }, new byte[]{ 0x00 }); // Dummy
        _mnemonicMap.Add(new Z80Part[]{ OpCodeADD, A, A }, new byte[]{ 0x87 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeADD, HL, BC }, new byte[]{ 0x09 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeADD, HL, DE }, new byte[]{ 0x19 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeAND, A }, new byte[]{ 0xA7 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeCALL, Value((ushort)0) }, new byte[]{ 0xCD });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeCALL, LabelRef(AsmLabel.Dummy) }, new byte[]{ 0xCD });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeCP, A }, new byte[]{ 0xBF });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeCP, B }, new byte[]{ 0xB8 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeCP, HLref }, new byte[]{ 0xBE });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeDEC, A }, new byte[]{ 0x3D });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeDEC, B }, new byte[]{ 0x05 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeDEC, C }, new byte[]{ 0x0D });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeDEC, D }, new byte[]{ 0x15 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeDEC, E }, new byte[]{ 0x1D });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeDEC, H }, new byte[]{ 0x25 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeDEC, L }, new byte[]{ 0x2D });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeDEC, HLref }, new byte[]{ 0x35 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeDEC, BC }, new byte[]{ 0x0B });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeDEC, DE }, new byte[]{ 0x1B });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeDEC, HL }, new byte[]{ 0x2B });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeDI }, new byte[]{ 0xF3 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeEI }, new byte[]{ 0xFB });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeIM_1 }, new byte[]{ 0xED, 0x56 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeINC, A }, new byte[]{ 0x3C });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeINC, B }, new byte[]{ 0x04 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeINC, C }, new byte[]{ 0x0C });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeINC, D }, new byte[]{ 0x14 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeINC, E }, new byte[]{ 0x1C });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeINC, H }, new byte[]{ 0x24 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeINC, L }, new byte[]{ 0x2C });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeINC, HLref }, new byte[]{ 0x34 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeINC, BC }, new byte[]{ 0x03 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeINC, DE }, new byte[]{ 0x13 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeINC, HL }, new byte[]{ 0x23 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeJP, Z }, new byte[]{ 0xCA });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeJP, NZ }, new byte[]{ 0xC2 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeJP, C }, new byte[]{ 0xDA });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeJP, LabelRef(AsmLabel.Dummy) }, new byte[]{ 0xC3 });
        
        // LD
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, A, A }, new byte[]{ 0x7F });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, A, B }, new byte[]{ 0x78 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, A, C }, new byte[]{ 0x79 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, A, D }, new byte[]{ 0x7A });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, A, E }, new byte[]{ 0x7B });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, A, H }, new byte[]{ 0x7C });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, A, L }, new byte[]{ 0x7D });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, A, DEref }, new byte[]{ 0x1A });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, A, HLref }, new byte[]{ 0x7E });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, A, Value((byte)0) }, new byte[]{ 0x3E });
        
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, B, A }, new byte[]{ 0x47 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, B, Value((byte)0) }, new byte[]{ 0x06 });
        
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, C, A }, new byte[]{ 0x4F });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, C, HLref }, new byte[]{ 0x4E });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, C, Value((byte)0) }, new byte[]{ 0x0E });
        
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, D, A }, new byte[]{ 0x57 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, D, H }, new byte[]{ 0x54 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, D, HLref }, new byte[]{ 0x56 });
        
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, E, A }, new byte[]{ 0x5F });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, E, L }, new byte[]{ 0x5D });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, E, HLref }, new byte[]{ 0x5E });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, E, Value((byte)0) }, new byte[]{ 0x1E });

        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, H, D }, new byte[]{ 0x62 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, L, E }, new byte[]{ 0x6B });

        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, BC, Value((ushort)0) }, new byte[]{ 0x01 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, BC, LabelRef(AsmLabel.Dummy) }, new byte[]{ 0x01 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, DE, Value((ushort)0) }, new byte[]{ 0x11 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, DE, LabelRef(AsmLabel.Dummy) }, new byte[]{ 0x11 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, HL, DE }, new byte[]{ 0x62, 0x6B }); // H=D, L=E
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, HL, Value((ushort)0) }, new byte[]{ 0x21 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, HL, LabelRef(AsmLabel.Dummy) }, new byte[]{ 0x21 });

        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, DEref, A }, new byte[]{ 0x12 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, HLref, A }, new byte[]{ 0x77 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, HLref, B }, new byte[]{ 0x70 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, HLref, C }, new byte[]{ 0x71 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, HLref, D }, new byte[]{ 0x72 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, HLref, E }, new byte[]{ 0x73 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, HLref, Value((byte)0) }, new byte[]{ 0x36 });
        
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, Value((ushort)0), A }, new byte[]{ 0x32 });
        
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLDIR }, new byte[]{ 0xED, 0xB0 });
        
        _mnemonicMap.Add(new Z80Part[]{ OpCodeAND, Value((byte)0) }, new byte[]{ 0xE6 });

        _mnemonicMap.Add(new Z80Part[]{ OpCodeSUB, A }, new byte[]{ 0x97 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSUB, B }, new byte[]{ 0x90 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSUB, C }, new byte[]{ 0x91 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSUB, D }, new byte[]{ 0x92 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSUB, E }, new byte[]{ 0x93 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSUB, H }, new byte[]{ 0x94 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSUB, L }, new byte[]{ 0x95 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSUB, Value((byte)0) }, new byte[]{ 0xD6 });

        _mnemonicMap.Add(new Z80Part[]{ OpCodeOR, A }, new byte[]{ 0xB7 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeOR, B }, new byte[]{ 0xB0 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeOR, C }, new byte[]{ 0xB1 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeOR, D }, new byte[]{ 0xB2 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeOR, E }, new byte[]{ 0xB3 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeOR, H }, new byte[]{ 0xB4 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeOR, L }, new byte[]{ 0xB5 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeOR, HLref }, new byte[]{ 0xB6 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeOR, Value((byte)0) }, new byte[]{ 0xF6 });
        
        _mnemonicMap.Add(new Z80Part[]{ OpCodeOUT, Value((byte)0) }, new byte[]{ 0xD3 });
        
        _mnemonicMap.Add(new Z80Part[]{ OpCodePUSH, BC }, new byte[]{ 0xC5 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodePUSH, DE }, new byte[]{ 0xD5 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodePUSH, HL }, new byte[]{ 0xE5 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodePUSH, AF }, new byte[]{ 0xF5 });
        
        _mnemonicMap.Add(new Z80Part[]{ OpCodePOP, BC }, new byte[]{ 0xC1 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodePOP, DE }, new byte[]{ 0xD1 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodePOP, HL }, new byte[]{ 0xE1 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodePOP, AF }, new byte[]{ 0xF1 });
        
        _mnemonicMap.Add(new Z80Part[]{ OpCodeRET }, new byte[]{ 0xC9 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSLA, A }, new byte[]{ 0xCB, 0x27 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeXOR, A }, new byte[]{ 0xAF });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, B, B }, new byte[]{ 0x40 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, B, C }, new byte[]{ 0x41 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, B, D }, new byte[]{ 0x42 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, B, E }, new byte[]{ 0x43 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, B, H }, new byte[]{ 0x44 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, B, L }, new byte[]{ 0x45 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, B, HLref }, new byte[]{ 0x46 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, B, A }, new byte[]{ 0x47 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, C, B }, new byte[]{ 0x48 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, C, C }, new byte[]{ 0x49 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, C, D }, new byte[]{ 0x4A });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, C, E }, new byte[]{ 0x4B });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, C, H }, new byte[]{ 0x4C });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, C, L }, new byte[]{ 0x4D });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, C, HLref }, new byte[]{ 0x4E });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, C, A }, new byte[]{ 0x4F });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, D, B }, new byte[]{ 0x50 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, D, C }, new byte[]{ 0x51 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, D, D }, new byte[]{ 0x52 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, D, E }, new byte[]{ 0x53 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, D, H }, new byte[]{ 0x54 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, D, L }, new byte[]{ 0x55 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, D, HLref }, new byte[]{ 0x56 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, D, A }, new byte[]{ 0x57 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, E, B }, new byte[]{ 0x58 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, E, C }, new byte[]{ 0x59 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, E, D }, new byte[]{ 0x5A });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, E, E }, new byte[]{ 0x5B });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, E, H }, new byte[]{ 0x5C });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, E, L }, new byte[]{ 0x5D });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, E, HLref }, new byte[]{ 0x5E });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, E, A }, new byte[]{ 0x5F });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, H, B }, new byte[]{ 0x60 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, H, C }, new byte[]{ 0x61 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, H, D }, new byte[]{ 0x62 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, H, E }, new byte[]{ 0x63 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, H, H }, new byte[]{ 0x64 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, H, L }, new byte[]{ 0x65 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, H, HLref }, new byte[]{ 0x66 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, H, A }, new byte[]{ 0x67 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, L, B }, new byte[]{ 0x68 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, L, C }, new byte[]{ 0x69 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, L, D }, new byte[]{ 0x6A });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, L, E }, new byte[]{ 0x6B });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, L, H }, new byte[]{ 0x6C });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, L, L }, new byte[]{ 0x6D });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, L, HLref }, new byte[]{ 0x6E });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, L, A }, new byte[]{ 0x6F });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, HLref, B }, new byte[]{ 0x70 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, HLref, C }, new byte[]{ 0x71 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, HLref, D }, new byte[]{ 0x72 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, HLref, E }, new byte[]{ 0x73 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, HLref, H }, new byte[]{ 0x74 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, HLref, L }, new byte[]{ 0x75 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, HLref, A }, new byte[]{ 0x77 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, A, B }, new byte[]{ 0x78 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, A, C }, new byte[]{ 0x79 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, A, D }, new byte[]{ 0x7A });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, A, E }, new byte[]{ 0x7B });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, A, H }, new byte[]{ 0x7C });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, A, L }, new byte[]{ 0x7D });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, A, HLref }, new byte[]{ 0x7E });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, A, A }, new byte[]{ 0x7F });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, B, Value((byte)0) }, new byte[]{ 0x06 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, C, Value((byte)0) }, new byte[]{ 0x0E });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, D, Value((byte)0) }, new byte[]{ 0x16 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, E, Value((byte)0) }, new byte[]{ 0x1E });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, H, Value((byte)0) }, new byte[]{ 0x26 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, L, Value((byte)0) }, new byte[]{ 0x2E });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, HLref, Value((byte)0) }, new byte[]{ 0x36 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, A, Value((byte)0) }, new byte[]{ 0x3E });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeADD, A, B }, new byte[]{ 0x80 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeADD, A, C }, new byte[]{ 0x81 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeADD, A, D }, new byte[]{ 0x82 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeADD, A, E }, new byte[]{ 0x83 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeADD, A, H }, new byte[]{ 0x84 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeADD, A, L }, new byte[]{ 0x85 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeADD, A, HLref }, new byte[]{ 0x86 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeADD, A, A }, new byte[]{ 0x87 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeADC, A, B }, new byte[]{ 0x88 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeADC, A, C }, new byte[]{ 0x89 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeADC, A, D }, new byte[]{ 0x8A });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeADC, A, E }, new byte[]{ 0x8B });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeADC, A, H }, new byte[]{ 0x8C });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeADC, A, L }, new byte[]{ 0x8D });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeADC, A, HLref }, new byte[]{ 0x8E });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeADC, A, A }, new byte[]{ 0x8F });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSUB, B }, new byte[]{ 0x90 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSUB, C }, new byte[]{ 0x91 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSUB, D }, new byte[]{ 0x92 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSUB, E }, new byte[]{ 0x93 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSUB, H }, new byte[]{ 0x94 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSUB, L }, new byte[]{ 0x95 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSUB, HLref }, new byte[]{ 0x96 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSUB, A }, new byte[]{ 0x97 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSBC, A, B }, new byte[]{ 0x98 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSBC, A, C }, new byte[]{ 0x99 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSBC, A, D }, new byte[]{ 0x9A });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSBC, A, E }, new byte[]{ 0x9B });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSBC, A, H }, new byte[]{ 0x9C });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSBC, A, L }, new byte[]{ 0x9D });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSBC, A, HLref }, new byte[]{ 0x9E });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSBC, A, A }, new byte[]{ 0x9F });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeAND, B }, new byte[]{ 0xA0 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeAND, C }, new byte[]{ 0xA1 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeAND, D }, new byte[]{ 0xA2 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeAND, E }, new byte[]{ 0xA3 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeAND, H }, new byte[]{ 0xA4 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeAND, L }, new byte[]{ 0xA5 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeAND, HLref }, new byte[]{ 0xA6 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeAND, A }, new byte[]{ 0xA7 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeXOR, B }, new byte[]{ 0xA8 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeXOR, C }, new byte[]{ 0xA9 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeXOR, D }, new byte[]{ 0xAA });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeXOR, E }, new byte[]{ 0xAB });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeXOR, H }, new byte[]{ 0xAC });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeXOR, L }, new byte[]{ 0xAD });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeXOR, HLref }, new byte[]{ 0xAE });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeXOR, A }, new byte[]{ 0xAF });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeOR, B }, new byte[]{ 0xB0 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeOR, C }, new byte[]{ 0xB1 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeOR, D }, new byte[]{ 0xB2 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeOR, E }, new byte[]{ 0xB3 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeOR, H }, new byte[]{ 0xB4 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeOR, L }, new byte[]{ 0xB5 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeOR, HLref }, new byte[]{ 0xB6 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeOR, A }, new byte[]{ 0xB7 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeCP, B }, new byte[]{ 0xB8 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeCP, C }, new byte[]{ 0xB9 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeCP, D }, new byte[]{ 0xBA });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeCP, E }, new byte[]{ 0xBB });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeCP, H }, new byte[]{ 0xBC });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeCP, L }, new byte[]{ 0xBD });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeCP, HLref }, new byte[]{ 0xBE });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeCP, A }, new byte[]{ 0xBF });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeADD, A, Value((byte)0) }, new byte[]{ 0xC6 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeADC, A, Value((byte)0) }, new byte[]{ 0xCE });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSUB, Value((byte)0) }, new byte[]{ 0xD6 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSBC, A, Value((byte)0) }, new byte[]{ 0xDE });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeAND, Value((byte)0) }, new byte[]{ 0xE6 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeXOR, Value((byte)0) }, new byte[]{ 0xEE });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeOR, Value((byte)0) }, new byte[]{ 0xF6 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeCP, Value((byte)0) }, new byte[]{ 0xFE });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeINC, B }, new byte[]{ 0x04 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeDEC, B }, new byte[]{ 0x05 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeINC, C }, new byte[]{ 0x0C });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeDEC, C }, new byte[]{ 0x0D });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeINC, D }, new byte[]{ 0x14 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeDEC, D }, new byte[]{ 0x15 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeINC, E }, new byte[]{ 0x1C });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeDEC, E }, new byte[]{ 0x1D });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeINC, H }, new byte[]{ 0x24 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeDEC, H }, new byte[]{ 0x25 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeINC, L }, new byte[]{ 0x2C });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeDEC, L }, new byte[]{ 0x2D });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeINC, HLref }, new byte[]{ 0x34 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeDEC, HLref }, new byte[]{ 0x35 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeINC, A }, new byte[]{ 0x3C });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeDEC, A }, new byte[]{ 0x3D });
    }

        private void InitMapExtended()
    {
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, IX, Value((ushort)0) }, new byte[]{ 0xDD, 0x21 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, IY, Value((ushort)0) }, new byte[]{ 0xFD, 0x21 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, IX, LabelRef(AsmLabel.Dummy) }, new byte[]{ 0xDD, 0x21 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, IY, LabelRef(AsmLabel.Dummy) }, new byte[]{ 0xFD, 0x21 });
// PUSH / POP
        _mnemonicMap.Add(new Z80Part[]{ OpCodePUSH, BC }, new byte[]{ 0xC5 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodePOP, BC }, new byte[]{ 0xC1 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodePUSH, DE }, new byte[]{ 0xD5 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodePOP, DE }, new byte[]{ 0xD1 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodePUSH, HL }, new byte[]{ 0xE5 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodePOP, HL }, new byte[]{ 0xE1 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodePUSH, AF }, new byte[]{ 0xF5 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodePOP, AF }, new byte[]{ 0xF1 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodePUSH, IX }, new byte[]{ 0xDD, 0xE5 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodePOP, IX }, new byte[]{ 0xDD, 0xE1 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodePUSH, IY }, new byte[]{ 0xFD, 0xE5 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodePOP, IY }, new byte[]{ 0xFD, 0xE1 });
// RET
        _mnemonicMap.Add(new Z80Part[]{ OpCodeRET }, new byte[]{ 0xC9 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeRET, NZ }, new byte[]{ 0xC0 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeRET, Z }, new byte[]{ 0xC8 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeRET, NC }, new byte[]{ 0xD0 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeRET, C }, new byte[]{ 0xD8 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeRET, PO }, new byte[]{ 0xE0 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeRET, PE }, new byte[]{ 0xE8 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeRET, P }, new byte[]{ 0xF0 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeRET, M }, new byte[]{ 0xF8 });
// JP
        _mnemonicMap.Add(new Z80Part[]{ OpCodeJP, NZ, Value((ushort)0) }, new byte[]{ 0xC2 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeJP, NZ, LabelRef(AsmLabel.Dummy) }, new byte[]{ 0xC2 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeJP, Z, Value((ushort)0) }, new byte[]{ 0xCA });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeJP, Z, LabelRef(AsmLabel.Dummy) }, new byte[]{ 0xCA });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeJP, NC, Value((ushort)0) }, new byte[]{ 0xD2 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeJP, NC, LabelRef(AsmLabel.Dummy) }, new byte[]{ 0xD2 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeJP, C, Value((ushort)0) }, new byte[]{ 0xDA });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeJP, C, LabelRef(AsmLabel.Dummy) }, new byte[]{ 0xDA });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeJP, PO, Value((ushort)0) }, new byte[]{ 0xE2 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeJP, PO, LabelRef(AsmLabel.Dummy) }, new byte[]{ 0xE2 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeJP, PE, Value((ushort)0) }, new byte[]{ 0xEA });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeJP, PE, LabelRef(AsmLabel.Dummy) }, new byte[]{ 0xEA });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeJP, P, Value((ushort)0) }, new byte[]{ 0xF2 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeJP, P, LabelRef(AsmLabel.Dummy) }, new byte[]{ 0xF2 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeJP, M, Value((ushort)0) }, new byte[]{ 0xFA });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeJP, M, LabelRef(AsmLabel.Dummy) }, new byte[]{ 0xFA });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeJP, Value((ushort)0) }, new byte[]{ 0xC3 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeJP, HLref }, new byte[]{ 0xE9 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeJP, IXref(0) }, new byte[]{ 0xDD, 0xE9 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeJP, IYref(0) }, new byte[]{ 0xFD, 0xE9 });
// CALL
        _mnemonicMap.Add(new Z80Part[]{ OpCodeCALL, NZ, Value((ushort)0) }, new byte[]{ 0xC4 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeCALL, NZ, LabelRef(AsmLabel.Dummy) }, new byte[]{ 0xC4 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeCALL, Z, Value((ushort)0) }, new byte[]{ 0xCC });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeCALL, Z, LabelRef(AsmLabel.Dummy) }, new byte[]{ 0xCC });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeCALL, NC, Value((ushort)0) }, new byte[]{ 0xD4 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeCALL, NC, LabelRef(AsmLabel.Dummy) }, new byte[]{ 0xD4 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeCALL, C, Value((ushort)0) }, new byte[]{ 0xDC });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeCALL, C, LabelRef(AsmLabel.Dummy) }, new byte[]{ 0xDC });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeCALL, PO, Value((ushort)0) }, new byte[]{ 0xE4 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeCALL, PO, LabelRef(AsmLabel.Dummy) }, new byte[]{ 0xE4 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeCALL, PE, Value((ushort)0) }, new byte[]{ 0xEC });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeCALL, PE, LabelRef(AsmLabel.Dummy) }, new byte[]{ 0xEC });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeCALL, P, Value((ushort)0) }, new byte[]{ 0xF4 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeCALL, P, LabelRef(AsmLabel.Dummy) }, new byte[]{ 0xF4 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeCALL, M, Value((ushort)0) }, new byte[]{ 0xFC });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeCALL, M, LabelRef(AsmLabel.Dummy) }, new byte[]{ 0xFC });
// JR
        _mnemonicMap.Add(new Z80Part[]{ OpCodeJR, Value((byte)0) }, new byte[]{ 0x18 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeJR, LabelRef(AsmLabel.Dummy) }, new byte[]{ 0x18 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeJR, NZ, Value((byte)0) }, new byte[]{ 0x20 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeJR, NZ, LabelRef(AsmLabel.Dummy) }, new byte[]{ 0x20 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeJR, Z, Value((byte)0) }, new byte[]{ 0x28 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeJR, Z, LabelRef(AsmLabel.Dummy) }, new byte[]{ 0x28 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeJR, NC, Value((byte)0) }, new byte[]{ 0x30 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeJR, NC, LabelRef(AsmLabel.Dummy) }, new byte[]{ 0x30 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeJR, C, Value((byte)0) }, new byte[]{ 0x38 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeJR, C, LabelRef(AsmLabel.Dummy) }, new byte[]{ 0x38 });
// DJNZ
        _mnemonicMap.Add(new Z80Part[]{ OpCodeDJNZ, Value((byte)0) }, new byte[]{ 0x10 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeDJNZ, LabelRef(AsmLabel.Dummy) }, new byte[]{ 0x10 });
// EX
        _mnemonicMap.Add(new Z80Part[]{ OpCodeEX, AF, AF_PRIME }, new byte[]{ 0x08 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeEXX }, new byte[]{ 0xD9 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeEX, DE, HL }, new byte[]{ 0xEB });
// OUT / IN
        _mnemonicMap.Add(new Z80Part[]{ OpCodeOUT, C, B }, new byte[]{ 0xED, 0x41 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeIN, B, C }, new byte[]{ 0xED, 0x40 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeOUT, C, C }, new byte[]{ 0xED, 0x49 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeIN, C, C }, new byte[]{ 0xED, 0x48 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeOUT, C, D }, new byte[]{ 0xED, 0x51 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeIN, D, C }, new byte[]{ 0xED, 0x50 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeOUT, C, E }, new byte[]{ 0xED, 0x59 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeIN, E, C }, new byte[]{ 0xED, 0x58 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeOUT, C, H }, new byte[]{ 0xED, 0x61 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeIN, H, C }, new byte[]{ 0xED, 0x60 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeOUT, C, L }, new byte[]{ 0xED, 0x69 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeIN, L, C }, new byte[]{ 0xED, 0x68 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeOUT, C, A }, new byte[]{ 0xED, 0x79 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeIN, A, C }, new byte[]{ 0xED, 0x78 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeOUT, Value((byte)0), A }, new byte[]{ 0xD3 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeIN, A, Value((byte)0) }, new byte[]{ 0xDB });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeADD, HL, BC }, new byte[]{ 0x09 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeADD, HL, DE }, new byte[]{ 0x19 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeADD, HL, HL }, new byte[]{ 0x29 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeADD, HL, SP }, new byte[]{ 0x39 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeRLC, B }, new byte[]{ 0xCB, 0x00 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeRLC, C }, new byte[]{ 0xCB, 0x01 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeRLC, D }, new byte[]{ 0xCB, 0x02 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeRLC, E }, new byte[]{ 0xCB, 0x03 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeRLC, H }, new byte[]{ 0xCB, 0x04 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeRLC, L }, new byte[]{ 0xCB, 0x05 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeRLC, HLref }, new byte[]{ 0xCB, 0x06 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeRLC, A }, new byte[]{ 0xCB, 0x07 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeRRC, B }, new byte[]{ 0xCB, 0x08 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeRRC, C }, new byte[]{ 0xCB, 0x09 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeRRC, D }, new byte[]{ 0xCB, 0x0A });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeRRC, E }, new byte[]{ 0xCB, 0x0B });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeRRC, H }, new byte[]{ 0xCB, 0x0C });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeRRC, L }, new byte[]{ 0xCB, 0x0D });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeRRC, HLref }, new byte[]{ 0xCB, 0x0E });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeRRC, A }, new byte[]{ 0xCB, 0x0F });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeRL, B }, new byte[]{ 0xCB, 0x10 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeRL, C }, new byte[]{ 0xCB, 0x11 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeRL, D }, new byte[]{ 0xCB, 0x12 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeRL, E }, new byte[]{ 0xCB, 0x13 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeRL, H }, new byte[]{ 0xCB, 0x14 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeRL, L }, new byte[]{ 0xCB, 0x15 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeRL, HLref }, new byte[]{ 0xCB, 0x16 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeRL, A }, new byte[]{ 0xCB, 0x17 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeRR, B }, new byte[]{ 0xCB, 0x18 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeRR, C }, new byte[]{ 0xCB, 0x19 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeRR, D }, new byte[]{ 0xCB, 0x1A });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeRR, E }, new byte[]{ 0xCB, 0x1B });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeRR, H }, new byte[]{ 0xCB, 0x1C });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeRR, L }, new byte[]{ 0xCB, 0x1D });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeRR, HLref }, new byte[]{ 0xCB, 0x1E });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeRR, A }, new byte[]{ 0xCB, 0x1F });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSLA, B }, new byte[]{ 0xCB, 0x20 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSLA, C }, new byte[]{ 0xCB, 0x21 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSLA, D }, new byte[]{ 0xCB, 0x22 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSLA, E }, new byte[]{ 0xCB, 0x23 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSLA, H }, new byte[]{ 0xCB, 0x24 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSLA, L }, new byte[]{ 0xCB, 0x25 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSLA, HLref }, new byte[]{ 0xCB, 0x26 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSLA, A }, new byte[]{ 0xCB, 0x27 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSRA, B }, new byte[]{ 0xCB, 0x28 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSRA, C }, new byte[]{ 0xCB, 0x29 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSRA, D }, new byte[]{ 0xCB, 0x2A });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSRA, E }, new byte[]{ 0xCB, 0x2B });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSRA, H }, new byte[]{ 0xCB, 0x2C });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSRA, L }, new byte[]{ 0xCB, 0x2D });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSRA, HLref }, new byte[]{ 0xCB, 0x2E });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSRA, A }, new byte[]{ 0xCB, 0x2F });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSLL, B }, new byte[]{ 0xCB, 0x30 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSLL, C }, new byte[]{ 0xCB, 0x31 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSLL, D }, new byte[]{ 0xCB, 0x32 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSLL, E }, new byte[]{ 0xCB, 0x33 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSLL, H }, new byte[]{ 0xCB, 0x34 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSLL, L }, new byte[]{ 0xCB, 0x35 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSLL, HLref }, new byte[]{ 0xCB, 0x36 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSLL, A }, new byte[]{ 0xCB, 0x37 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSRL, B }, new byte[]{ 0xCB, 0x38 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSRL, C }, new byte[]{ 0xCB, 0x39 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSRL, D }, new byte[]{ 0xCB, 0x3A });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSRL, E }, new byte[]{ 0xCB, 0x3B });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSRL, H }, new byte[]{ 0xCB, 0x3C });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSRL, L }, new byte[]{ 0xCB, 0x3D });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSRL, HLref }, new byte[]{ 0xCB, 0x3E });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSRL, A }, new byte[]{ 0xCB, 0x3F });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeOUT, C, A }, new byte[]{ 0xED, 0x79 });
// Auto-generated Z80 instructions
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, B, B }, new byte[]{ 0x40 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, B, C }, new byte[]{ 0x41 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, B, D }, new byte[]{ 0x42 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, B, E }, new byte[]{ 0x43 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, B, H }, new byte[]{ 0x44 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, B, L }, new byte[]{ 0x45 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, B, HLref }, new byte[]{ 0x46 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, B, A }, new byte[]{ 0x47 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, C, B }, new byte[]{ 0x48 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, C, C }, new byte[]{ 0x49 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, C, D }, new byte[]{ 0x4A });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, C, E }, new byte[]{ 0x4B });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, C, H }, new byte[]{ 0x4C });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, C, L }, new byte[]{ 0x4D });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, C, HLref }, new byte[]{ 0x4E });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, C, A }, new byte[]{ 0x4F });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, D, B }, new byte[]{ 0x50 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, D, C }, new byte[]{ 0x51 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, D, D }, new byte[]{ 0x52 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, D, E }, new byte[]{ 0x53 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, D, H }, new byte[]{ 0x54 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, D, L }, new byte[]{ 0x55 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, D, HLref }, new byte[]{ 0x56 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, D, A }, new byte[]{ 0x57 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, E, B }, new byte[]{ 0x58 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, E, C }, new byte[]{ 0x59 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, E, D }, new byte[]{ 0x5A });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, E, E }, new byte[]{ 0x5B });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, E, H }, new byte[]{ 0x5C });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, E, L }, new byte[]{ 0x5D });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, E, HLref }, new byte[]{ 0x5E });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, E, A }, new byte[]{ 0x5F });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, H, B }, new byte[]{ 0x60 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, H, C }, new byte[]{ 0x61 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, H, D }, new byte[]{ 0x62 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, H, E }, new byte[]{ 0x63 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, H, H }, new byte[]{ 0x64 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, H, L }, new byte[]{ 0x65 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, H, HLref }, new byte[]{ 0x66 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, H, A }, new byte[]{ 0x67 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, L, B }, new byte[]{ 0x68 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, L, C }, new byte[]{ 0x69 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, L, D }, new byte[]{ 0x6A });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, L, E }, new byte[]{ 0x6B });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, L, H }, new byte[]{ 0x6C });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, L, L }, new byte[]{ 0x6D });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, L, HLref }, new byte[]{ 0x6E });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, L, A }, new byte[]{ 0x6F });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, HLref, B }, new byte[]{ 0x70 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, HLref, C }, new byte[]{ 0x71 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, HLref, D }, new byte[]{ 0x72 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, HLref, E }, new byte[]{ 0x73 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, HLref, H }, new byte[]{ 0x74 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, HLref, L }, new byte[]{ 0x75 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, HLref, A }, new byte[]{ 0x77 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, A, B }, new byte[]{ 0x78 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, A, C }, new byte[]{ 0x79 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, A, D }, new byte[]{ 0x7A });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, A, E }, new byte[]{ 0x7B });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, A, H }, new byte[]{ 0x7C });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, A, L }, new byte[]{ 0x7D });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, A, HLref }, new byte[]{ 0x7E });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, A, A }, new byte[]{ 0x7F });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, B, Value((byte)0) }, new byte[]{ 0x06 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, C, Value((byte)0) }, new byte[]{ 0x0E });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, D, Value((byte)0) }, new byte[]{ 0x16 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, E, Value((byte)0) }, new byte[]{ 0x1E });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, H, Value((byte)0) }, new byte[]{ 0x26 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, L, Value((byte)0) }, new byte[]{ 0x2E });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, A, Value((byte)0) }, new byte[]{ 0x3E });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, HLref, Value((byte)0) }, new byte[]{ 0x36 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, B, IXref(0) }, new byte[]{ 0xDD, 0x46 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, IXref(0), B }, new byte[]{ 0xDD, 0x70 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, B, IYref(0) }, new byte[]{ 0xFD, 0x46 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, IYref(0), B }, new byte[]{ 0xFD, 0x70 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, C, IXref(0) }, new byte[]{ 0xDD, 0x4E });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, IXref(0), C }, new byte[]{ 0xDD, 0x71 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, C, IYref(0) }, new byte[]{ 0xFD, 0x4E });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, IYref(0), C }, new byte[]{ 0xFD, 0x71 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, D, IXref(0) }, new byte[]{ 0xDD, 0x56 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, IXref(0), D }, new byte[]{ 0xDD, 0x72 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, D, IYref(0) }, new byte[]{ 0xFD, 0x56 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, IYref(0), D }, new byte[]{ 0xFD, 0x72 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, E, IXref(0) }, new byte[]{ 0xDD, 0x5E });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, IXref(0), E }, new byte[]{ 0xDD, 0x73 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, E, IYref(0) }, new byte[]{ 0xFD, 0x5E });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, IYref(0), E }, new byte[]{ 0xFD, 0x73 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, H, IXref(0) }, new byte[]{ 0xDD, 0x66 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, IXref(0), H }, new byte[]{ 0xDD, 0x74 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, H, IYref(0) }, new byte[]{ 0xFD, 0x66 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, IYref(0), H }, new byte[]{ 0xFD, 0x74 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, L, IXref(0) }, new byte[]{ 0xDD, 0x6E });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, IXref(0), L }, new byte[]{ 0xDD, 0x75 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, L, IYref(0) }, new byte[]{ 0xFD, 0x6E });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, IYref(0), L }, new byte[]{ 0xFD, 0x75 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, A, IXref(0) }, new byte[]{ 0xDD, 0x7E });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, IXref(0), A }, new byte[]{ 0xDD, 0x77 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, A, IYref(0) }, new byte[]{ 0xFD, 0x7E });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, IYref(0), A }, new byte[]{ 0xFD, 0x77 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, IXref(0), Value((byte)0) }, new byte[]{ 0xDD, 0x36 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, IYref(0), Value((byte)0) }, new byte[]{ 0xFD, 0x36 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeADD, A, B }, new byte[]{ 0x80 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeADD, A, C }, new byte[]{ 0x81 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeADD, A, D }, new byte[]{ 0x82 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeADD, A, E }, new byte[]{ 0x83 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeADD, A, H }, new byte[]{ 0x84 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeADD, A, L }, new byte[]{ 0x85 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeADD, A, HLref }, new byte[]{ 0x86 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeADD, A, A }, new byte[]{ 0x87 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeADD, A, IXref(0) }, new byte[]{ 0xDD, 0x86 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeADD, A, IYref(0) }, new byte[]{ 0xFD, 0x86 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeADD, A, Value((byte)0) }, new byte[]{ 0xC6 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeADC, A, B }, new byte[]{ 0x88 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeADC, A, C }, new byte[]{ 0x89 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeADC, A, D }, new byte[]{ 0x8A });
_mnemonicMap.Add(new Z80Part[]{ OpCodeADC, A, E }, new byte[]{ 0x8B });
_mnemonicMap.Add(new Z80Part[]{ OpCodeADC, A, H }, new byte[]{ 0x8C });
_mnemonicMap.Add(new Z80Part[]{ OpCodeADC, A, L }, new byte[]{ 0x8D });
_mnemonicMap.Add(new Z80Part[]{ OpCodeADC, A, HLref }, new byte[]{ 0x8E });
_mnemonicMap.Add(new Z80Part[]{ OpCodeADC, A, A }, new byte[]{ 0x8F });
_mnemonicMap.Add(new Z80Part[]{ OpCodeADC, A, IXref(0) }, new byte[]{ 0xDD, 0x8E });
_mnemonicMap.Add(new Z80Part[]{ OpCodeADC, A, IYref(0) }, new byte[]{ 0xFD, 0x8E });
_mnemonicMap.Add(new Z80Part[]{ OpCodeADC, A, Value((byte)0) }, new byte[]{ 0xCE });
_mnemonicMap.Add(new Z80Part[]{ OpCodeSUB, B }, new byte[]{ 0x90 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeSUB, C }, new byte[]{ 0x91 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeSUB, D }, new byte[]{ 0x92 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeSUB, E }, new byte[]{ 0x93 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeSUB, H }, new byte[]{ 0x94 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeSUB, L }, new byte[]{ 0x95 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeSUB, HLref }, new byte[]{ 0x96 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeSUB, A }, new byte[]{ 0x97 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeSUB, IXref(0) }, new byte[]{ 0xDD, 0x96 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeSUB, IYref(0) }, new byte[]{ 0xFD, 0x96 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeSUB, Value((byte)0) }, new byte[]{ 0xD6 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeSBC, A, B }, new byte[]{ 0x98 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeSBC, A, C }, new byte[]{ 0x99 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeSBC, A, D }, new byte[]{ 0x9A });
_mnemonicMap.Add(new Z80Part[]{ OpCodeSBC, A, E }, new byte[]{ 0x9B });
_mnemonicMap.Add(new Z80Part[]{ OpCodeSBC, A, H }, new byte[]{ 0x9C });
_mnemonicMap.Add(new Z80Part[]{ OpCodeSBC, A, L }, new byte[]{ 0x9D });
_mnemonicMap.Add(new Z80Part[]{ OpCodeSBC, A, HLref }, new byte[]{ 0x9E });
_mnemonicMap.Add(new Z80Part[]{ OpCodeSBC, A, A }, new byte[]{ 0x9F });
_mnemonicMap.Add(new Z80Part[]{ OpCodeSBC, A, IXref(0) }, new byte[]{ 0xDD, 0x9E });
_mnemonicMap.Add(new Z80Part[]{ OpCodeSBC, A, IYref(0) }, new byte[]{ 0xFD, 0x9E });
_mnemonicMap.Add(new Z80Part[]{ OpCodeSBC, A, Value((byte)0) }, new byte[]{ 0xDE });
_mnemonicMap.Add(new Z80Part[]{ OpCodeAND, B }, new byte[]{ 0xA0 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeAND, C }, new byte[]{ 0xA1 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeAND, D }, new byte[]{ 0xA2 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeAND, E }, new byte[]{ 0xA3 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeAND, H }, new byte[]{ 0xA4 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeAND, L }, new byte[]{ 0xA5 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeAND, HLref }, new byte[]{ 0xA6 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeAND, A }, new byte[]{ 0xA7 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeAND, IXref(0) }, new byte[]{ 0xDD, 0xA6 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeAND, IYref(0) }, new byte[]{ 0xFD, 0xA6 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeAND, Value((byte)0) }, new byte[]{ 0xE6 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeXOR, B }, new byte[]{ 0xA8 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeXOR, C }, new byte[]{ 0xA9 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeXOR, D }, new byte[]{ 0xAA });
_mnemonicMap.Add(new Z80Part[]{ OpCodeXOR, E }, new byte[]{ 0xAB });
_mnemonicMap.Add(new Z80Part[]{ OpCodeXOR, H }, new byte[]{ 0xAC });
_mnemonicMap.Add(new Z80Part[]{ OpCodeXOR, L }, new byte[]{ 0xAD });
_mnemonicMap.Add(new Z80Part[]{ OpCodeXOR, HLref }, new byte[]{ 0xAE });
_mnemonicMap.Add(new Z80Part[]{ OpCodeXOR, A }, new byte[]{ 0xAF });
_mnemonicMap.Add(new Z80Part[]{ OpCodeXOR, IXref(0) }, new byte[]{ 0xDD, 0xAE });
_mnemonicMap.Add(new Z80Part[]{ OpCodeXOR, IYref(0) }, new byte[]{ 0xFD, 0xAE });
_mnemonicMap.Add(new Z80Part[]{ OpCodeXOR, Value((byte)0) }, new byte[]{ 0xEE });
_mnemonicMap.Add(new Z80Part[]{ OpCodeOR, B }, new byte[]{ 0xB0 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeOR, C }, new byte[]{ 0xB1 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeOR, D }, new byte[]{ 0xB2 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeOR, E }, new byte[]{ 0xB3 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeOR, H }, new byte[]{ 0xB4 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeOR, L }, new byte[]{ 0xB5 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeOR, HLref }, new byte[]{ 0xB6 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeOR, A }, new byte[]{ 0xB7 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeOR, IXref(0) }, new byte[]{ 0xDD, 0xB6 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeOR, IYref(0) }, new byte[]{ 0xFD, 0xB6 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeOR, Value((byte)0) }, new byte[]{ 0xF6 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeCP, B }, new byte[]{ 0xB8 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeCP, C }, new byte[]{ 0xB9 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeCP, D }, new byte[]{ 0xBA });
_mnemonicMap.Add(new Z80Part[]{ OpCodeCP, E }, new byte[]{ 0xBB });
_mnemonicMap.Add(new Z80Part[]{ OpCodeCP, H }, new byte[]{ 0xBC });
_mnemonicMap.Add(new Z80Part[]{ OpCodeCP, L }, new byte[]{ 0xBD });
_mnemonicMap.Add(new Z80Part[]{ OpCodeCP, HLref }, new byte[]{ 0xBE });
_mnemonicMap.Add(new Z80Part[]{ OpCodeCP, A }, new byte[]{ 0xBF });
_mnemonicMap.Add(new Z80Part[]{ OpCodeCP, IXref(0) }, new byte[]{ 0xDD, 0xBE });
_mnemonicMap.Add(new Z80Part[]{ OpCodeCP, IYref(0) }, new byte[]{ 0xFD, 0xBE });
_mnemonicMap.Add(new Z80Part[]{ OpCodeCP, Value((byte)0) }, new byte[]{ 0xFE });
_mnemonicMap.Add(new Z80Part[]{ OpCodeINC, B }, new byte[]{ 0x04 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeDEC, B }, new byte[]{ 0x05 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeINC, C }, new byte[]{ 0x0C });
_mnemonicMap.Add(new Z80Part[]{ OpCodeDEC, C }, new byte[]{ 0x0D });
_mnemonicMap.Add(new Z80Part[]{ OpCodeINC, D }, new byte[]{ 0x14 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeDEC, D }, new byte[]{ 0x15 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeINC, E }, new byte[]{ 0x1C });
_mnemonicMap.Add(new Z80Part[]{ OpCodeDEC, E }, new byte[]{ 0x1D });
_mnemonicMap.Add(new Z80Part[]{ OpCodeINC, H }, new byte[]{ 0x24 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeDEC, H }, new byte[]{ 0x25 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeINC, L }, new byte[]{ 0x2C });
_mnemonicMap.Add(new Z80Part[]{ OpCodeDEC, L }, new byte[]{ 0x2D });
_mnemonicMap.Add(new Z80Part[]{ OpCodeINC, HLref }, new byte[]{ 0x34 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeDEC, HLref }, new byte[]{ 0x35 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeINC, A }, new byte[]{ 0x3C });
_mnemonicMap.Add(new Z80Part[]{ OpCodeDEC, A }, new byte[]{ 0x3D });
_mnemonicMap.Add(new Z80Part[]{ OpCodeINC, IXref(0) }, new byte[]{ 0xDD, 0x34 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeDEC, IXref(0) }, new byte[]{ 0xDD, 0x35 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeINC, IYref(0) }, new byte[]{ 0xFD, 0x34 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeDEC, IYref(0) }, new byte[]{ 0xFD, 0x35 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, BC, Value((ushort)0) }, new byte[]{ 0x01 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, BC, LabelRef(AsmLabel.Dummy) }, new byte[]{ 0x01 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, DE, Value((ushort)0) }, new byte[]{ 0x11 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, DE, LabelRef(AsmLabel.Dummy) }, new byte[]{ 0x11 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, HL, Value((ushort)0) }, new byte[]{ 0x21 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, HL, LabelRef(AsmLabel.Dummy) }, new byte[]{ 0x21 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, SP, Value((ushort)0) }, new byte[]{ 0x31 });
_mnemonicMap.Add(new Z80Part[]{ OpCodeLD, SP, LabelRef(AsmLabel.Dummy) }, new byte[]{ 0x31 });
    }

    public void ORG(ushort address) => _startAddress = address;
    
    public void Label(AsmLabel label) => _dataList.Add(new DataLabel(label));
    public ValueLabelRef LabelRef(AsmLabel label) => new(label);
    public Value Value(ushort val) => new(val);
    public Value Value(byte val) => new(val);
    public IndexRef IXref(sbyte offset) => new IndexRef(IX, offset);
    public IndexRef IYref(sbyte offset) => new IndexRef(IY, offset);

    private void Append(Z80Part p1, Z80Part? p2 = null, Z80Part? p3 = null)
    {
        var parts = new List<Z80Part> { p1 };
        if (p2 != null) parts.Add(p2);
        if (p3 != null) parts.Add(p3);

        if (p1 is OpCodePart)
        {
            var key = new MnemonicKey(parts.ToArray());
            while (true)
            {
                if (_mnemonicMap.TryGet(key, out var bdata))
                {
                    foreach (var b in bdata!.ByteData) _dataList.Add(new DataByte(b));
                    break;
                }
                key.RemoveTail();
                if (key.Parts.Count == 0)
                {
                    string fullMnemonic = string.Join(" ", parts.Select(p => p.GetInfo()));
                    Errors.Add($"Mnemonic not found: {fullMnemonic}");
                    _dataList.Add(new DataByte(0x00));
                    break;
                }
            }
        }

        foreach (var p in parts)
        {
            if (p is Value v)
                foreach(var b in v.Bytes) _dataList.Add(new DataByte(b));
            if (p is ValueLabelRef r)
                _dataList.Add(new DataLabelRef(r.Label));
        }
    }

    // DSL Methods
    public void ADD(Z80Part p1, Z80Part p2) => Append(OpCodeADD, p1, p2);
    public void ADC(Z80Part p1, Z80Part p2) => Append(OpCodeADC, p1, p2);
    public void AND(byte p1) => Append(OpCodeAND, Value(p1));
    public void AND(Z80Part p1) => Append(OpCodeAND, p1);
    public void CALL(Z80Part p1) => Append(OpCodeCALL, p1);
    public void CP(Z80Part p1) => Append(OpCodeCP, p1);
    public void DEC(Z80Part p1) => Append(OpCodeDEC, p1);
    public void DI() => Append(OpCodeDI);
    public void EI() => Append(OpCodeEI);
    public void IM1() => Append(OpCodeIM_1);
    public void INC(Z80Part p1) => Append(OpCodeINC, p1);
    public void JP(Z80Part p1, Z80Part p2) => Append(OpCodeJP, p1, p2);
    public void JP(Z80Part p1) => Append(OpCodeJP, p1);
    public void LD(Z80Part p1, Z80Part p2) => Append(OpCodeLD, p1, p2);
    public void LD(ushort p1, Z80Part p2) => Append(OpCodeLD, Value(p1), p2);
    public void LD(Z80Part p1, ushort p2) => Append(OpCodeLD, p1, Value(p2));
    public void LD(Z80Part p1, byte p2) => Append(OpCodeLD, p1, Value(p2));
    public void LDIR() => Append(OpCodeLDIR);
    public void OR(Z80Part p1) => Append(OpCodeOR, p1);
    public void OR(byte p1) => Append(OpCodeOR, Value(p1));
    public void OUT(byte port) => Append(OpCodeOUT, Value(port));
    public void OUT(Z80Part p1, Z80Part p2) => Append(OpCodeOUT, p1, p2);
    public void POP(Z80Part p1) => Append(OpCodePOP, p1);
    public void PUSH(Z80Part p1) => Append(OpCodePUSH, p1);
    public void RET() => Append(OpCodeRET);
    public void SBC(Z80Part p1, Z80Part p2) => Append(OpCodeSBC, p1, p2);
    public void SLA(Z80Part p1) => Append(OpCodeSLA, p1);
    public void SRL(Z80Part p1) => Append(OpCodeSRL, p1);
    public void SUB(Z80Part p1) => Append(OpCodeSUB, p1);
    public void SUB(byte p1) => Append(OpCodeSUB, Value(p1));
    public void XOR(Z80Part p1) => Append(OpCodeXOR, p1);

    public void DB(byte[] data)
    {
        foreach (var b in data) _dataList.Add(new DataByte(b));
    }
    public void DB(byte data) => _dataList.Add(new DataByte(data));
    public void DB(Z80Part part)
    {
        if (part is ValueLabelRef r) _dataList.Add(new DataLabelRef(r.Label));
        else if (part is Value v) foreach(var b in v.Bytes) _dataList.Add(new DataByte(b));
    }

    public void DW(Z80Part part)
    {
        if (part is ValueLabelRef r)
        {
            _dataList.Add(new DataLabelRef(r.Label)); // LabelRef邵ｺ・ｯ2郢晁・縺・ｹ晏現縺・ｹｧ・ｵ郢ｧ・､郢晢ｽｳ邵ｺ蜷ｶ・狗ｹｧ蛹ｻ竕ｧ邵ｺ・ｫPass 1邵ｺ・ｧ陷・ｽｦ騾・・・・ｹｧ蠕娯ｻ邵ｺ繝ｻ・・
        }
        else if (part is Value v)
        {
            if (v.Bytes.Length == 1)
            {
                _dataList.Add(new DataByte(v.Bytes[0]));
                _dataList.Add(new DataByte(0));
            }
            else
            {
                _dataList.Add(new DataByte(v.Bytes[0]));
                _dataList.Add(new DataByte(v.Bytes[1]));
            }
        }
    }

    public byte[] Build()
    {
        if (Errors.Count > 0)
        {
            var distinctErrors = Errors.Distinct().ToList();
            throw new Exception("Missing mnemonics:\n" + string.Join("\n", distinctErrors));
        }
        ushort addr = _startAddress;
        var labelMap = new Dictionary<AsmLabel, ushort>();
        var resolvedList = new List<AssemblerData>();

        // Pass 1: Resolve Labels
        foreach (var dat in _dataList)
        {
            if (dat is DataLabel lbl)
            {
                labelMap[lbl.Label] = addr;
                dat.Address = addr;
            }
            else if (dat is DataLabelRef)
            {
                resolvedList.Add(dat);
                addr += 2;
            }
            else
            {
                dat.Address = addr;
                resolvedList.Add(dat);
                addr += 1;
            }
        }

        // Pass 2: Emit Bytes
        var byteList = new List<byte>();
        foreach (var dat in resolvedList)
        {
            if (dat is DataLabelRef r)
            {
                if (labelMap.TryGetValue(r.Label, out ushort lblAddr))
                {
                    dat.Address = lblAddr;
                    byteList.AddRange(dat.GetBytes());
                }
                else
                {
                    Errors.Add($"Label not found: {r.Label.Name}");
                    byteList.AddRange(new byte[] { 0x00, 0x00 });
                }
            }
            else
            {
                byteList.AddRange(dat.GetBytes());
            }
        }

        if (Errors.Count > 0)
        {
            var distinctErrors = Errors.Distinct().ToList();
            throw new Exception("Errors during assembly:\n" + string.Join("\n", distinctErrors));
        }

        return byteList.ToArray();
    }
}










