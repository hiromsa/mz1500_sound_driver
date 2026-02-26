using System;

namespace FamiStudio
{
    public struct Color
    {
        public byte R, G, B, A;

        public static Color FromArgb(int a, int r, int g, int b) => new Color { A = (byte)a, R = (byte)r, G = (byte)g, B = (byte)b };
        public static Color FromArgb(int r, int g, int b) => new Color { A = 255, R = (byte)r, G = (byte)g, B = (byte)b };
        public static Color FromArgb(int argb) => new Color { A = (byte)((argb >> 24) & 0xFF), R = (byte)((argb >> 16) & 0xFF), G = (byte)((argb >> 8) & 0xFF), B = (byte)(argb & 0xFF) };
        public int ToArgb() => (A << 24) | (R << 16) | (G << 8) | B;

        public static Color Azure => FromArgb(240, 255, 255);
    }

    public struct LocalizedString
    {
        private string _value;
        public LocalizedString(string value) { _value = value; }
        public string Value => _value ?? string.Empty;
        public static implicit operator string(LocalizedString ls) => ls._value;
        public static implicit operator LocalizedString(string s) => new LocalizedString(s);
        public override string ToString() => _value ?? string.Empty;
    }

    public static class Theme
    {
        public static Color[] CustomColors = new Color[1] { new Color() };
        public static Color RandomCustomColor() => new Color();
        public static Color EnforceThemeColor(Color c) => c;
    }

    public static class NesApu
    {
        public const int FpsPAL = 50;
        public const int FpsNTSC = 60;
        public const int NUM_WAV_EXPORT_APU = 1;
        public const int DACDefaultValueDiv2 = 64;
        public const int DACDefaultValue = 64;
    }

    public static class Localization
    {
        public static void LocalizeStatic(Type t) { }
    }

    public static class Platform
    {
        public static bool IsDesktop = true;
        public static bool IsMobile = false;
        public static double TimeSeconds() => 0.0;
        public static bool IsInMainThread() => true;
    }

    public static class Settings
    {
        public static int PatternNameNumDigits = 2;
        public static string PatternNamePrefix = "Pattern ";
    }
}
