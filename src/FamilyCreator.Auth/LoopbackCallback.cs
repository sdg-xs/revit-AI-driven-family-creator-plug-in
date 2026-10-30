using System.Net;
using System.Net.Sockets;
using System.Text;

namespace FamilyCreator.Auth;

public sealed class LoopbackCallback : IDisposable
{
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    public string RedirectUri { get; }

    public LoopbackCallback()
    {
        listener.Start();
        RedirectUri = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/auth/callback";
    }

    public async Task<AuthorizationCode> WaitAsync(string state, string? existingClient, CancellationToken cancellationToken)
    {
        while (true)
        {
            using var client = await listener.AcceptTcpClientAsync(cancellationToken);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            var stream = client.GetStream();
            var bytes = new List<byte>();
            var single = new byte[1];
            try
            {
                while (bytes.Count < 16384)
                {
                    if (await stream.ReadAsync(single, timeout.Token) == 0) break;
                    bytes.Add(single[0]);
                    if (bytes.Count >= 4 && bytes[^4] == 13 && bytes[^3] == 10 && bytes[^2] == 13 && bytes[^1] == 10) break;
                }
                var request = Encoding.ASCII.GetString(bytes.ToArray()).Split("\r\n")[0].Split(' ');
                var target = request.Length == 3 && request[0] == "GET" ? request[1] : "";
                if (!target.StartsWith("/auth/callback?", StringComparison.Ordinal))
                {
                    await ReplyAsync(stream, "404 Not Found", "Not found.", timeout.Token);
                    continue;
                }
                AuthorizationCode code;
                try { code = OAuthProtocol.ValidateCallback(target["/auth/callback".Length..], state, existingClient); }
                catch (AuthException)
                {
                    await ReplyAsync(stream, "400 Bad Request", "Sign-in could not be completed. Return to Family Creator and try again.", timeout.Token);
                    throw;
                }
                await ReplyAsync(stream, "200 OK", "Sign-in response received. Return to Family Creator to check the connection.", timeout.Token);
                return code;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { }
            catch (IOException) { }
        }
    }

    private static async Task ReplyAsync(NetworkStream stream, string status, string message, CancellationToken token)
    {
        var body = Encoding.UTF8.GetBytes(message);
        var header = Encoding.ASCII.GetBytes($"HTTP/1.1 {status}\r\nContent-Type: text/plain; charset=utf-8\r\nCache-Control: no-store\r\nContent-Security-Policy: default-src 'none'\r\nConnection: close\r\nContent-Length: {body.Length}\r\n\r\n");
        await stream.WriteAsync(header, token);
        await stream.WriteAsync(body, token);
    }

    public void Dispose() => listener.Stop();
}
