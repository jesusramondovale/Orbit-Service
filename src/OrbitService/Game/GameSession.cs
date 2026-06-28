using System.Net.WebSockets;
using System.Text;

namespace OrbitService.Game;

public sealed class GameSession
{
    private readonly ConnectedPlayer _player1;
    private readonly ConnectedPlayer _player2;

    public GameSession(ConnectedPlayer player1, ConnectedPlayer player2)
    {
        _player1 = player1;
        _player2 = player2;
        _player1.Session = this;
        _player2.Session = this;
    }

    public ConnectedPlayer GetOpponent(ConnectedPlayer player) =>
        player.PlayerId == _player1.PlayerId ? _player2 : _player1;

    public async Task RelayStateAsync(ConnectedPlayer sender, IncomingMessage state)
    {
        var opponent = GetOpponent(sender);
        if (!opponent.IsOpen)
        {
            return;
        }

        var message = GameMessages.Serialize(new OpponentStateMessage
        {
            Angle = state.Angle,
            Speed = state.Speed,
            OnTrail = state.OnTrail,
            Crashed = state.Crashed,
        });

        await opponent.SendAsync(message);
    }

    public async Task NotifyOpponentDisconnectedAsync(ConnectedPlayer disconnected)
    {
        var opponent = GetOpponent(disconnected);
        if (!opponent.IsOpen)
        {
            return;
        }

        var message = GameMessages.Serialize(new OpponentDisconnectedMessage());
        await opponent.SendAsync(message);
    }

    public void ClearSession()
    {
        _player1.Session = null;
        _player2.Session = null;
    }
}

public sealed class ConnectedPlayer
{
    private readonly WebSocket _socket;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public ConnectedPlayer(string playerId, WebSocket socket)
    {
        PlayerId = playerId;
        _socket = socket;
    }

    public string PlayerId { get; }
    public GameSession? Session { get; set; }
    public bool IsOpen => _socket.State == WebSocketState.Open;

    public async Task SendAsync(string message)
    {
        if (!IsOpen)
        {
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(message);
        await _sendLock.WaitAsync();
        try
        {
            if (_socket.State == WebSocketState.Open)
            {
                await _socket.SendAsync(
                    bytes,
                    WebSocketMessageType.Text,
                    endOfMessage: true,
                    CancellationToken.None);
            }
        }
        finally
        {
            _sendLock.Release();
        }
    }
}
