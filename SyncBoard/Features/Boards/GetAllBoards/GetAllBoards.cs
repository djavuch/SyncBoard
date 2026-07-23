using System.Security.Claims;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SyncBoard.Database;

namespace SyncBoard.Features.Boards.GetAllBoards;

public record BoardResponse(Guid Id, string Title, DateTime CreatedAt);

public record GetAllBoardsQuery(Guid UserId) : IRequest<IResult>;

public static class GetAllBoardsEndpoint
{
    public static void MapGetAllBoards(this IEndpointRouteBuilder app)
    {
        app.MapGet("api/boards", async (
                ClaimsPrincipal principal,
                ISender sender) =>
            {
                var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
                return await sender.Send(new GetAllBoardsQuery(userId));
            })
            .RequireAuthorization()
            .WithTags("Boards");
    }
}

public class GetAllBoardsQueryHandler : IRequestHandler<GetAllBoardsQuery, IResult>
{
    private readonly AppDbContext _dbContext;

    public GetAllBoardsQueryHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IResult> Handle(GetAllBoardsQuery request, CancellationToken cancellationToken)
    {
        var boards = await _dbContext.Boards
            .Where(b => b.OwnerId == request.UserId)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new BoardResponse(b.Id, b.Title, b.CreatedAt))
            .ToListAsync(cancellationToken);

        return Results.Ok(boards);
    }
}