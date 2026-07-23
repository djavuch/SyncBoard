using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SyncBoard.Database;
using SyncBoard.Hubs.Board;

namespace SyncBoard.Features.Columns.DeleteColumn;

public record DeleteColumnCommand(Guid ColumnId, Guid UserId) : IRequest<IResult>;

public static class DeleteColumnEndpoint
{
    public static void MapDeleteColumn(this IEndpointRouteBuilder app)
    {
        app.MapDelete("api/columns/{columnId:guid}", async (
                Guid columnId,
                ClaimsPrincipal principal,
                ISender sender) =>
            {
                var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
                return await sender.Send(new DeleteColumnCommand(columnId, userId));
            })
            .RequireAuthorization()
            .WithTags("Columns");
    }
}

public class DeleteColumnCommandHandler : IRequestHandler<DeleteColumnCommand, IResult>
{
    private readonly AppDbContext _dbContext;
    private readonly IHubContext<BoardHub> _hub;

    public DeleteColumnCommandHandler(AppDbContext dbContext, IHubContext<BoardHub> hub)
    {
        _dbContext = dbContext;
        _hub = hub;
    }

    public async Task<IResult> Handle(DeleteColumnCommand request, CancellationToken ct)
    {
        var column = await _dbContext.Columns
            .Include(c => c.Board)
            .FirstOrDefaultAsync(c => c.Id == request.ColumnId, ct);

        if (column is null)
            return Results.NotFound("Column not found");

        if (column.Board.OwnerId != request.UserId)
            return Results.Forbid();

        _dbContext.Columns.Remove(column);
        await _dbContext.SaveChangesAsync(ct);

        await _hub.Clients.Group(column.Board.Id.ToString())
            .SendAsync("ColumnDeleted", new ColumnDeletedEvent(column.Id), ct);

        return Results.NoContent();
    }
}