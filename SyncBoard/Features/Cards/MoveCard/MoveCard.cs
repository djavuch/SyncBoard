using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SyncBoard.Database;
using SyncBoard.Hubs.Board;

namespace SyncBoard.Features.Cards.MoveCard;

public abstract record MoveCardRequest(Guid ToColumnId, int Position);

public record MoveCardResponse(Guid CardId, Guid ToColumnId, int Position);

public record MoveCardCommand(Guid CardId, Guid ToColumnId, int Position, Guid UserId)
    : IRequest<IResult>;

public static class MoveCardEndpoint
{
    public static void MapMoveCard(this IEndpointRouteBuilder app)
    {
        app.MapPost("api/cards/{cardId:guid}/move", async (
                Guid cardId,
                ClaimsPrincipal principal,
                MoveCardRequest request,
                ISender sender) =>
            {
                var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
                return await sender.Send(new MoveCardCommand(
                    cardId, request.ToColumnId, request.Position, userId));
            })
            .RequireAuthorization()
            .WithTags("Cards");
    }
}

public class MoveCardCommandHandler : IRequestHandler<MoveCardCommand, IResult>
{
    private readonly AppDbContext _dbContext;
    private readonly IHubContext<BoardHub> _hub;

    public MoveCardCommandHandler(AppDbContext dbContext, IHubContext<BoardHub> hub)
    {
        _dbContext = dbContext;
        _hub = hub;
    }

    public async Task<IResult> Handle(MoveCardCommand request, CancellationToken ct)
    {
        var card = await _dbContext.Cards
            .Include(c => c.Column)
            .ThenInclude(col => col.Board)
            .FirstOrDefaultAsync(c => c.Id == request.CardId, ct);

        if (card is null)
            return Results.NotFound("Card not found.");

        var boardId = card.Column?.Board?.Id;

        if (boardId is null || card.Column?.Board?.OwnerId != request.UserId)
            return Results.Forbid();

        var fromColumnId = card.ColumnId;

        var targetColumn = await _dbContext.Columns
            .AsNoTracking()
            .Include(c => c.Board)
            .FirstOrDefaultAsync(c => c.Id == request.ToColumnId, ct);
        
        if (targetColumn is null)
            return Results.NotFound("Target column not found.");

        if (targetColumn.Board.Id != boardId)
            return Results.BadRequest("Target column is on a different board.");
        
        // Moving cards
        card.ColumnId = request.ToColumnId;
        card.Position = request.Position;
        await _dbContext.SaveChangesAsync(ct);

        await _hub.Clients.Group(boardId.Value.ToString())
            .SendAsync("CardMoved", new CardMovedEvent(
                card.Id,
                fromColumnId!.Value,
                request.ToColumnId,
                request.Position), ct);

        return Results.Ok(new MoveCardResponse(card.Id, request.ToColumnId, request.Position));
    }
}