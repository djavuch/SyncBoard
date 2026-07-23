using Microsoft.AspNetCore.Identity;

namespace SyncBoard.Entities;

public class User : IdentityUser<Guid>
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    
    public ICollection<Board> Boards { get; set; } = new List<Board>();
    public ICollection<Card> Cards { get; set; } = new List<Card>();
    
    private User() { }

    public User(string email, string firstName, string lastName)
    {
        Id = Guid.CreateVersion7();
        Email = email;
        UserName = email;
        FirstName = firstName;
        LastName = lastName;
    }
}