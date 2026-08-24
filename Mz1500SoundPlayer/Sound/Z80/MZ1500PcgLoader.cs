using System;

namespace Mz1500SoundPlayer.Sound.Z80;

public static class MZ1500PcgLoader
{
    public static void AppendImageLoader(Z80Assembler asm, byte[] pcgData)
    {
        string prefix = "pcg_";
        var lblImageLoader = new AsmLabel("ImageLoader");
        var lblCls = new AsmLabel(prefix + "CLS");
        var lblStart = new AsmLabel(prefix + "start");
        var lblMemfil = new AsmLabel(prefix + "MEMFIL");
        var lblLoopStart = new AsmLabel(prefix + "LoopStart");
        var lblLoopEnd = new AsmLabel(prefix + "LoopEnd");
        var lblVramStart = new AsmLabel(prefix + "VRAM-start");
        var lblVramLoop = new AsmLabel(prefix + "VRAM-loop");
        var lblVramLoopReturn = new AsmLabel(prefix + "VRAM-loop-return");
        var lblLoopSkipPcg = new AsmLabel(prefix + "loop_skip_pcg:");
        
        var lblPsgGreenStart = new AsmLabel(prefix + "PSGData-Green-start");
        var lblPsgGreenEnd = new AsmLabel(prefix + "PSGData-Green-end");
        var lblPsgRedStart = new AsmLabel(prefix + "PSGData-Red-start");
        var lblPsgRedEnd = new AsmLabel(prefix + "PSGData-Red-end");
        var lblPsgBlueStart = new AsmLabel(prefix + "PSGData-Blue-start");
        var lblPsgBlueEnd = new AsmLabel(prefix + "PSGData-Blue-end");

        asm.Label(lblImageLoader);
        asm.CALL(asm.LabelRef(lblCls));
        asm.CALL(asm.LabelRef(lblStart));
        asm.RET();

        asm.Label(lblCls);
        asm.CALL(asm.Value((ushort)0x0DA6)); // Basic CLS routine
        asm.LD(asm.HL, 0xD000);
        asm.LD(asm.BC, 40 * 25);
        asm.XOR(asm.A);
        asm.CALL(asm.LabelRef(lblMemfil));

        asm.LD(asm.HL, 0xD800);
        asm.LD(asm.BC, 40 * 25);
        asm.LD(asm.A, 0x0);
        asm.CALL(asm.LabelRef(lblMemfil));
        asm.RET();

        asm.Label(lblMemfil);
        asm.LD(asm.D, asm.H);
        asm.LD(asm.E, asm.L);
        asm.INC(asm.DE);
        asm.DEC(asm.BC);
        asm.LD(asm.HLref, asm.A);
        asm.LDIR();
        asm.RET();

        asm.Label(lblStart);
        
        asm.OUT(0xF1); // F1 Output
        asm.LD(asm.A, 0x1);
        asm.OUT(0xF0); // F0 Output for Display Priority/Screen 2

        // PCG Pattern setup
        byte e5 = 0xE5;

        // Bank 3 (Green)
        asm.LD(asm.DE, asm.LabelRef(lblPsgGreenStart));
        asm.LD(asm.BC, asm.LabelRef(lblPsgGreenEnd));
        asm.LD(asm.HL, 0xD000);
        asm.LD(asm.A, 0x3);
        asm.OUT(e5);
        asm.CALL(asm.LabelRef(lblLoopStart));

        // Bank 2 (Red)
        asm.LD(asm.DE, asm.LabelRef(lblPsgRedStart));
        asm.LD(asm.BC, asm.LabelRef(lblPsgRedEnd));
        asm.LD(asm.HL, 0xD000);
        asm.LD(asm.A, 0x2);
        asm.OUT(e5);
        asm.CALL(asm.LabelRef(lblLoopStart));

        // Bank 1 (Blue)
        asm.LD(asm.DE, asm.LabelRef(lblPsgBlueStart));
        asm.LD(asm.BC, asm.LabelRef(lblPsgBlueEnd));
        asm.LD(asm.HL, 0xD000);
        asm.LD(asm.A, 0x1);
        asm.OUT(e5);
        asm.CALL(asm.LabelRef(lblLoopStart));

        asm.JP(asm.LabelRef(lblLoopEnd));

        asm.Label(lblLoopStart);
        asm.LD(asm.A, asm.DEref); // Get 1 byte of PCG
        asm.LD(asm.HLref, asm.A); // Write to VRAM
        asm.INC(asm.DE);
        asm.INC(asm.HL);

        asm.LD(asm.A, asm.B);
        asm.CP(asm.D);
        asm.JP(asm.NZ, asm.LabelRef(lblLoopStart));
        asm.LD(asm.A, asm.C);
        asm.CP(asm.E);
        asm.JP(asm.NZ, asm.LabelRef(lblLoopStart));
        asm.RET();

        asm.Label(lblLoopEnd);

        // Set Screen VRAM characters to match PCG indices
        asm.Label(lblVramStart);
        asm.LD(asm.HL, 0xD400); // Screen 2 VRAM
        asm.LD(asm.DE, 0xDC00); // Screen 2 Color Data
        
        asm.LD(asm.B, 0x00);
        asm.LD(asm.C, 0b00001000); // 0x08
        asm.CALL(asm.LabelRef(lblVramLoop));
        
        asm.LD(asm.B, 0x00);
        asm.LD(asm.C, 0b01001000); // 0x48
        asm.CALL(asm.LabelRef(lblVramLoop));
        
        asm.LD(asm.B, 0x00);
        asm.LD(asm.C, 0b10001000); // 0x88
        asm.CALL(asm.LabelRef(lblVramLoop));
        
        asm.LD(asm.B, 0x00);
        asm.LD(asm.C, 0b11001000); // 0xC8
        asm.CALL(asm.LabelRef(lblVramLoop));
        
        asm.JP(asm.LabelRef(lblLoopSkipPcg)); // bypass loop symbol name collision

        asm.Label(lblVramLoop);
        asm.LD(asm.A, 0x1);
        asm.OUT(0xE6); // PCG Access Enable?

        asm.LD(asm.HLref, asm.B); // Set char code 0~255
        asm.INC(asm.HL);

        asm.LD(asm.A, asm.C);
        asm.LD(asm.DEref, asm.A); // Set color attribute
        asm.INC(asm.DE);

        asm.LD(asm.A, 0xFF);
        asm.CP(asm.B);
        asm.JP(asm.Z, asm.LabelRef(lblVramLoopReturn));
        asm.INC(asm.B);
        asm.JP(asm.LabelRef(lblVramLoop));

        asm.Label(lblVramLoopReturn);
        asm.RET();

        asm.Label(lblLoopSkipPcg);
        asm.RET();

        // Data payload
        var greenPlane = new byte[8000];
        var redPlane = new byte[8000];
        var bluePlane = new byte[8000];
        Array.Copy(pcgData, 0, greenPlane, 0, 8000);
        Array.Copy(pcgData, 8000, redPlane, 0, 8000);
        Array.Copy(pcgData, 16000, bluePlane, 0, 8000);

        asm.Label(lblPsgGreenStart);
        asm.DB(greenPlane);
        asm.Label(lblPsgGreenEnd);

        asm.Label(lblPsgRedStart);
        asm.DB(redPlane);
        asm.Label(lblPsgRedEnd);

        asm.Label(lblPsgBlueStart);
        asm.DB(bluePlane);
        asm.Label(lblPsgBlueEnd);
    }
}
