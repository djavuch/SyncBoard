using System.Security.Claims;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SyncBoard.Database;

namespace SyncBoard.Features.Cards.GetCardsByColumn;

public record CardDto(Guid Id, string Title, int Position);
public record GetCardsByColumnResponse(Guid ColumnId, List<CardDto> Cards);

public record GetCardByColumnQuery(Guid ColumnId, Guid UserId) : IRequest<IResult>;

public static class GetCardByColumnEndpoint
{
    public static void MapGetCardByColumn(this IEndpointRouteBuilder app)
    {
        app.MapGet("api/columns/{columnId:guid}/cards", async (
                Guid columnId,
                ClaimsPrincipal principal,
                ISender sender) =>
            {
                var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
                return await sender.Send(new GetCardByColumnQuery(columnId, userId));
            })
            .RequireAuthorization()
            .WithTags("Card");
    }
}

public class GetCardsByColumnQueryHandler : IRequestHandler<GetCardByColumnQuery, IResult>
{
    private readonly AppDbContext _dbContext;

    public GetCardsByColumnQueryHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IResult> Handle(GetCardByColumnQuery request, CancellationToken ct)
    {
        var column = await _dbContext.Columns
            .AsNoTracking()
            .Include(c => c.Board)
            .FirstOrDefaultAsync(c => c.Id == request.ColumnId, ct);

        if (column is null)
            return Results.NotFound("Column not found.");
        
        if (column.Board.OwnerId != request.UserId)
            return Results.Forbid();

        var cards = await _dbContext.Cards
            .AsNoTracking()
            .Where(c => c.ColumnId == request.ColumnId)
            .Select(c => new CardDto(c.Id, c.Title, c.Position))
            .ToListAsync(ct);
        
        return Results.Ok(new GetCardsByColumnResponse(request.ColumnId, cards));

    }
}