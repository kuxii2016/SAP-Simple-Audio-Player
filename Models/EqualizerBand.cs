using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SAP.Models;

public class EqualizerBand : INotifyPropertyChanged
{
    private float _gain;
    private bool _isActive = true;

    public string Name { get; set; } = "";
    public float Frequency { get; set; }
    public float Bandwidth { get; set; } = 0.8f;

    public float Gain
    {
        get => _gain;
        set
        {
            if (SetProperty(ref _gain, Math.Clamp(value, -12f, 12f)))
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GainDisplay)));
        }
    }

    public bool IsActive { get => _isActive; set => SetProperty(ref _isActive, value); }
    public string GainDisplay => $"{Gain:+0.0;-0.0;0.0} dB";

    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
