using System.Security.Claims;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SyncBoard.Database;
using System.Diagnostics;

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
    private readonly ILogger<GetAllBoardsQueryHandler> _logger;

    public GetAllBoardsQueryHandler(
        AppDbContext dbContext,
        ILogger<GetAllBoardsQueryHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }


    public async Task<IResult> Handle(
        GetAllBoardsQuery request,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();

        var boards = await _dbContext.Boards
            .Where(b => b.OwnerId == request.UserId)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new BoardResponse(b.Id, b.Title, b.CreatedAt))
            .ToListAsync(cancellationToken);

        var durationMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;

        if (durationMs >= 100)
        {
            _logger.LogInformation(
                "Board list loaded in {BoardQueryDurationMs} ms. Count: {BoardCount}",
                durationMs,
                boards.Count);
        }

        return Results.Ok(boards);
    }
}