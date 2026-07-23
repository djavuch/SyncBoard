using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SyncBoard.Database;
using SyncBoard.Hubs.Board;

namespace SyncBoard.Features.Columns.GetBoardColumns;

public record ColumnDto(Guid Id, string Title, int Position, int CardsCount);
public record GetBoardColumnsResponse(Guid BoardId, List<ColumnDto> Columns);

public record GetBoardColumnsQuery(Guid BoardId, Guid UserId) : IRequest<IResult>;

public static class GetBoardColumnsEndpoint
{
    public static void MapGetBoardColumns(this IEndpointRouteBuilder app)
    {
        app.MapGet("api/boards/{boardId:guid}/columns", async (
                Guid boardId,
                ClaimsPrincipal principal,
                ISender sender) =>
            {
                var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
                return await sender.Send(new GetBoardColumnsQuery(boardId, userId));
            })
            .RequireAuthorization()
            .WithTags("Column");
    }
}

public class GetBoardColumnsQueryHandler : IRequestHandler<GetBoardColumnsQuery, IResult>
{
    private readonly AppDbContext _dbContext;
    private readonly IHubContext<BoardHub> _hub;

    public GetBoardColumnsQueryHandler(AppDbContext dbContext, IHubContext<BoardHub> hub)
    {
        _dbContext = dbContext;
        _hub = hub;
    }

    public async Task<IResult> Handle(GetBoardColumnsQuery request, CancellationToken cancellationToken)
    {
        var board = await _dbContext.Boards
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == request.BoardId, cancellationToken);

        if (board is null)
            return Results.NotFound("Board not found.");

        if (board.OwnerId != request.UserId)
            return Results.Forbid();

        var columns = await _dbContext.Columns
            .AsNoTracking()
            .Where(c => c.BoardId == request.BoardId)
            .OrderBy(c => c.Position)
            .Select(c => new ColumnDto(c.Id, c.Title, c.Position, c.Cards.Count))
            .ToListAsync(cancellationToken);

        return Results.Ok(new GetBoardColumnsResponse(request.BoardId, columns));
    }

}