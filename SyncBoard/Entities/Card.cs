namespace SyncBoard.Entities;

public class Card
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    public int Position { get; set; }
    public Guid? ColumnId { get; set; }
    public Column Column { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public Guid? CreatedById { get; private set; }
    public User? CreatedBy { get; private set; }
    
    private Card() {}

    public Card(Guid id, string title, Guid? columnId)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Card title can't be empty.");

        Id = id;
        Title = title;
        ColumnId = columnId;
    }

    public void UpdateTitle(string newTitle)
    {
        if (string.IsNullOrWhiteSpace(newTitle))
            throw new ArgumentException("Card title can't be empty.");

        Title = newTitle;
    }
}