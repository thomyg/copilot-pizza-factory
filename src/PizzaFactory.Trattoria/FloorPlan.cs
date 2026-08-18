namespace PizzaFactory.Trattoria;

public enum TableShape
{
    Round,
    Square,
    Rect,
}

/// <summary>A table on the floor map. Coordinates are percentages of the floor container.</summary>
public sealed record Table(int Id, int Seats, TableShape Shape, double X, double Y, double W, double H);

/// <summary>
/// The dining room: 17 tables, mixed sizes and shapes — cozy rounds at the window, squares in
/// the middle, the long family table centre stage, and the big corner booth. Hand-placed so the
/// map reads like a real trattoria, not a spreadsheet.
/// </summary>
public static class FloorPlan
{
    public static IReadOnlyList<Table> Tables { get; } =
    [
        // Window row (top) — the people-watching seats.
        new(1, 2, TableShape.Round, 3, 4, 8, 13),
        new(2, 2, TableShape.Round, 15, 4, 8, 13),
        new(3, 2, TableShape.Round, 27, 4, 8, 13),
        new(4, 4, TableShape.Square, 39, 3, 11, 16),
        new(5, 4, TableShape.Square, 55, 3, 11, 16),
        new(6, 2, TableShape.Round, 71, 4, 8, 13),
        new(7, 6, TableShape.Rect, 83, 3, 14, 16),

        // Middle of the room — the long family table is the heart of the house.
        new(8, 4, TableShape.Square, 4, 30, 11, 16),
        new(9, 8, TableShape.Rect, 22, 29, 26, 18),
        new(10, 6, TableShape.Rect, 55, 30, 16, 16),
        new(11, 4, TableShape.Square, 79, 30, 11, 16),

        // Lower middle.
        new(12, 2, TableShape.Round, 4, 57, 8, 13),
        new(13, 4, TableShape.Square, 17, 56, 11, 16),
        new(14, 4, TableShape.Square, 34, 56, 11, 16),
        new(15, 6, TableShape.Rect, 51, 56, 16, 16),
        new(16, 2, TableShape.Round, 74, 57, 8, 13),

        // The corner booth by the kitchen door — regulars only (they claim).
        new(17, 6, TableShape.Rect, 60, 80, 20, 15),
    ];

    public static Table Get(int id) => Tables.First(t => t.Id == id);
}
