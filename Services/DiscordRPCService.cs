using DiscordRPC;
using DiscordRPC.Message;

namespace SAP.Services;

public class DiscordRPCService : IDisposable
{
    private const string AppId = "791892336140746773";
    private readonly DiscordRpcClient _client;
    private bool _initialized;

    public DiscordRPCService()
    {
        _client = new DiscordRpcClient(AppId);
        _client.OnReady += (_, msg) => Console.WriteLine($"Discord RPC ready: {msg.User.Username}");
        _client.OnError += (_, msg) => Console.WriteLine($"Discord RPC error: {msg.Message}");
    }

    public void Initialize()
    {
        if (_initialized) return;
        _client.Initialize();
        _initialized = true;
    }

    public void UpdatePresence(string? title, string? artist, TimeSpan? position, TimeSpan? duration, bool isPaused)
    {
        if (!_initialized) Initialize();

        if (string.IsNullOrEmpty(title))
        {
            _client.ClearPresence();
            return;
        }

        var elapsed = position.HasValue ? DateTime.UtcNow - position.Value : DateTime.UtcNow;
        var timestamps = duration.HasValue && duration.Value.TotalSeconds > 0
            ? Timestamps.FromTimeSpan(duration.Value)
            : null;

        var state = $"by {artist ?? "Unknown Artist"}";

        _client.SetPresence(new RichPresence
        {
            Details = title,
            State = state,
            Timestamps = timestamps,
            Assets = new Assets
            {
                LargeImageKey = "audio",
                LargeImageText = "SAP - Simple Audio Player"
            }
        });
    }

    public void ClearPresence()
    {
        if (_initialized)
            _client.ClearPresence();
    }

    public void Dispose()
    {
        if (_initialized)
        {
            _client.ClearPresence();
            _client.Dispose();
        }
    }
}
