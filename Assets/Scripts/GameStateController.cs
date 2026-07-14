namespace CoralCascade
{
    public enum GameState
    {
        Playing,
        Won,
        Lost
    }

    /// <summary>
    /// Win/lose state:
    ///   WIN  = board cleared of all bubbles.
    ///   LOSE = out of shots (evaluated only when quiescent), OR — on descending-pressure
    ///          levels — bubbles crossed the danger line (<see cref="LoseByPressure"/>,
    ///          immediate: the crossing itself is the failure, no quiescence needed).
    /// </summary>
    public class GameStateController
    {
        public GameState State { get; private set; } = GameState.Playing;
        public int ShotsRemaining { get; private set; }
        public int StartingShots { get; private set; }

        public void Reset(int shots)
        {
            StartingShots = shots;
            ShotsRemaining = shots;
            State = GameState.Playing;
        }

        public void ConsumeShot()
        {
            if (State != GameState.Playing) return;
            if (ShotsRemaining > 0) ShotsRemaining--;
        }

        /// <summary>
        /// Re-evaluate win/lose. Win is checked whenever the board might have been cleared
        /// (a secondary chain can clear it mid-fall); lose only once the table is quiet
        /// (no shot in flight, nothing falling), so a still-falling cascade that would
        /// clear the board is never beaten to the punch by a premature "Lost".
        /// </summary>
        public void Evaluate(bool boardCleared, bool quiescent)
        {
            if (State != GameState.Playing) return;

            if (boardCleared)
                State = GameState.Won;          // WIN: board cleared
            else if (quiescent && ShotsRemaining <= 0)
                State = GameState.Lost;         // LOSE: out of shots
        }

        /// <summary>Pressure loss: attached bubbles crossed the danger line.</summary>
        public void LoseByPressure()
        {
            if (State == GameState.Playing)
                State = GameState.Lost;
        }

        public void ForceState(GameState state) => State = state; // debug triggers
    }
}
