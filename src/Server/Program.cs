using DTA.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register DTA Singletons
builder.Services.AddSingleton<IServerTeleportService, ServerTeleportService>();
builder.Services.AddSingleton<IServerUIService, ServerUIService>();
builder.Services.AddSingleton<IServerLicenseService, ServerLicenseService>();

var app = builder.Build();

// Configure HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

// For integration tests
public partial class Program { }
