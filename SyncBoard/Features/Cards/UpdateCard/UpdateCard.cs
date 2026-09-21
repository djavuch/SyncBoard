using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SyncBoard.Database;
using SyncBoard.Entities;
using SyncBoard.Hubs.Board;

namespace SyncBoard.Features.Cards.UpdateCard;

public sealed record UpdateCardRequest(string? Title);

public record UpdateCardResponse(Guid Id, string Title);

public record UpdateCardCommand(Guid CardId, string Title, Guid UserId) : IRequest<IResult>;

public static class UpdateCareEndpoint
{
    public static void MapUpdateCard(this IEndpointRouteBuilder app)
    {
        app.MapPatch("api/cards/{cardId:guid}", async (
                Guid cardId,
                ClaimsPrincipal principal,
                UpdateCardRequest request,
                ISender sender) =>
            {
                var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
                return await sender.Send(new UpdateCardCommand(cardId, request.Title ?? string.Empty, userId));
            })
            .RequireAuthorization()
            .WithTags("Cards");
    }
}

public class UpdateCardCommandHandler : IRequestHandler<UpdateCardCommand, IResult>
{
    private readonly AppDbContext _dbContext;
    private readonly IHubContext<BoardHub> _hub; 

    public UpdateCardCommandHandler(AppDbContext dbContext, IHubContext<BoardHub> hub)
    {
        _dbContext = dbContext;
        _hub = hub;
    }

    public async Task<IResult> Handle(UpdateCardCommand request, CancellationToken ct)
    {
        var card = await _dbContext.Cards
            .Include(c => c.Column)
            .ThenInclude(col => col.Board)
            .FirstOrDefaultAsync(c => c.Id == request.CardId, ct);

        if (card is null)
            return Results.NotFound("Card not found.");
        
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return Results.BadRequest(new
            {
                Error = "Card title is required."
            });
        }
        
        if (card.Column?.Board?.OwnerId != request.UserId)
            return Results.Forbid();
        
        card.UpdateTitle(request.Title);
        await _dbContext.SaveChangesAsync(ct);

        await _hub.Clients.Group(card.Column.Board.Id.ToString())
            .SendAsync("CardUpdated", new CardUpdatedEvent(card.Id, card.Title), ct);
        
        return Results.Ok(new UpdateCardResponse(card.Id, card.Title));
    }
}