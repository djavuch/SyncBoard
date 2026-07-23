using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SyncBoard.Database;
using SyncBoard.Entities;
using SyncBoard.Hubs.Board;

namespace SyncBoard.Features.Cards.CreateCard;

public abstract record CreateCardRequest(string Title, Guid? ColumnId);

public record CreateCardResponse(Guid Id, string Title);

public record CreateCardCommand(string Title, Guid? ColumnId) : IRequest<CreateCardResponse>;

public static class CreateBoardEndpoint
{
    public static void MapCreateCard(this IEndpointRouteBuilder app)
    {
        app.MapPost("api/cards", async (CreateCardRequest request, ISender sender) =>
        {
            var command = new CreateCardCommand(request.Title, request.ColumnId);
            var result = await sender.Send(command);
            return Results.Ok(result);
        });
    }
}

public class CreateCardCommandHandler : IRequestHandler<CreateCardCommand, CreateCardResponse>
{
    private readonly AppDbContext _dbContext;
    private readonly IHubContext<BoardHub> _hub; 

    public CreateCardCommandHandler(AppDbContext dbContext, IHubContext<BoardHub> hub)
    {
        _dbContext = dbContext;
        _hub = hub;
    }

    public async Task<CreateCardResponse> Handle(CreateCardCommand request, CancellationToken ct)
    {
        var card = new Card(Guid.CreateVersion7(), request.Title, request.ColumnId);

        _dbContext.Cards.Add(card);
        await _dbContext.SaveChangesAsync(ct);
        
        Guid? boardId = null;
        if (request.ColumnId.HasValue)
        {
            var columnId = request.ColumnId.Value;
            boardId = await _dbContext.Columns
                .AsNoTracking()
                .Where(c => c.Id == columnId)
                .Select(c => c.BoardId)
                .FirstOrDefaultAsync(ct);

            if (boardId.HasValue)
            {
                await _hub.Clients.Group(boardId.Value.ToString())
                    .SendAsync("CardCreated", new CardCreatedEvent(
                        card.Id, card.Title, columnId, card.Position), ct);
            }
        }
        
        return new CreateCardResponse(card.Id, card.Title);
    }
}