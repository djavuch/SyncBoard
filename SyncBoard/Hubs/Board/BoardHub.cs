using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SyncBoard.Database;

namespace SyncBoard.Hubs.Board;

public class BoardHub : Hub
{
    private readonly AppDbContext _dbContext;

    public BoardHub(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    
    public async Task JoinBoard(string boardId)
    {
        if (!Guid.TryParse(boardId, out var parsedBoardId))
        {
            throw new HubException("Invalid board ID");
        }
        
        if(!Guid.TryParse(Context.UserIdentifier, out var userId))
        {
            throw new HubException("Invalid user ID");
        }
        
        var hasAccess = await _dbContext.Boards.AnyAsync(
            board => board.Id == parsedBoardId 
                     && board.OwnerId == userId,
            Context.ConnectionAborted);

        if (!hasAccess)
        {
            throw new HubException("Board not found or access denied.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, 
            parsedBoardId.ToString(),
            Context.ConnectionAborted);
    }

    public async Task LeaveBoard(string boardId)
    {
        if (!Guid.TryParse(boardId, out var parsedBoardId))
        {
            throw new HubException("Invalid board ID");
        }
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, 
            parsedBoardId.ToString(),
            Context.ConnectionAborted);
    }
}