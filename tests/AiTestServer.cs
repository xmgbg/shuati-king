using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

internal sealed class AiTestServer : IDisposable
{
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource stop = new();
    private readonly Task worker;
    public string Endpoint { get; }
    public int Status { get; set; } = 200;
    public string Body { get; set; } = """{"choices":[{"message":{"role":"assistant","content":"OK"}}]}""";
    public TaskCompletionSource<bool>? ResponseGate { get; set; }
    public int RequestCount { get; private set; }
    public string LastPath { get; private set; } = "";
    public string LastModel { get; private set; } = "";

    public AiTestServer()
    {
        listener.Start();
        Endpoint = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/v1";
        worker = Task.Run(Serve);
    }

    private async Task Serve()
    {
        try
        {
            while (!stop.IsCancellationRequested)
            {
                using var client = await listener.AcceptTcpClientAsync(stop.Token);
                using var stream = client.GetStream();
                var header = new List<byte>();
                var one = new byte[1];
                while (header.Count < 16384)
                {
                    await stream.ReadExactlyAsync(one, stop.Token);
                    header.Add(one[0]);
                    if (header.Count >= 4 && header.TakeLast(4).SequenceEqual(new byte[] { 13, 10, 13, 10 })) break;
                }
                var lines = Encoding.ASCII.GetString(header.ToArray()).Split("\r\n");
                LastPath = lines[0].Split(' ')[1];
                var length = int.Parse(lines.Single(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)).Split(':')[1]);
                var bytes = new byte[length];
                await stream.ReadExactlyAsync(bytes, stop.Token);
                using var json = JsonDocument.Parse(bytes);
                LastModel = json.RootElement.GetProperty("model").GetString()!;
                RequestCount++;
                if (ResponseGate is { } gate) await gate.Task.WaitAsync(stop.Token);
                var body = Encoding.UTF8.GetBytes(Body);
                var response = Encoding.ASCII.GetBytes($"HTTP/1.1 {Status} Test\r\nContent-Type: application/json\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(response, stop.Token);
                await stream.WriteAsync(body, stop.Token);
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
    }

    public void Dispose()
    {
        stop.Cancel(); listener.Stop();
        worker.GetAwaiter().GetResult();
        stop.Dispose();
    }
}
