using Transfers.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(
    builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Connection string 'Postgres' is not configured."));

builder.Services.AddSingleton(TimeProvider.System);

var app = builder.Build();

app.MapTransfers();

app.Run();