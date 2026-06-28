using System.Net.WebSockets;
using System.Text;

namespace OrbitService.Game;

public sealed class GameWebSocketHandler
{
    private readonly MatchmakingQueue _queue = new();
    private readonly ILogger<GameWebSocketHandler> _logger;

    public GameWebSocketHandler(ILogger<GameWebSocketHandler> logger)
    {
        _logger = logger;
    }

    public async Task HandleAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        var playerId = Guid.NewGuid().ToString("N");
        var player = new ConnectedPlayer(playerId, socket);

        try
        {
            var joined = await WaitForJoinAsync(socket);
            if (!joined)
            {
                return;
            }

            await player.SendAsync(GameMessages.Serialize(new JoinedMessage
            {
                PlayerId = playerId,
                Status = "waiting",
            }));

            _queue.Enqueue(player);
            await TryCreateMatchAsync();

            await ReceiveLoopAsync(player, socket);
        }
        catch (WebSocketException ex)
        {
            _logger.LogDebug(ex, "WebSocket closed for player {PlayerId}", playerId);
        }
        finally
        {
            await HandleDisconnectAsync(player);
        }
    }

    private async Task<bool> WaitForJoinAsync(WebSocket socket)
    {
        var message = await ReceiveMessageAsync(socket);
        if (message is null)
        {
            return false;
        }

        var incoming = GameMessages.Deserialize(message);
        return incoming?.Type == "join";
    }

    private async Task TryCreateMatchAsync()
    {
        if (!_queue.TryDequeuePair(out var player1, out var player2) ||
            player1 is null ||
            player2 is null)
        {
            return;
        }

        var session = new GameSession(player1, player2);
        var startAt = DateTimeOffset.UtcNow.AddSeconds(3).ToUnixTimeMilliseconds();

        var matched1 = GameMessages.Serialize(new MatchedMessage
        {
            PlayerId = player1.PlayerId,
            OpponentId = player2.PlayerId,
            StartAt = startAt,
        });

        var matched2 = GameMessages.Serialize(new MatchedMessage
        {
            PlayerId = player2.PlayerId,
            OpponentId = player1.PlayerId,
            StartAt = startAt,
        });

        await Task.WhenAll(player1.SendAsync(matched1), player2.SendAsync(matched2));
        _logger.LogInformation(
            "Match created: {Player1} vs {Player2}, startAt={StartAt}",
            player1.PlayerId,
            player2.PlayerId,
            startAt);
    }

    private async Task ReceiveLoopAsync(ConnectedPlayer player, WebSocket socket)
    {
        while (socket.State == WebSocketState.Open)
        {
            var message = await ReceiveMessageAsync(socket);
            if (message is null)
            {
                break;
            }

            var incoming = GameMessages.Deserialize(message);
            if (incoming?.Type == "state" && player.Session is not null)
            {
                await player.Session.RelayStateAsync(player, incoming);
            }
        }
    }

    private async Task HandleDisconnectAsync(ConnectedPlayer player)
    {
        _queue.Remove(player);

        if (player.Session is not null)
        {
            await player.Session.NotifyOpponentDisconnectedAsync(player);
            player.Session.ClearSession();
        }
    }

    private static async Task<string?> ReceiveMessageAsync(WebSocket socket)
    {
        var buffer = new byte[4096];
        using var ms = new MemoryStream();

        while (true)
        {
            var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                if (socket.State == WebSocketState.Open ||
                    socket.State == WebSocketState.CloseReceived)
                {
                    await socket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Closing",
                        CancellationToken.None);
                }

                return null;
            }

            ms.Write(buffer, 0, result.Count);
            if (result.EndOfMessage)
            {
                break;
            }
        }

        return Encoding.UTF8.GetString(ms.ToArray());
    }
}
