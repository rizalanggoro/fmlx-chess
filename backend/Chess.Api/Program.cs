using System.Text.Json.Serialization;
using Api.GameEngine;
using Api.Hubs;
using Api.Interfaces.GameEngine;
using Api.Interfaces.Services;
using Api.Services;
using Api.Swagger;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting Chess.Api application...");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    // Add services to the container.
    builder.Services
        .AddControllers()
        .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(
                new JsonStringEnumConverter()
            )
        );
    builder.Services.AddCors(
        o => o.AddDefaultPolicy(
            p => p.AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader()
        )
    );

    builder.Services
        .AddSignalR()
        .AddJsonProtocol(o =>
            o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter())
        );
    builder.Services.AddSingleton<IRoomRegistry, RoomRegistry>();
    builder.Services.AddSingleton<IRoomService, RoomService>();
    builder.Services.AddTransient<IBoardInitializer, StandardBoardInitializer>();

    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SupportNonNullableReferenceTypes();
        c.UseAllOfToExtendReferenceSchemas();
        c.SchemaFilter<RequiredSchemaFilter>();
        c.DocumentFilter<SignalRContractSchemaFilter>();
    });

    var app = builder.Build();

    app.UseSerilogRequestLogging();

    // Configure the HTTP request pipeline.
    // ponytail: open CORS (semua environment, FE di subdomain terpisah), tighten when auth exists
    app.UseCors();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();

    app.UseAuthorization();

    app.MapControllers();
    app.MapHub<GameHub>("/hubs/game");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Chess.Api terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
