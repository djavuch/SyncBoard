using System.Security.Claims;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SyncBoard.Database;

namespace SyncBoard.Features.Boards.GetBoardById;

public record CardDto(Guid Id, string Title, string Description, int Position);
public record ColumnDto(Guid Id, string Title, int Position, List<CardDto> Cards);
public record GetBoardByIdResponse(Guid Id, string Title, DateTime CreatedAt, List<ColumnDto> Columns);

public record GetBoardByIdQuery(Guid BoardId, Guid UserId) : IRequest<IResult>;

public static class GetBoardByIdEndpoint
{
    public static void MapGetBoardById(this IEndpointRouteBuilder app)
    {
        app.MapGet("api/boards/{boardId:guid}", async (
                Guid boardId,
                System.Security.Claims.ClaimsPrincipal principal,
                ISender sender) =>
            {
                var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
                return await sender.Send(new GetBoardByIdQuery(boardId, userId));
            })
            .RequireAuthorization()
            .WithTags("Boards");
    }
}

public class GetBoardByIdQueryHandler : IRequestHandler<GetBoardByIdQuery, IResult>
{
    private readonly AppDbContext _dbContext;

    public GetBoardByIdQueryHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IResult> Handle(GetBoardByIdQuery request, CancellationToken cancellationToken)
    {
        var board = await _dbContext.Boards
            .Include(b => b.Columns)
            .ThenInclude(c => c.Cards)
            .FirstOrDefaultAsync(b => b.Id == request.BoardId, cancellationToken);

        if (board is null)
            return Results.NotFound("Board not found");

        if (board.OwnerId != request.UserId)
            return Results.Forbid();

        var response = new GetBoardByIdResponse(
            board.Id,
            board.Title,
            board.CreatedAt,
            board.Columns
                .OrderBy(c => c.Position)
                .Select(c => new ColumnDto(
                    c.Id,
                    c.Title,
                    c.Position,
                    c.Cards
                        .OrderBy(card => card.Position)
                        .Select(card => new CardDto(
                            card.Id,
                            card.Title,
                            card.Description,
                            card.Position))
                        .ToList()))
                .ToList());

        return Results.Ok(response);
    }
}