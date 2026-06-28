namespace OrbitService.Game;

public sealed class MatchmakingQueue
{
    private readonly Queue<ConnectedPlayer> _waiting = new();
    private readonly object _lock = new();

    public void Enqueue(ConnectedPlayer player)
    {
        lock (_lock)
        {
            _waiting.Enqueue(player);
        }
    }

    public bool TryDequeuePair(out ConnectedPlayer? player1, out ConnectedPlayer? player2)
    {
        lock (_lock)
        {
            if (_waiting.Count < 2)
            {
                player1 = null;
                player2 = null;
                return false;
            }

            player1 = _waiting.Dequeue();
            player2 = _waiting.Dequeue();
            return true;
        }
    }

    public void Remove(ConnectedPlayer player)
    {
        lock (_lock)
        {
            var remaining = new Queue<ConnectedPlayer>();
            while (_waiting.Count > 0)
            {
                var current = _waiting.Dequeue();
                if (current.PlayerId != player.PlayerId)
                {
                    remaining.Enqueue(current);
                }
            }

            while (remaining.Count > 0)
            {
                _waiting.Enqueue(remaining.Dequeue());
            }
        }
    }
}
