using SyncBoard.Entities;

namespace SyncBoard.Entities;

public class Column
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    
    public int Position { get; set; }

    public Guid BoardId { get; init; }
    public Board Board { get; set; } = null!;
    
    public ICollection<Card> Cards { get; set; } = new List<Card>();
    
    private Column() { }

    public Column(Guid id, string title, int position, Guid boardId)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Column title can't be empty.");
        if (position < 0)
            throw new ArgumentException("Position can't be negative.");

        Id = id;
        Title = title;
        Position = position;
        BoardId = boardId;
    }

    public void UpdatePosition(int newPosition)
    {
        if (newPosition < 0)
            throw new ArgumentException("Position can't be negative");

        Position = newPosition;
    }
}