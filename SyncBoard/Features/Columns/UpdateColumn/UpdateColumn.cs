using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SyncBoard.Database;
using SyncBoard.Hubs.Board;

namespace SyncBoard.Features.Columns.UpdateColumn;

public sealed record UpdateColumnRequest(string? Title, int? Position);

public record UpdateColumnCommand(Guid ColumnId, string? Title, int? Position, Guid UserId) 
    : IRequest<IResult>;


public static class UpdateColumnEndpoint
{
    public static void MapUpdateColumn(this IEndpointRouteBuilder app)
    {
        app.MapPut("api/columns/{columnId:guid}", async (
            Guid columnId,
            ClaimsPrincipal principal,
            UpdateColumnRequest request,
            ISender sender) =>
        {
            var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            return await sender.Send(new UpdateColumnCommand(columnId, request.Title, request.Position, userId));
        })
        .RequireAuthorization()
        .WithTags("Columns");
    }
}

public class UpdateColumnCommandHandler : IRequestHandler<UpdateColumnCommand, IResult>
{
    private readonly AppDbContext _dbContext;
    private readonly IHubContext<BoardHub> _hub; 

    public UpdateColumnCommandHandler(AppDbContext dbContext, IHubContext<BoardHub> hub)
    {
        _dbContext = dbContext;
        _hub = hub;
    }

    public async Task<IResult> Handle(UpdateColumnCommand request, CancellationToken ct)
    {
        var column = await _dbContext.Columns
            .Include(c => c.Board)
            .FirstOrDefaultAsync(c => c.Id == request.ColumnId, ct);

        if (column is null)
            return Results.NotFound("Column not found.");

        if (column.Board.OwnerId != request.UserId)
            return Results.Forbid();

        if (!string.IsNullOrWhiteSpace(request.Title))
            column.Title = request.Title;
        
        if (request.Position.HasValue)
            column.UpdatePosition(request.Position.Value);

        await _dbContext.SaveChangesAsync(ct);
        
        await _hub.Clients.Group(column.Board.Id.ToString())
            .SendAsync("CardUpdated", new ColumnUpdatedEvent(column.Id, column.Title, column.Position), ct);

        return Results.NoContent();
    }
}