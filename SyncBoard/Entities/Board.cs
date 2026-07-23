namespace SyncBoard.Entities;

public class Board
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public Guid? OwnerId { get; private set; }
    public User? Owner { get; set; }

    public ICollection<Column> Columns { get; init; } = new List<Column>();
    
    private Board() { }

    public Board(Guid id, string title, Guid? ownerId)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Board title can't be empty.");

        Id = id;
        Title = title;
        OwnerId = ownerId;
    }

    public void UpdateTitle(string newTitle)
    {
        if (string.IsNullOrWhiteSpace(newTitle))
            throw new ArgumentException("Board title can't be empty.");
        
        Title = newTitle;
    }
}