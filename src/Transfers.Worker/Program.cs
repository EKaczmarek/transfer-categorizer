using Transfers.Application;
using Transfers.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);
var cs = builder.Configuration.GetConnectionString("Default")!;

builder.Services.AddInfrastructure(cs);
builder.Services.AddMessaging(builder.Configuration);
builder.Services.AddSingleton<ITransferCategorizer, RuleBasedCategorizer>();
builder.Services.AddHostedService<OutboxDispatcher>();
builder.Services.AddHostedService<TransferCreatedConsumer>();

await builder.Build().RunAsync();