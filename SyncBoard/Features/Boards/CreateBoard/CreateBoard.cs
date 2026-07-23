using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using SyncBoard.Database;
using SyncBoard.Entities;
using SyncBoard.Hubs.Board;

namespace SyncBoard.Features.Boards.CreateBoard;

public abstract record CreateBoardRequest(string Title);
public record CreateBoardResponse(Guid Id, string Title);

public record CreateBoardCommand(string Title, Guid? OwnerId) : IRequest<CreateBoardResponse>;

public static class CreateBoardEndpoint
{
    public static void MapCreateBoard(this IEndpointRouteBuilder app)
    {
         app.MapPost("api/boards", async (
                CreateBoardRequest request,
                ClaimsPrincipal principal,
                ISender sender) =>
            {
                var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var command = new CreateBoardCommand(request.Title, userId);
                var result = await sender.Send(command);
                return Results.Ok(result);
            })
            .RequireAuthorization()
            .WithTags("Boards");
    }
}

public class CreateBoardCommandHandler : IRequestHandler<CreateBoardCommand, CreateBoardResponse>
{
    private readonly AppDbContext _dbContext;

    public CreateBoardCommandHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CreateBoardResponse> Handle(CreateBoardCommand request, CancellationToken ct)
    {
        var board = new Board(Guid.CreateVersion7(), request.Title, request.OwnerId);
        
        _dbContext.Boards.Add(board);
        await _dbContext.SaveChangesAsync(ct);
        
        return new CreateBoardResponse(board.Id, board.Title);
    }
}