using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Mz1500SoundPlayer;

public class FmEditorViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private int _alg;
    public int Alg
    {
        get => _alg;
        set 
        { 
            _alg = value; 
            UpdateCarrierRoles();
            OnPropertyChanged(); 
        }
    }

    private void UpdateCarrierRoles()
    {
        Op1.IsCarrier = false;
        Op2.IsCarrier = false;
        Op3.IsCarrier = false;
        Op4.IsCarrier = false;

        switch (_alg)
        {
            case 0: Op4.IsCarrier = true; break;
            case 1: Op4.IsCarrier = true; break;
            case 2: Op4.IsCarrier = true; break;
            case 3: Op4.IsCarrier = true; break;
            case 4: Op2.IsCarrier = true; Op4.IsCarrier = true; break;
            case 5: Op2.IsCarrier = true; Op3.IsCarrier = true; Op4.IsCarrier = true; break;
            case 6: Op2.IsCarrier = true; Op3.IsCarrier = true; Op4.IsCarrier = true; break;
            case 7: Op1.IsCarrier = true; Op2.IsCarrier = true; Op3.IsCarrier = true; Op4.IsCarrier = true; break;
        }
    }

    private int _fb;
    public int Fb
    {
        get => _fb;
        set { _fb = value; OnPropertyChanged(); }
    }

    public FmOperatorViewModel Op1 { get; } = new("OP1 (M1)");
    public FmOperatorViewModel Op2 { get; } = new("OP2 (M2)");
    public FmOperatorViewModel Op3 { get; } = new("OP3 (C1)");
    public FmOperatorViewModel Op4 { get; } = new("OP4 (C2)");

    public FmEditorViewModel()
    {
        Op1.PropertyChanged += Operator_PropertyChanged;
        Op2.PropertyChanged += Operator_PropertyChanged;
        Op3.PropertyChanged += Operator_PropertyChanged;
        Op4.PropertyChanged += Operator_PropertyChanged;
    }

    private void Operator_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // When any operator parameter changes, we can trigger an event or command if needed
    }

    public void ParseMml(string mml)
    {
        // Example: @FM[1] = { 0, 0, 31, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, ... }
        // 46 parameters
        var match = Regex.Match(mml, @"\{([^}]+)\}");
        if (match.Success)
        {
            var parts = match.Groups[1].Value.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 46)
            {
                Alg = int.Parse(parts[0].Trim());
                Fb = int.Parse(parts[1].Trim());

                // MML Order: OP1, OP2, OP3, OP4
                Op1.Parse(parts, 2 + 0 * 11);
                Op2.Parse(parts, 2 + 1 * 11);
                Op3.Parse(parts, 2 + 2 * 11);
                Op4.Parse(parts, 2 + 3 * 11);
            }
        }
    }

    public string ToMml(int fmNumber)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append($"@FM[{fmNumber}] = {{ ");
        sb.Append($"{Alg}, {Fb}, ");
        sb.Append(Op1.ToMml());
        sb.Append(", ");
        sb.Append(Op2.ToMml());
        sb.Append(", ");
        sb.Append(Op3.ToMml());
        sb.Append(", ");
        sb.Append(Op4.ToMml());
        sb.Append(" }");
        return sb.ToString();
    }
}

public class FmOperatorViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public string Title { get; }

    private bool _isCarrier;
    public bool IsCarrier
    {
        get => _isCarrier;
        set { _isCarrier = value; OnPropertyChanged(); }
    }

    private bool _isMuted;
    public bool IsMuted
    {
        get => _isMuted;
        set { _isMuted = value; OnPropertyChanged(); }
    }

    private bool _isSolo;
    public bool IsSolo
    {
        get => _isSolo;
        set { _isSolo = value; OnPropertyChanged(); }
    }

    public FmOperatorViewModel(string title)
    {
        Title = title;
    }

    private int _ar = 31;
    public int Ar { get => _ar; set { _ar = value; OnPropertyChanged(); } }

    private int _d1r;
    public int D1r { get => _d1r; set { _d1r = value; OnPropertyChanged(); } }

    private int _d2r;
    public int D2r { get => _d2r; set { _d2r = value; OnPropertyChanged(); } }

    private int _rr = 15;
    public int Rr { get => _rr; set { _rr = value; OnPropertyChanged(); } }

    private int _d1l;
    public int D1l { get => _d1l; set { _d1l = value; OnPropertyChanged(); } }

    private int _tl = 127; // 127 is max attenuation (silent)
    public int Tl { get => _tl; set { _tl = value; OnPropertyChanged(); } }

    private int _ks;
    public int Ks { get => _ks; set { _ks = value; OnPropertyChanged(); } }

    private int _mul = 1;
    public int Mul { get => _mul; set { _mul = value; OnPropertyChanged(); } }

    private int _dt1;
    public int Dt1 { get => _dt1; set { _dt1 = value; OnPropertyChanged(); } }

    private int _dt2;
    public int Dt2 { get => _dt2; set { _dt2 = value; OnPropertyChanged(); } }

    private int _ame;
    public int Ame { get => _ame; set { _ame = value; OnPropertyChanged(); } }

    public void Parse(string[] parts, int offset)
    {
        // Parameter order in MML: AR, D1R, D2R, RR, D1L, TL, KS, MUL, DT1, DT2, AME
        Ar = int.Parse(parts[offset + 0].Trim());
        D1r = int.Parse(parts[offset + 1].Trim());
        D2r = int.Parse(parts[offset + 2].Trim());
        Rr = int.Parse(parts[offset + 3].Trim());
        D1l = int.Parse(parts[offset + 4].Trim());
        Tl = int.Parse(parts[offset + 5].Trim());
        Ks = int.Parse(parts[offset + 6].Trim());
        Mul = int.Parse(parts[offset + 7].Trim());
        Dt1 = int.Parse(parts[offset + 8].Trim());
        Dt2 = int.Parse(parts[offset + 9].Trim());
        Ame = int.Parse(parts[offset + 10].Trim());
    }

    public string ToMml()
    {
        return $"{Ar}, {D1r}, {D2r}, {Rr}, {D1l}, {Tl}, {Ks}, {Mul}, {Dt1}, {Dt2}, {Ame}";
    }

    public void CopyFrom(FmOperatorViewModel other)
    {
        Ar = other.Ar;
        D1r = other.D1r;
        D2r = other.D2r;
        Rr = other.Rr;
        D1l = other.D1l;
        Tl = other.Tl;
        Ks = other.Ks;
        Mul = other.Mul;
        Dt1 = other.Dt1;
        Dt2 = other.Dt2;
        Ame = other.Ame;
    }
}
