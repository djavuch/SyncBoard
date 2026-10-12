using System.Text;
using Elastic.Ingest.Elasticsearch;
using Elastic.Ingest.Elasticsearch.DataStreams;
using Elastic.Serilog.Sinks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Prometheus;
using Serilog;
using Serilog.Events;
using SyncBoard.Database;
using SyncBoard.Entities;
using SyncBoard.Features.Auth.Login;
using SyncBoard.Features.Auth.Register;
using SyncBoard.Features.Boards.CreateBoard;
using SyncBoard.Features.Boards.DeleteBoard;
using SyncBoard.Features.Boards.GetAllBoards;
using SyncBoard.Features.Boards.GetBoardById;
using SyncBoard.Features.Boards.UpdateBoard;
using SyncBoard.Features.Cards.CreateCard;
using SyncBoard.Features.Cards.DeleteCard;
using SyncBoard.Features.Cards.GetCardsByColumn;
using SyncBoard.Features.Cards.MoveCard;
using SyncBoard.Features.Cards.UpdateCard;
using SyncBoard.Features.Columns.CreateColumns;
using SyncBoard.Features.Columns.DeleteColumn;
using SyncBoard.Features.Columns.GetBoardColumns;
using SyncBoard.Features.Columns.UpdateColumn;
using SyncBoard.Hubs.Board;
using SyncBoard.Infrastructure.Auth;
using SyncBoard.Infrastructure.Exceptions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("SyncBoardConnection");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddSerilog(configuration => configuration
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "SyncBoard")
    // .WriteTo.Console()
    .WriteTo.Elasticsearch(
        [new Uri("http://localhost:9200")],
        options =>
        {
            options.DataStream = 
                new DataStreamName("logs", "syncboard", "development");
            
            options.BootstrapMethod = BootstrapMethod.Failure;
        }));

builder.Services.AddIdentityCore<User>(options =>
    {
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequiredLength = 6;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<AppDbContext>();

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));
builder.Services.AddScoped<JwtProvider>();

var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
                  ?? throw new InvalidOperationException("JWT Settings are missing in appsettings.json");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey))
        };
    });

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
builder.Services.AddSignalR();

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseRouting();

app.UseSerilogRequestLogging();
app.UseHttpMetrics();

app.UseExceptionHandler();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapGet("/debug/error", IResult () =>
            throw new InvalidOperationException(
                "Monitoring test: intentional failure"))
        .AllowAnonymous();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapRegister();
app.MapLogin();
    
// Boards
app.MapCreateBoard();
app.MapGetBoardById();
app.MapUpdateBoard();
app.MapDeleteBoard();
app.MapGetAllBoards();
    
// Columns
app.MapCreateColumn();
app.MapDeleteColumn();
app.MapGetBoardColumns();
app.MapUpdateColumn();

// Cards
app.MapCreateCard();
app.MapDeleteCard();
app.MapGetCardByColumn();
app.MapMoveCard();
app.MapUpdateCard();

app.MapHub<BoardHub>("/hubs/board")
    .RequireAuthorization();

app.MapMetrics();

app.Run();
