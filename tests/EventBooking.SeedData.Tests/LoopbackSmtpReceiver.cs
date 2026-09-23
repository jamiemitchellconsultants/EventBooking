using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using MimeKit;

namespace EventBooking.SeedData.Tests;

/// <summary>Captures messages from the actual SMTP client in disposable host tests.</summary>
internal sealed class LoopbackSmtpReceiver : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _worker;

    /// <summary>Starts a test receiver on an unused loopback port.</summary>
    public LoopbackSmtpReceiver()
    {
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _worker = ReceiveAsync(_stop.Token);
    }

    /// <summary>Gets the port to inject into the seed subprocess.</summary>
    public int Port { get; }
    /// <summary>Gets accepted MIME messages after the SMTP DATA phase.</summary>
    public ConcurrentQueue<MimeMessage> Messages { get; } = new();
    /// <summary>Gets or sets whether the provider rejects DATA before accepting a message.</summary>
    public bool RejectMessages { get; set; }

    private async Task ReceiveAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                using var client = await _listener.AcceptTcpClientAsync(cancellationToken);
                await using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.UTF8, false, 1024, leaveOpen: true);
                await using var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, leaveOpen: true)
                {
                    NewLine = "\r\n",
                    AutoFlush = true,
                };
                await writer.WriteLineAsync("220 localhost test SMTP ready");
                while (await reader.ReadLineAsync(cancellationToken) is { } command)
                {
                    if (command.StartsWith("QUIT", StringComparison.OrdinalIgnoreCase))
                    {
                        await writer.WriteLineAsync("221 Bye");
                        break;
                    }
                    if (command.StartsWith("DATA", StringComparison.OrdinalIgnoreCase))
                    {
                        if (RejectMessages)
                        {
                            await writer.WriteLineAsync("550 Test provider rejection");
                            continue;
                        }
                        await writer.WriteLineAsync("354 End with a single dot");
                        var data = new StringBuilder();
                        while (await reader.ReadLineAsync(cancellationToken) is { } line && line != ".")
                            data.Append(line.StartsWith("..", StringComparison.Ordinal) ? line[1..] : line)
                                .Append("\r\n");
                        using var bytes = new MemoryStream(Encoding.UTF8.GetBytes(data.ToString()));
                        Messages.Enqueue(await MimeMessage.LoadAsync(bytes, cancellationToken));
                        await writer.WriteLineAsync("250 Message accepted");
                    }
                    else
                    {
                        await writer.WriteLineAsync("250 localhost");
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    /// <summary>Stops the receiver and observes any unexpected protocol failure.</summary>
    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        try { await _worker; }
        finally
        {
            _listener.Stop();
            _stop.Dispose();
            foreach (var message in Messages) message.Dispose();
        }
    }
}
