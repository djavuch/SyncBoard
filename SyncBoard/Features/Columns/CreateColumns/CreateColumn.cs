using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SyncBoard.Database;
using SyncBoard.Entities;
using SyncBoard.Hubs.Board;

namespace SyncBoard.Features.Columns.CreateColumns;

public sealed record CreateColumnRequest(string Title, int Position);
public record CreateColumnResponse(Guid Id, string Title, int Position, Guid BoardId);

public record CreateColumnCommand(Guid BoardId, string Title, int Position, Guid UserId) 
    : IRequest<IResult>;

public static class CreateColumnEndpoint
{
    public static void MapCreateColumn(this IEndpointRouteBuilder app)
    {
        app.MapPost("api/boards/{boardId:guid}/columns", async (
                Guid boardId,
                ClaimsPrincipal principal,
                CreateColumnRequest request,
                ISender sender) =>
            {
                var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var command = new CreateColumnCommand(boardId, request.Title, request.Position, userId);
                return await sender.Send(command);
            })
            .RequireAuthorization()
            .WithTags("Columns");
    }
}

public class CreateColumnCommandHandler : IRequestHandler<CreateColumnCommand, IResult>
{
    private readonly AppDbContext _dbContext;
    private readonly IHubContext<BoardHub> _hub;

    public CreateColumnCommandHandler(AppDbContext dbContext, IHubContext<BoardHub> hub)
    {
        _dbContext = dbContext;
        _hub = hub;
    }
    
    public async Task<IResult> Handle(CreateColumnCommand request, CancellationToken ct)
    {
        var board = await _dbContext.Boards
            .FirstOrDefaultAsync(b => b.Id == request.BoardId, ct);

        if (board is null)
            return Results.NotFound("Board not found.");

        if (board.OwnerId != request.UserId)
            return Results.Forbid();
        
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return Results.BadRequest(new
            {
                Error = "Column title is required."
            });
        }
        
        if (request.Position < 0)
        {
            return Results.BadRequest(new
            {
                Error = "Column position can't be negative."
            });
        }

        var column = new Column(
            Guid.CreateVersion7(),
            request.Title,
            request.Position,
            request.BoardId);

        _dbContext.Columns.Add(column);

        await _dbContext.SaveChangesAsync(ct);

        await _hub.Clients.Group(request.BoardId.ToString())
            .SendAsync("ColumnCreated", new ColumnCreatedEvent(column.Id, column.Title, column.Position), ct);
        
        return Results.Created(
            $"/api/columns/{column.Id}",
            new CreateColumnResponse(column.Id, column.Title, column.Position, column.BoardId));
    }
}
