using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SyncBoard.Database;
using SyncBoard.Hubs.Board;

namespace SyncBoard.Features.Cards.DeleteCard;

public record DeleteCardCommand(Guid CardId, Guid UserId) : IRequest<IResult>;

public static class DeleteCardEndpoint
{
    public static void MapDeleteCard(this IEndpointRouteBuilder app)
    {
        app.MapDelete("api/cards/{cardId:guid}", async (
                Guid cardId,
                ClaimsPrincipal principal,
                ISender sender) =>
            {
                var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
                return await sender.Send(new DeleteCardCommand(cardId, userId));
            })
            .RequireAuthorization()
            .WithTags("Cards");
    }
}

public class DeleteCardCommandHandler : IRequestHandler<DeleteCardCommand, IResult>
{
    private readonly AppDbContext _dbContext;
    private readonly IHubContext<BoardHub> _hub;

    public DeleteCardCommandHandler(AppDbContext dbContext, IHubContext<BoardHub> hub)
    {
        _dbContext = dbContext;
        _hub = hub;
    }

    public async Task<IResult> Handle(DeleteCardCommand request, CancellationToken ct)
    {
        var card = await _dbContext.Cards
            .Include(c => c.Column).ThenInclude(col => col.Board)
            .FirstOrDefaultAsync(c => c.Id == request.CardId, ct);

        if (card is null)
            return Results.NotFound("Card not found.");

        if (card.Column?.Board?.OwnerId != request.UserId)
            return Results.Forbid();

        _dbContext.Cards.Remove(card);
        await _dbContext.SaveChangesAsync(ct);

        await _hub.Clients.Group(card.Column.Board.Id.ToString())
            .SendAsync("CardDeleted", new CardDeletedEvent(card.Id), ct);
        
        return Results.NoContent();
    }
}