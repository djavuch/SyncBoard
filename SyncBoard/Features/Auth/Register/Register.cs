using MediatR;
using Microsoft.AspNetCore.Identity;
using SyncBoard.Database;
using SyncBoard.Entities;

namespace SyncBoard.Features.Auth.Register;

public record RegisterRequest(string Email, string Password, string FirstName, string LastName);
public record RegisterResponse(Guid UserId, string Message);


public record RegisterCommand(string Email, string Password, 
    string FirstName, string LastName) : IRequest<IResult>;

public static class RegisterEndpoint
{
    public static void MapRegister(this IEndpointRouteBuilder app)
    {
        app.MapPost("api/auth/register", async (RegisterRequest request, ISender sender) => 
            await sender.Send(new RegisterCommand(request.Email, request.Password, request.FirstName, request.LastName)))
            .WithTags("Auth");
    }
}

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, IResult>
{
    private readonly UserManager<User> _userManager;

    public RegisterCommandHandler(UserManager<User> userManager)
    {
        _userManager = userManager;
    }

    public async Task<IResult> Handle(RegisterCommand reguest, CancellationToken cancellationToken)
    {
        var existingUser = await _userManager.FindByEmailAsync(reguest.Email);
        if (existingUser is not null)
            return Results.BadRequest("User already exists");
        
        var user = new User(reguest.Email, reguest.FirstName, reguest.LastName);
        
        var result = await _userManager.CreateAsync(user, reguest.Password);
        
        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => e.Description);
            return Results.BadRequest(result.Errors);
        }
        
        return Results.Ok(new RegisterResponse(user.Id, "User created successfully"));
    }
}