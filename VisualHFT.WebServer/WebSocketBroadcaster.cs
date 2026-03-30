using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;

namespace VisualHFT.WebServer;

public class WebSocketBroadcaster
{
    private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(WebSocketBroadcaster));

    private readonly ConcurrentDictionary<string, WebSocket> _clients = new();

    public void AddClient(string id, WebSocket ws)
    {
        _clients.TryAdd(id, ws);
        log.Info($"WebSocket client connected: {id}. Total clients: {_clients.Count}");
    }

    public void RemoveClient(string id)
    {
        _clients.TryRemove(id, out _);
        log.Info($"WebSocket client disconnected: {id}. Total clients: {_clients.Count}");
    }

    public int ClientCount => _clients.Count;

    public async Task BroadcastAsync(string json)
    {
        if (_clients.IsEmpty) return;

        var bytes = Encoding.UTF8.GetBytes(json);
        var segment = new ArraySegment<byte>(bytes);
        var deadClients = new List<string>();

        foreach (var (id, ws) in _clients)
        {
            try
            {
                if (ws.State == WebSocketState.Open)
                {
                    await ws.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
                }
                else
                {
                    deadClients.Add(id);
                }
            }
            catch
            {
                deadClients.Add(id);
            }
        }

        foreach (var id in deadClients)
        {
            RemoveClient(id);
        }
    }

    public async Task HandleClientAsync(WebSocket ws, CancellationToken ct)
    {
        var id = Guid.NewGuid().ToString();
        AddClient(id, ws);

        var buffer = new byte[1024];
        try
        {
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", ct);
                    break;
                }
            }
        }
        catch (WebSocketException) { }
        catch (OperationCanceledException) { }
        finally
        {
            RemoveClient(id);
        }
    }
}
