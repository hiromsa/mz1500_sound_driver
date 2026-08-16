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
    public FmOperatorViewModel Op2 { get; } = new("OP2 (C1)");
    public FmOperatorViewModel Op3 { get; } = new("OP3 (M2)");
    public FmOperatorViewModel Op4 { get; } = new("OP4 (C2)");

    public FmEditorViewModel()
    {
        Op1.PropertyChanged += Operator_PropertyChanged;
        Op2.PropertyChanged += Operator_PropertyChanged;
        Op3.PropertyChanged += Operator_PropertyChanged;
        Op4.PropertyChanged += Operator_PropertyChanged;

        Op1.RequestDeltaApply = TryApplyDelta;
        Op2.RequestDeltaApply = TryApplyDelta;
        Op3.RequestDeltaApply = TryApplyDelta;
        Op4.RequestDeltaApply = TryApplyDelta;

        // Default to a simple sine wave (Algorithm 0, Carrier OP4)
        Alg = 0;
        Op4.ApplyValueDirectly(nameof(FmOperatorViewModel.Tl), 0);
    }

    private bool TryApplyDelta(FmOperatorViewModel sender, string propertyName, int delta, int requestedValue)
    {
        if (!sender.IsSelected) return false;

        var allOps = new[] { Op1, Op2, Op3, Op4 };
        var selectedOps = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Where(allOps, o => o.IsSelected));
        if (selectedOps.Count <= 1) return false;

        int actualDelta = delta;

        // Calculate maximum allowed delta
        foreach (var op in selectedOps)
        {
            int currentVal = op.GetValue(propertyName);
            var bounds = op.GetBounds(propertyName);

            int minDelta = bounds.min - currentVal;
            int maxDelta = bounds.max - currentVal;

            if (actualDelta < minDelta) actualDelta = minDelta;
            if (actualDelta > maxDelta) actualDelta = maxDelta;
        }

        if (actualDelta == 0 && delta != 0)
        {
            // Re-sync UI for all selected ops since we blocked the change
            foreach (var op in selectedOps)
            {
                op.OnPropertyChanged(propertyName);
            }
            return true;
        }

        foreach (var op in selectedOps)
        {
            int currentVal = op.GetValue(propertyName);
            op.ApplyValueDirectly(propertyName, currentVal + actualDelta);
        }

        return true;
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

public delegate bool DeltaApplyHandler(FmOperatorViewModel sender, string propertyName, int delta, int requestedValue);

