using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SyncBoard.Database;
using SyncBoard.Entities;
using SyncBoard.Hubs.Board;

namespace SyncBoard.Features.Boards.CreateBoard;

public sealed record CreateBoardRequest(string? Title);

public record CreateBoardCommand(string? Title, Guid OwnerId) : IRequest<IResult>;

public static class CreateBoardEndpoint
{
    public static void MapCreateBoard(this IEndpointRouteBuilder app)
    {
         app.MapPost("api/boards", async (
                CreateBoardRequest request,
                ClaimsPrincipal principal,
                ISender sender) =>
            {
                var nameIdentifier = principal.FindFirstValue(ClaimTypes.NameIdentifier);
                
                if (!Guid.TryParse(nameIdentifier, out var userId))
                    return Results.Unauthorized();
                
                var command = new CreateBoardCommand(request.Title, userId);
                
                return await sender.Send(command);
            })
            .RequireAuthorization()
            .WithTags("Boards");
    }
}

public class CreateBoardCommandHandler : IRequestHandler<CreateBoardCommand, IResult>
{
    private readonly AppDbContext _dbContext;

    public CreateBoardCommandHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IResult> Handle(CreateBoardCommand request, CancellationToken ct)
    {
        var title = request.Title;

        if (!string.IsNullOrWhiteSpace(title))
        {
            return Results.BadRequest(new
            {
                Error = "Board title is required."
            });
        }

        if (request.Title.Length  > 256)
        {
            return Results.BadRequest(new
            {
                Error = "Board title can't be longer than 256 characters."
            });
        }
        
        var ownerExists = await _dbContext.Users.AnyAsync(u => u.Id == request.OwnerId, ct);

        if (!ownerExists)
            return Results.Unauthorized();
            
        var board = new Board(Guid.CreateVersion7(), title, request.OwnerId);

        _dbContext.Boards.Add(board);

        await _dbContext.SaveChangesAsync(ct);
            
        return Results.Ok(new
        {
            Id = board.Id,
            Title = board.Title
        });
    }
}