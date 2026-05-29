using Fusion;

public enum GamePhase
{
    Waiting,
    Playing
}

public class NetworkGameState : NetworkBehaviour
{
    [Networked]
    public GamePhase Phase { get; set; }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            Phase = GamePhase.Waiting;
        }
    }

    public void SetWaiting()
    {
        if (!Object.HasStateAuthority)
            return;

        Phase = GamePhase.Waiting;
    }

    public void SetPlaying()
    {
        if (!Object.HasStateAuthority)
            return;

        Phase = GamePhase.Playing;
    }

    public bool IsPlaying()
    {
        return Phase == GamePhase.Playing;
    }
}