public class FmOperatorViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public DeltaApplyHandler? RequestDeltaApply;

    public void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public string Title { get; }

    private bool _isCarrier;
    public bool IsCarrier { get => _isCarrier; set { _isCarrier = value; OnPropertyChanged(); } }

    private bool _isMuted;
    public bool IsMuted { get => _isMuted; set { _isMuted = value; OnPropertyChanged(); } }

    private bool _isSolo;
    public bool IsSolo { get => _isSolo; set { _isSolo = value; OnPropertyChanged(); } }

    private bool _isSelected;
    public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }

    public FmOperatorViewModel(string title)
    {
        Title = title;
    }

    private bool SetValueWithDelta(ref int field, int value, int min, int max, [CallerMemberName] string? propertyName = null)
    {
        int clampedValue = Math.Clamp(value, min, max);
        if (field == clampedValue) return false;
        
        int delta = clampedValue - field;
        
        if (RequestDeltaApply != null && RequestDeltaApply(this, propertyName!, delta, clampedValue))
        {
            return true;
        }
        
        field = clampedValue;
        OnPropertyChanged(propertyName);
        return true;
    }

    private int _ar = 31;
    public int Ar { get => _ar; set => SetValueWithDelta(ref _ar, value, 0, 31); }

    private int _d1r;
    public int D1r { get => _d1r; set => SetValueWithDelta(ref _d1r, value, 0, 31); }

    private int _d2r;
    public int D2r { get => _d2r; set => SetValueWithDelta(ref _d2r, value, 0, 31); }

    private int _rr = 15;
    public int Rr { get => _rr; set => SetValueWithDelta(ref _rr, value, 0, 15); }

    private int _d1l;
    public int D1l { get => _d1l; set => SetValueWithDelta(ref _d1l, value, 0, 15); }

    private int _tl = 127;
    public int Tl { get => _tl; set => SetValueWithDelta(ref _tl, value, 0, 127); }

    private int _ks;
    public int Ks { get => _ks; set => SetValueWithDelta(ref _ks, value, 0, 3); }

    private int _mul = 1;
    public int Mul { get => _mul; set => SetValueWithDelta(ref _mul, value, 0, 15); }

    private int _dt1;
    public int Dt1 { get => _dt1; set => SetValueWithDelta(ref _dt1, value, 0, 7); }

    private int _dt2;
    public int Dt2 { get => _dt2; set => SetValueWithDelta(ref _dt2, value, 0, 3); }

    private int _ame;
    public int Ame { get => _ame; set => SetValueWithDelta(ref _ame, value, 0, 1); }

    public int GetValue(string propertyName)
    {
        return propertyName switch {
            nameof(Ar) => _ar, nameof(D1r) => _d1r, nameof(D2r) => _d2r, nameof(Rr) => _rr,
            nameof(D1l) => _d1l, nameof(Tl) => _tl, nameof(Ks) => _ks, nameof(Mul) => _mul,
            nameof(Dt1) => _dt1, nameof(Dt2) => _dt2, nameof(Ame) => _ame, _ => 0
        };
    }

    public (int min, int max) GetBounds(string propertyName)
    {
        return propertyName switch {
            nameof(Ar) or nameof(D1r) or nameof(D2r) => (0, 31),
            nameof(Rr) or nameof(D1l) or nameof(Mul) => (0, 15),
            nameof(Tl) => (0, 127),
            nameof(Ks) or nameof(Dt2) => (0, 3),
            nameof(Dt1) => (0, 7),
            nameof(Ame) => (0, 1),
            _ => (0, 0)
        };
    }

    public void ApplyValueDirectly(string propertyName, int value)
    {
        var bounds = GetBounds(propertyName);
        int clamped = Math.Clamp(value, bounds.min, bounds.max);
        switch (propertyName)
        {
            case nameof(Ar): _ar = clamped; break;
            case nameof(D1r): _d1r = clamped; break;
            case nameof(D2r): _d2r = clamped; break;
            case nameof(Rr): _rr = clamped; break;
            case nameof(D1l): _d1l = clamped; break;
            case nameof(Tl): _tl = clamped; break;
            case nameof(Ks): _ks = clamped; break;
            case nameof(Mul): _mul = clamped; break;
            case nameof(Dt1): _dt1 = clamped; break;
            case nameof(Dt2): _dt2 = clamped; break;
            case nameof(Ame): _ame = clamped; break;
        }
        OnPropertyChanged(propertyName);
    }

    public void Parse(string[] parts, int offset)
    {
        ApplyValueDirectly(nameof(Ar), int.Parse(parts[offset + 0].Trim()));
        ApplyValueDirectly(nameof(D1r), int.Parse(parts[offset + 1].Trim()));
        ApplyValueDirectly(nameof(D2r), int.Parse(parts[offset + 2].Trim()));
        ApplyValueDirectly(nameof(Rr), int.Parse(parts[offset + 3].Trim()));
        ApplyValueDirectly(nameof(D1l), int.Parse(parts[offset + 4].Trim()));
        ApplyValueDirectly(nameof(Tl), int.Parse(parts[offset + 5].Trim()));
        ApplyValueDirectly(nameof(Ks), int.Parse(parts[offset + 6].Trim()));
        ApplyValueDirectly(nameof(Mul), int.Parse(parts[offset + 7].Trim()));
        ApplyValueDirectly(nameof(Dt1), int.Parse(parts[offset + 8].Trim()));
        ApplyValueDirectly(nameof(Dt2), int.Parse(parts[offset + 9].Trim()));
        ApplyValueDirectly(nameof(Ame), int.Parse(parts[offset + 10].Trim()));
    }

    public string ToMml()
    {
        return $"{Ar}, {D1r}, {D2r}, {Rr}, {D1l}, {Tl}, {Ks}, {Mul}, {Dt1}, {Dt2}, {Ame}";
    }

    public void CopyFrom(FmOperatorViewModel other)
    {
        ApplyValueDirectly(nameof(Ar), other.Ar);
        ApplyValueDirectly(nameof(D1r), other.D1r);
        ApplyValueDirectly(nameof(D2r), other.D2r);
        ApplyValueDirectly(nameof(Rr), other.Rr);
        ApplyValueDirectly(nameof(D1l), other.D1l);
        ApplyValueDirectly(nameof(Tl), other.Tl);
        ApplyValueDirectly(nameof(Ks), other.Ks);
        ApplyValueDirectly(nameof(Mul), other.Mul);
        ApplyValueDirectly(nameof(Dt1), other.Dt1);
        ApplyValueDirectly(nameof(Dt2), other.Dt2);
        ApplyValueDirectly(nameof(Ame), other.Ame);
    }

    public string ToClipboardString()
    {
        return $"{Ar},{D1r},{D2r},{Rr},{D1l},{Tl},{Ks},{Mul},{Dt1},{Dt2},{Ame}";
    }

    public bool FromClipboardString(string str)
    {
        var parts = str.Split(',', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 11)
        {
            try
            {
                ApplyValueDirectly(nameof(Ar), int.Parse(parts[0].Trim()));
                ApplyValueDirectly(nameof(D1r), int.Parse(parts[1].Trim()));
                ApplyValueDirectly(nameof(D2r), int.Parse(parts[2].Trim()));
                ApplyValueDirectly(nameof(Rr), int.Parse(parts[3].Trim()));
                ApplyValueDirectly(nameof(D1l), int.Parse(parts[4].Trim()));
                ApplyValueDirectly(nameof(Tl), int.Parse(parts[5].Trim()));
                ApplyValueDirectly(nameof(Ks), int.Parse(parts[6].Trim()));
                ApplyValueDirectly(nameof(Mul), int.Parse(parts[7].Trim()));
                ApplyValueDirectly(nameof(Dt1), int.Parse(parts[8].Trim()));
                ApplyValueDirectly(nameof(Dt2), int.Parse(parts[9].Trim()));
                ApplyValueDirectly(nameof(Ame), int.Parse(parts[10].Trim()));
                return true;
            }
            catch { return false; }
        }
        return false;
    }
}
