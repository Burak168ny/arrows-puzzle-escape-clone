namespace Arrows.Model
{
    public enum MoveOutcome
    {
        Exit,
        Blocked,
    }

    /// <summary>Result of launching an arrow along its head direction.</summary>
    public readonly struct MoveResult
    {
        public readonly MoveOutcome Outcome;
        public readonly int ArrowId;

        /// <summary>Free cells in front of the head before the grid edge (Exit) or the blocker (Blocked).</summary>
        public readonly int FreeCells;

        /// <summary>Cell that stopped the arrow. Only meaningful when Blocked.</summary>
        public readonly GridPos BlockedCell;

        /// <summary>Id of the arrow that was hit (can be the arrow itself if it loops into its own body).</summary>
        public readonly int BlockerId;

        private MoveResult(MoveOutcome outcome, int arrowId, int freeCells, GridPos blockedCell, int blockerId)
        {
            Outcome = outcome;
            ArrowId = arrowId;
            FreeCells = freeCells;
            BlockedCell = blockedCell;
            BlockerId = blockerId;
        }

        public bool IsExit => Outcome == MoveOutcome.Exit;
        public bool IsBlocked => Outcome == MoveOutcome.Blocked;

        public static MoveResult Exit(int arrowId, int freeCells) =>
            new MoveResult(MoveOutcome.Exit, arrowId, freeCells, default, GridModel.Empty);

        public static MoveResult Blocked(int arrowId, int freeCells, GridPos at, int blockerId) =>
            new MoveResult(MoveOutcome.Blocked, arrowId, freeCells, at, blockerId);

        public override string ToString() =>
            IsExit ? $"Exit(arrow {ArrowId}, free {FreeCells})"
                   : $"Blocked(arrow {ArrowId} by {BlockerId} at {BlockedCell}, free {FreeCells})";
    }
}
