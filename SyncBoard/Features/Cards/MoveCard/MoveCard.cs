using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SyncBoard.Database;
using SyncBoard.Hubs.Board;

namespace SyncBoard.Features.Cards.MoveCard;

public sealed record MoveCardRequest(Guid ToColumnId, int Position);

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

        var boardId = card.Column.Board.Id;

        if (card.Column.Board.OwnerId != request.UserId)
            return Results.Forbid();
        
        var board = card.Column.Board;

        var fromColumnId = card.ColumnId;

        var targetColumn = await _dbContext.Columns
            .AsNoTracking()
            .Include(c => c.Board)
            .FirstOrDefaultAsync(c => c.Id == request.ToColumnId, ct);
        
        if (targetColumn is null)
            return Results.NotFound("Target column not found.");

        if (targetColumn.Board.Id != boardId)
            return Results.BadRequest("Target column is on a different board.");
        
        if (request.Position < 0)
        {
            return Results.BadRequest("Position can't be negative.");
        }

// Загружаем карточки исходной колонки в текущем порядке.
// Они отслеживаются EF Core, поскольку будем менять их позиции.
        var sourceCards = await _dbContext.Cards
            .Where(c => c.ColumnId == fromColumnId)
            .OrderBy(c => c.Position)
            .ThenBy(c => c.Id)
            .ToListAsync(ct);

// Убираем карточку только из списка в памяти.
// Из базы данных её не удаляем.
        sourceCards.Remove(card);

// При перемещении внутри колонки используем тот же список.
        var targetCards = sourceCards;

// При переносе в другую колонку загружаем её карточки отдельно.
        if (fromColumnId != request.ToColumnId)
        {
            targetCards = await _dbContext.Cards
                .Where(c => c.ColumnId == request.ToColumnId)
                .OrderBy(c => c.Position)
                .ThenBy(c => c.Id)
                .ToListAsync(ct);
        }

// Вставка по индексу Count разрешена: это добавление в конец.
// Больше Count — уже несуществующее место.
        if (request.Position > targetCards.Count)
        {
            return Results.BadRequest(
                $"Position must be between 0 and {targetCards.Count}.");
        }

// Вставляем карточку на выбранное место.
        targetCards.Insert(request.Position, card);

// Обновляем принадлежность карточки к колонке.
        card.ColumnId = request.ToColumnId;

// Пересчитываем позиции исходной колонки.
        for (var i = 0; i < sourceCards.Count; i++)
        {
            sourceCards[i].Position = i;
        }

// При переносе между колонками пересчитываем и целевую.
// Внутри одной колонки списки совпадают — первый цикл уже всё сделал.
        if (fromColumnId != request.ToColumnId)
        {
            for (var i = 0; i < targetCards.Count; i++)
            {
                targetCards[i].Position = i;
            }
        }
        
        board.UpdateVersion();

// Сохраняем все изменённые карточки одним вызовом.
        await _dbContext.SaveChangesAsync(ct);

        await _hub.Clients.Group(boardId.ToString())
            .SendAsync("CardMoved", new CardMovedEvent(
                card.Id,
                fromColumnId,
                request.ToColumnId,
                request.Position), ct);

        return Results.Ok(new MoveCardResponse(card.Id, request.ToColumnId, request.Position));
    }
}
