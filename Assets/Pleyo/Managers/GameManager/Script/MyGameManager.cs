using UnityEngine;

/// <summary>
/// Game manager. Game states are driven by the template's hooks (GameLoading_*, GamePlaying_*, GameOver_*).
/// Elves' Workshop: 10 s tutorial + 3 s countdown + 2 min of play, then ElfGame shows its results and quits.
/// </summary>
public class MyGameManager : MMGameManager
{
    protected override void Awake()
    {
        // No template leaderboard scene: the game shows its results itself.
        LeaderboardSceneName = "";
        base.Awake();
    }

    protected override void Start()
    {
        ElfGame game = FindAnyObjectByType<ElfGame>();
        GameDuration = game != null ? game.GetTotalGameDuration() : 133f;
        base.Start();
    }

    protected override void GameOver_Start()
    {
        if (ElfGame.Instance != null)
            ElfGame.Instance.ApplyScoresToTemplate(this);
        // Skip the template's end countdown: ElfGame shows the results then quits.
        _TMP_MMGameOver = true;
        base.GameOver_Start();
    }
}
