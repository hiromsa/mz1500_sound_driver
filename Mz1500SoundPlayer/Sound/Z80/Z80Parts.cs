using System;
using System.Collections.Generic;
using System.Linq;

namespace Mz1500SoundPlayer.Sound.Z80;

// Operand, Mnemonic definitions
public abstract class Z80Part : IEquatable<Z80Part>
{
    public abstract bool Equals(Z80Part? other);
    public abstract string GetInfo();
    public override int GetHashCode() => 0; // Requires proper implementation per derived class for Dictionary use
}

public class OpCodePart : Z80Part
{
    public string Name { get; }
    public OpCodePart(string name) => Name = name;
    public override bool Equals(Z80Part? other) => other is OpCodePart o && Name == o.Name;
    public override string GetInfo() => Name;
    public override int GetHashCode() => Name.GetHashCode();
}

public abstract class OperandPart : Z80Part { }

public class Register : OperandPart
{
    public string Name { get; }
    public Register(string name) => Name = name;
    public override bool Equals(Z80Part? other) => other is Register r && Name == r.Name;
    public override string GetInfo() => Name;
    public override int GetHashCode() => Name.GetHashCode();
}

public class Value : OperandPart
{
    public byte[] Bytes { get; }
    public Value(byte b) => Bytes = new[] { b };
    public Value(ushort s) => Bytes = BitConverter.GetBytes(s);
    public override bool Equals(Z80Part? other) => other is Value; // Simplified for map search
    public override string GetInfo() => $"<{BitConverter.ToString(Bytes)}>";
    public override int GetHashCode() => 0;
}

public class ValueRef : OperandPart
{
    public byte[] Bytes { get; }
    public ValueRef(byte b) => Bytes = new[] { b };
    public ValueRef(ushort s) => Bytes = BitConverter.GetBytes(s);
    public override bool Equals(Z80Part? other) => other is ValueRef;
    public override string GetInfo() => $"(<{BitConverter.ToString(Bytes)}>)";
    public override int GetHashCode() => 1;
}

public class ValueLabelRef : OperandPart
{
    public string Name { get; }
    public ValueLabelRef(string name) => Name = name;
    public override bool Equals(Z80Part? other) => other is ValueLabelRef;
    public override string GetInfo() => "(<label>)";
    public override int GetHashCode() => 2;
}

// Data Nodes
public abstract class AssemblerData
{
    public ushort Address { get; set; }
    public string MnemonicDescription { get; set; } = "";
    public abstract bool HasBytes();
    public abstract byte[] GetBytes();
}

public class DataLabel : AssemblerData
{
    public string Name { get; }
    public DataLabel(string name) => Name = name;
    public override bool HasBytes() => false;
    public override byte[] GetBytes() => Array.Empty<byte>();
}

public class DataLabelRef : AssemblerData
{
    public string Name { get; }
    public DataLabelRef(string name) => Name = name;
    public override bool HasBytes() => true;
    public override byte[] GetBytes() => BitConverter.GetBytes(Address);
}

public class DataByte : AssemblerData
{
    public byte Data { get; }
    public DataByte(byte data) => Data = data;
    public override bool HasBytes() => true;
    public override byte[] GetBytes() => new[] { Data };
}

public class MnemonicKey : IEquatable<MnemonicKey>
{
    public List<Z80Part> Parts { get; } = new();

    public MnemonicKey(params Z80Part[] parts)
    {
        Parts.AddRange(parts);
    }

    public void RemoveTail() => Parts.RemoveAt(Parts.Count - 1);

    public bool Equals(MnemonicKey? other)
    {
        if (other is null || Parts.Count != other.Parts.Count) return false;
        for (int i = 0; i < Parts.Count; i++)
        {
            if (!Parts[i].Equals(other.Parts[i])) return false;
        }
        return true;
    }

    public override int GetHashCode()
    {
        int hash = 17;
        foreach (var p in Parts) hash = hash * 31 + p.GetHashCode();
        return hash;
    }
}
