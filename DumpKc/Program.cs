using System;
using Mz1500SoundPlayer.Sound;

class Program {
    static void Main() {
        double[] freqs = { 261.6, 293.6, 329.6, 349.2, 392.0 };
        foreach (var freq in freqs) {
            Ym2151Helper.GetKcKf(freq, out byte kc, out byte kf);
            Console.WriteLine($""Freq: {freq} -> KC: {kc:X2} KF: {kf:X2}"");
        }
    }
}