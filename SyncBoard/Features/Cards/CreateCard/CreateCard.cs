using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using SyncBoard.Database;
using SyncBoard.Entities;
using SyncBoard.Hubs.Board;

namespace SyncBoard.Features.Cards.CreateCard;

public sealed record CreateCardRequest(string Title, Guid ColumnId);

public record CreateCardResponse(Guid Id, string Title, Guid ColumnId, int Position);

public record CreateCardCommand(string Title, Guid ColumnId, Guid UserId) : IRequest<IResult>;

public static class CreateBoardEndpoint
{
    public static void MapCreateCard(this IEndpointRouteBuilder app)
    {
        app.MapPost("api/cards", async (
                ClaimsPrincipal principal,
                CreateCardRequest request,
                ISender sender) =>
        {
            var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var command = new CreateCardCommand(request.Title, request.ColumnId, userId);
            return await sender.Send(command);
        })
        .RequireAuthorization()
        .WithTags("Cards");
    }
}

public class CreateCardCommandHandler : IRequestHandler<CreateCardCommand, IResult>
{
    private readonly AppDbContext _dbContext;
    private readonly IHubContext<BoardHub> _hub; 

    public CreateCardCommandHandler(AppDbContext dbContext, IHubContext<BoardHub> hub)
    {
        _dbContext = dbContext;
        _hub = hub;
    }

    public async Task<IResult> Handle(CreateCardCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return Results.BadRequest(new
            {
                Error = "Card title is required."
            });
        }

        if (request.ColumnId == Guid.Empty)
            return Results.BadRequest("ColumnId is required.");

        var column = await _dbContext.Columns
            .Include(c => c.Board)
            .FirstOrDefaultAsync(c => c.Id == request.ColumnId, ct);

        if (column is null)
            return Results.NotFound("Column not found.");

        if (column.Board.OwnerId != request.UserId)
            return Results.Forbid();

        var card = new Card(Guid.CreateVersion7(), request.Title, request.ColumnId);

        _dbContext.Cards.Add(card);
        await _dbContext.SaveChangesAsync(ct);

        await _hub.Clients.Group(column.BoardId.ToString())
            .SendAsync("CardCreated", new CardCreatedEvent(
                card.Id, card.Title, card.ColumnId, card.Position), ct);

        return Results.Ok(new
        {
            Id = card.Id, 
            Title = card.Title
        });
    }
}
