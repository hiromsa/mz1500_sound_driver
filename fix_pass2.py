import re

path = r"c:\tools\mz1500_sound_driver\Mz1500SoundPlayer\Sound\Z80\Z80Assembler.cs"

with open(path, "r", encoding="utf-8") as f:
    content = f.read()

old_code = """        // Pass 2: Emit Bytes
        var byteList = new List<byte>();
        foreach (var dat in resolvedList)
        {
            if (dat is DataLabelRef r)
            {
                dat.Address = labelMap[r.Label];
                byteList.AddRange(dat.GetBytes());
            }
            else
            {
                byteList.AddRange(dat.GetBytes());
            }
        }

        return byteList.ToArray();"""

new_code = """        // Pass 2: Emit Bytes
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
            throw new Exception("Errors during assembly:\\n" + string.Join("\\n", distinctErrors));
        }

        return byteList.ToArray();"""

# Handle CRLF and LF differences
old_code_crlf = old_code.replace("\n", "\r\n")

if old_code in content:
    content = content.replace(old_code, new_code)
elif old_code_crlf in content:
    content = content.replace(old_code_crlf, new_code.replace("\n", "\r\n"))
else:
    print("Could not find the target codeblock!")
    import sys
    sys.exit(1)

with open(path, "w", encoding="utf-8") as f:
    f.write(content)

print("Replacement successful.")
