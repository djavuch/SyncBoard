using MediatR;
using Microsoft.AspNetCore.Identity;
using SyncBoard.Entities;
using SyncBoard.Infrastructure.Auth;

namespace SyncBoard.Features.Auth.Login;

public record LoginRequest(string Email, string Password);
public record LoginResponse(string Token);

public record LoginCommand(string Email, string Password) : IRequest<IResult>;

public static class LoginEndpoint
{
    public static void MapLogin(this IEndpointRouteBuilder app)
    {
        app.MapPost("api/auth/login", async (LoginRequest request, ISender sender) => 
            await sender.Send(new LoginCommand(request.Email, request.Password)))
            .WithTags("Auth");
    }
}

public class LoginCommandHandler : IRequestHandler<LoginCommand, IResult>
{
    private readonly UserManager<User> _userManager;
    private readonly JwtProvider _jwtProvider;

    public LoginCommandHandler(UserManager<User> userManager, JwtProvider jwtProvider)
    {
        _userManager = userManager;
        _jwtProvider = jwtProvider;
    }

    public async Task<IResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);

        if (user is null)
            return Results.BadRequest(new { Error = "Invalid email or password." });
        
        var isPasswordValid = await _userManager.CheckPasswordAsync(user, request.Password);

        if (!isPasswordValid)
            return Results.BadRequest(new { Error = "Invalid email or password." });

        var token = _jwtProvider.CreateToken(user);
        
        return Results.Ok(new LoginResponse(token));
    }
}