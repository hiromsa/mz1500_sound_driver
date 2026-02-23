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

public class MZ1500Assembler
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
    public Register IX { get; } = new("IX");
    public Register IY { get; } = new("IY");
    public Register Z { get; } = new("Z");
    public Register NZ { get; } = new("NZ");
    
    // Register Refs
    public Register DEref { get; } = new("(DE)");
    public Register HLref { get; } = new("(HL)");

    // Opcodes
    public OpCodePart OpCodeADD { get; } = new("ADD");
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
    public OpCodePart OpCodeXOR { get; } = new("XOR");

    public MZ1500Assembler()
    {
        InitMap();
    }

    private void InitMap()
    {
        // 頻繁に使われるZ80命令のバイト列定義 (VB版から移植)
        _mnemonicMap.Add(new Z80Part[]{ LabelRef("") }, new byte[]{ 0x00 }); // Dummy
        _mnemonicMap.Add(new Z80Part[]{ OpCodeADD, HL, DE }, new byte[]{ 0x19 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeAND, A }, new byte[]{ 0xA7 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeCALL, Value((ushort)0) }, new byte[]{ 0xCD });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeCALL, LabelRef("") }, new byte[]{ 0xCD });
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
        _mnemonicMap.Add(new Z80Part[]{ OpCodeJP, LabelRef("") }, new byte[]{ 0xC3 });
        
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
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, C, Value((byte)0) }, new byte[]{ 0x0E });
        
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, D, A }, new byte[]{ 0x57 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, D, H }, new byte[]{ 0x54 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, D, HLref }, new byte[]{ 0x56 });
        
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, E, A }, new byte[]{ 0x5F });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, E, L }, new byte[]{ 0x5D });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, E, HLref }, new byte[]{ 0x5E });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, E, Value((byte)0) }, new byte[]{ 0x1E });

        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, BC, Value((ushort)0) }, new byte[]{ 0x01 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, BC, LabelRef("") }, new byte[]{ 0x01 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, DE, Value((ushort)0) }, new byte[]{ 0x11 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, DE, LabelRef("") }, new byte[]{ 0x11 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, HL, DE }, new byte[]{ 0x62, 0x6B }); // H=D, L=E
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, HL, Value((ushort)0) }, new byte[]{ 0x21 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, HL, LabelRef("") }, new byte[]{ 0x21 });

        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, DEref, A }, new byte[]{ 0x12 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, HLref, A }, new byte[]{ 0x77 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, HLref, B }, new byte[]{ 0x70 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, HLref, C }, new byte[]{ 0x71 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, HLref, D }, new byte[]{ 0x72 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, HLref, E }, new byte[]{ 0x73 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, HLref, Value((byte)0) }, new byte[]{ 0x36 });
        
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLD, Value((ushort)0), A }, new byte[]{ 0x32 });
        
        _mnemonicMap.Add(new Z80Part[]{ OpCodeLDIR }, new byte[]{ 0xED, 0xB0 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeOR, A }, new byte[]{ 0xB7 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeOR, B }, new byte[]{ 0xB0 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeOR, C }, new byte[]{ 0xB1 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeOR, D }, new byte[]{ 0xB2 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeOR, E }, new byte[]{ 0xB3 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeOR, H }, new byte[]{ 0xB4 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeOR, L }, new byte[]{ 0xB5 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeOR, HLref }, new byte[]{ 0xB6 });
        
        _mnemonicMap.Add(new Z80Part[]{ OpCodeOUT, Value((byte)0) }, new byte[]{ 0xD3 });
        
        _mnemonicMap.Add(new Z80Part[]{ OpCodePUSH, DE }, new byte[]{ 0xD5 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodePUSH, HL }, new byte[]{ 0xE5 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodePOP, DE }, new byte[]{ 0xD1 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodePOP, HL }, new byte[]{ 0xE1 });
        
        _mnemonicMap.Add(new Z80Part[]{ OpCodeRET }, new byte[]{ 0xC9 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeSLA, A }, new byte[]{ 0xCB, 0x27 });
        _mnemonicMap.Add(new Z80Part[]{ OpCodeXOR, A }, new byte[]{ 0xAF });
    }

    public void ORG(ushort address) => _startAddress = address;
    
    public void Label(string name) => _dataList.Add(new DataLabel(name));
    public ValueLabelRef LabelRef(string name) => new(name);
    public Value Value(ushort val) => new(val);
    public Value Value(byte val) => new(val);

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
                if (key.Parts.Count == 0) throw new Exception($"Mnemonic not found: {p1.GetInfo()}");
            }
        }

        foreach (var p in parts)
        {
            if (p is Value v)
                foreach(var b in v.Bytes) _dataList.Add(new DataByte(b));
            if (p is ValueLabelRef r)
                _dataList.Add(new DataLabelRef(r.Name));
        }
    }

    // DSL Methods
    public void ADD(Z80Part p1, Z80Part p2) => Append(OpCodeADD, p1, p2);
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
    public void OUT(byte port) => Append(OpCodeOUT, Value(port));
    public void POP(Z80Part p1) => Append(OpCodePOP, p1);
    public void PUSH(Z80Part p1) => Append(OpCodePUSH, p1);
    public void RET() => Append(OpCodeRET);
    public void SLA(Z80Part p1) => Append(OpCodeSLA, p1);
    public void XOR(Z80Part p1) => Append(OpCodeXOR, p1);

    public void DB(byte[] data)
    {
        foreach (var b in data) _dataList.Add(new DataByte(b));
    }
    public void DB(byte data) => _dataList.Add(new DataByte(data));
    public void DB(Z80Part part)
    {
        if (part is ValueLabelRef r) _dataList.Add(new DataLabelRef(r.Name));
        else if (part is Value v) foreach(var b in v.Bytes) _dataList.Add(new DataByte(b));
    }

    public byte[] Build()
    {
        ushort addr = _startAddress;
        var labelMap = new Dictionary<string, ushort>();
        var resolvedList = new List<AssemblerData>();

        // Pass 1: Resolve Labels
        foreach (var dat in _dataList)
        {
            if (dat is DataLabel lbl)
            {
                labelMap[lbl.Name] = addr;
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
                dat.Address = labelMap[r.Name];
                byteList.AddRange(dat.GetBytes());
            }
            else
            {
                byteList.AddRange(dat.GetBytes());
            }
        }

        return byteList.ToArray();
    }
}
