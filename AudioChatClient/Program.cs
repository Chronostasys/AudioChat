// See https://aka.ms/new-console-template for more information
using AudioChatClient;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using NAudio.Dsp;
using NAudio.Wave;
using System.Collections.Concurrent;
using System.Threading.Channels;

while (true)
{
    var _logger = new Logger<Program>(LoggerFactory.Create((b) =>
    {
        b.SetMinimumLevel(LogLevel.Debug);
        b.AddConsole();
    }));
    var dic = new ConcurrentDictionary<string, FilteredProvider>();
    var wo = new DirectSoundOut(100);
    TaskCompletionSource tsksrc = new();
    wo.PlaybackStopped += (sender, args) =>
    {
        _logger.LogError("play sound error: {}, try restart", args.Exception);
        tsksrc.TrySetResult();
    };
    MixingWaveProvider32 mixprovider = new();
    var capture = new WaveInEvent
    {
        WaveFormat = new WaveFormat(16000, 1),
        BufferMilliseconds = 100
    };
    var connection = new HubConnectionBuilder()
        .WithUrl("http://localhost:5000/ChatHub")
        .Build();
    connection.Closed += async (error) =>
    {
        if (error!=null)
        {
            _logger.LogError("signalr conn lost, err: {}", error);
            await connection.StartAsync();
        }
    };
    connection.On<string>("Disconnect", (user) =>
    {
        _logger.LogInformation("user {} leaves the chat", user);
        dic.TryRemove(user, out var provider);
        lock (mixprovider)
        {
            mixprovider.RemoveInputStream(provider);
        }
    });
    connection.On<string, byte[]>("ReceiveMessage", (user, message) =>
    {
        if (dic.TryGetValue(user, out var provider))
        {
            provider.AddSamples(message, 0, message.Length);
        }
        else
        {
            _logger.LogInformation("start receiving live audio stream from user {}", user);
            provider = new(new(16000, 1)) { DiscardOnBufferOverflow = true };
            dic[user] = provider;
            lock (mixprovider)
            {
                mixprovider.AddInputStream(new Wave16ToFloatProvider(provider));
            }
            provider.AddSamples(message, 0, message.Length);
        }
        if (provider.BufferedDuration.TotalMilliseconds > 320)
        {
            _logger.LogWarning("high latency detected({}ms), try to catch on the live audio stream...", provider.BufferedDuration.TotalMilliseconds);
            provider.ClearBuffer();
        }
        lock (mixprovider)
        {
            if (wo.PlaybackState is not PlaybackState.Playing)
            {
                wo.Init(mixprovider);
                wo.Play();
            }
        }
    });


    await connection.StartAsync();
    var channel = Channel.CreateBounded<byte[]>(1000);
    capture.DataAvailable += async (object? sender, WaveInEventArgs e) =>
    {
        await channel.Writer.WriteAsync(e.Buffer[..e.BytesRecorded]);
    };
    capture.RecordingStopped += (object? sender, StoppedEventArgs args) =>
    {
        _logger.LogError("record error: {}, try restart", args.Exception);
        tsksrc.TrySetResult();
    };
    await connection.SendAsync("SendMessage", channel.Reader);
    capture.StartRecording();
    _logger.LogInformation("voice record started");
    await tsksrc.Task;
    await connection.DisposeAsync();
    capture.Dispose();
    wo.Dispose();
    channel.Writer.Complete();
}