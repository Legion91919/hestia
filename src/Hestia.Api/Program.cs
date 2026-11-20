using Hestia.Api;

var builder = WebApplication.CreateBuilder(args);
HestiaApp.ConfigureServices(builder);
var app = builder.Build();
HestiaApp.Configure(app);
app.Run();
