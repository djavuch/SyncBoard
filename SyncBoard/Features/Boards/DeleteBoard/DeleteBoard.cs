using System.Security.Claims;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SyncBoard.Database;

namespace SyncBoard.Features.Boards.DeleteBoard;

public record DeleteBoardCommand(Guid BoardId, Guid UserId) : IRequest<IResult>;

public static class DeleteBoardEndpoint
{
    public static void MapDeleteBoard(this IEndpointRouteBuilder app)
    {
        app.MapDelete("api/boards/{boardId:guid}", async (
                Guid boardId,
                ClaimsPrincipal principal,
                ISender sender) =>
            {
                var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
                return await sender.Send(new DeleteBoardCommand(boardId, userId));
            })
            .RequireAuthorization()
            .WithTags("Boards");
    }
}

public class DeleteBoardCommandHandler : IRequestHandler<DeleteBoardCommand, IResult>
{
    private readonly AppDbContext _dbContext;

    public DeleteBoardCommandHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IResult> Handle(DeleteBoardCommand request, CancellationToken cancellationToken)
    {
        var board = await _dbContext.Boards
            .Include(b => b.Columns)
            .ThenInclude(c => c.Cards)
            .FirstOrDefaultAsync(b => b.Id == request.BoardId, cancellationToken);

        if (board is null)
            return Results.NotFound("Board not found.");

        if (board.OwnerId != request.UserId)
            return Results.Forbid();

        _dbContext.Boards.Remove(board);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }
}