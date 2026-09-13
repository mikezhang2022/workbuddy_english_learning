using FactoryReport.Application.Abstractions;
using FactoryReport.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/health", (IDataAccessModeProvider modeProvider, IPlaceholderDataStore store) =>
{
    return Results.Json(new
    {
        status = "Healthy",
        mode = modeProvider.Mode.ToString(),
        isFake = modeProvider.IsFake,
        store = store.Describe(),
        utc = DateTime.UtcNow
    });
})
.WithName("Health");

app.Run();

public partial class Program;
