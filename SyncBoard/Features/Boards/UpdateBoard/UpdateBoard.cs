using System.Security.Claims;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SyncBoard.Database;

namespace SyncBoard.Features.Boards.UpdateBoard;

public abstract record UpdateBoardRequest(string Title);
public record UpdateBoardResponse(Guid Id, string Title);

public record UpdateBoardCommand(Guid BoardId, string Title, Guid UserId) : IRequest<IResult>;

public static class UpdateBoardEndpoint
{
    public static void MapUpdateBoard(this IEndpointRouteBuilder app)
    {
        app.MapPatch("api/boards/{boardId:guid}", async (
                Guid boardId,
                ClaimsPrincipal principal,
                UpdateBoardRequest request,
                ISender sender) =>
            {
                var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
                return await sender.Send(new UpdateBoardCommand(boardId, request.Title, userId));
            })
            .RequireAuthorization()
            .WithTags("Boards");
    }
}

public class UpdateBoardCommandHandler : IRequestHandler<UpdateBoardCommand, IResult>
{
    private readonly AppDbContext _dbContext;

    public UpdateBoardCommandHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IResult> Handle(UpdateBoardCommand request, CancellationToken cancellationToken)
    {
        var board = await _dbContext.Boards
            .FirstOrDefaultAsync(b => b.Id == request.BoardId, cancellationToken);

        if (board is null)
            return Results.NotFound("Board not found.");

        if (board.OwnerId != request.UserId)
            return Results.Forbid();

        board.UpdateTitle(request.Title);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new UpdateBoardResponse(board.Id, board.Title));
    }
}