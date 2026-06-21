using NAudio.Dsp;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using SAP.Models;

namespace SAP.Services;

public class AudioPlayerService : IDisposable
{
    private IWavePlayer? _outputDevice;
    private AudioFileReader? _audioFileReader;
    private VolumeSampleProvider? _volumeProvider;
    private PositionTracker? _positionTracker;
    private EqualizerSampleProvider? _eqProvider;
    private bool _eqEnabled;

    public event Action? SongFinished;

    public PlaybackState State => _outputDevice?.PlaybackState ?? PlaybackState.Stopped;

    public float Volume
    {
        get => _volumeProvider?.Volume ?? 0.8f;
        set { if (_volumeProvider != null) _volumeProvider.Volume = value; }
    }

    public TimeSpan Position
    {
        get => _positionTracker?.CurrentTime ?? _audioFileReader?.CurrentTime ?? TimeSpan.Zero;
        set
        {
            if (_audioFileReader != null)
            {
                _audioFileReader.CurrentTime = value;
                _positionTracker?.Reset(value);
            }
        }
    }

    public TimeSpan Duration => _audioFileReader?.TotalTime ?? TimeSpan.Zero;

    public bool IsEqualizerEnabled => _eqEnabled;

    public void Load(string filePath, List<EqualizerBand> eqBands)
    {
        Stop();
        _audioFileReader?.Dispose();
        _audioFileReader = new AudioFileReader(filePath);

        _volumeProvider = new VolumeSampleProvider(_audioFileReader) { Volume = _volumeProvider?.Volume ?? 0.8f };
        _positionTracker = new PositionTracker(_volumeProvider);
        _positionTracker.Ended += () => SongFinished?.Invoke();

        _eqProvider = _eqEnabled ? new EqualizerSampleProvider(_positionTracker, eqBands) : null;
    }

    public void Play()
    {
        if (_audioFileReader == null) return;
        if (_outputDevice?.PlaybackState == PlaybackState.Paused)
        {
            _outputDevice.Play();
            return;
        }

        _outputDevice?.Dispose();
        _outputDevice = new WasapiOut(NAudio.CoreAudioApi.AudioClientShareMode.Shared, false, 200);
        ISampleProvider final = _eqProvider ?? (ISampleProvider)_positionTracker!;
        _outputDevice.Init(final);
        _outputDevice.Play();
    }

    public void Pause() => _outputDevice?.Pause();

    public void Stop()
    {
        _outputDevice?.Stop();
        _outputDevice?.Dispose();
        _outputDevice = null;
    }

    public void EnableEqualizer(bool enable, List<EqualizerBand> bands)
    {
        _eqEnabled = enable;
        if (_audioFileReader == null) return;

        bool wasPlaying = State == PlaybackState.Playing;
        if (wasPlaying) Stop();

        _eqProvider = enable ? new EqualizerSampleProvider(_positionTracker!, bands) : null;

        if (wasPlaying) Play();
    }

    public void UpdateEqualizer(List<EqualizerBand> bands) => _eqProvider?.UpdateFilters(bands);

    public void Dispose()
    {
        Stop();
        _audioFileReader?.Dispose();
    }
}

public class PositionTracker : ISampleProvider
{
    private readonly ISampleProvider _source;
    private long _totalSamples;
    private bool _ended;

    public event Action? Ended;
    public WaveFormat WaveFormat => _source.WaveFormat;
    public TimeSpan CurrentTime => TimeSpan.FromSeconds((double)_totalSamples / WaveFormat.SampleRate / WaveFormat.Channels);

    public PositionTracker(ISampleProvider source) => _source = source;

    public void Reset(TimeSpan position)
    {
        _totalSamples = (long)(position.TotalSeconds * WaveFormat.SampleRate * WaveFormat.Channels);
        _ended = false;
    }

    public int Read(float[] buffer, int offset, int count)
    {
        var read = _source.Read(buffer, offset, count);
        if (read > 0)
            _totalSamples += read;
        else if (!_ended)
        {
            _ended = true;
            Ended?.Invoke();
        }
        return read;
    }
}

public class EqualizerSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly object _lock = new();
    private BiQuadFilter[,]? _filters;
    private int _channels;
    private int _sampleRate;
    private bool _anyBandActive;

    public WaveFormat WaveFormat => _source.WaveFormat;

    public EqualizerSampleProvider(ISampleProvider source, List<EqualizerBand> bands)
    {
        _source = source;
        UpdateFilters(bands);
    }

    public void UpdateFilters(List<EqualizerBand> bands)
    {
        lock (_lock)
        {
            _channels = _source.WaveFormat.Channels;
            _sampleRate = _source.WaveFormat.SampleRate;
            _anyBandActive = false;
            _filters = new BiQuadFilter[bands.Count, _channels];
            for (int b = 0; b < bands.Count; b++)
            {
                var band = bands[b];
                var gain = band.IsActive ? band.Gain : 0;
                if (Math.Abs(gain) > 0.01f) _anyBandActive = true;
                for (int c = 0; c < _channels; c++)
                    _filters[b, c] = BiQuadFilter.PeakingEQ(_sampleRate, band.Frequency, band.Bandwidth, band.IsActive ? band.Gain : 0);
            }
        }
    }

    public int Read(float[] buffer, int offset, int count)
    {
        var samplesRead = _source.Read(buffer, offset, count);
        if (!_anyBandActive || _filters == null) return samplesRead;

        lock (_lock)
        {
            for (int i = 0; i < samplesRead; i++)
            {
                int ch = i % _channels;
                for (int b = 0; b < _filters.GetLength(0); b++)
                    buffer[offset + i] = _filters[b, ch].Transform(buffer[offset + i]);
            }
        }
        return samplesRead;
    }
}
