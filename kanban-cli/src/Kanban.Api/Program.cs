using Account.Infrastructure;
using Kanban.Api.Account;
using Kanban.Api.Controllers;
using Kanban.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);

AccountOptions accountOptions;
try
{
    accountOptions = new AccountOptions();
}
catch (Exception ex)
{
    throw new InvalidOperationException("Учётное хранилище не настроено.", ex);
}

builder.Services.AddSingleton(accountOptions);
builder.Services.AddAccountStore(accountOptions.AccountConnection);
builder.Services.AddAccountServices(accountOptions.JwtSigningKey);

builder.Services.AddSingleton<IConfigureOptions<JwtBearerOptions>, AccountJwtBearerConfigureOptions>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddAuthorization();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.WriteIndented = true;
        options.JsonSerializerOptions.Converters.Add(new DateOnlyJsonConverter());
    });

builder.Services.AddOpenApi();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:5174", "http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var solutionRoot = Directory.GetCurrentDirectory();
while (solutionRoot != null && !Directory.Exists(Path.Combine(solutionRoot, "data")) && !File.Exists(Path.Combine(solutionRoot, "kanban-cli.sln")))
{
    var parent = Directory.GetParent(solutionRoot);
    if (parent == null) break;
    solutionRoot = parent.FullName;
}

string dataPath;
if (Directory.Exists(Path.Combine(solutionRoot ?? Directory.GetCurrentDirectory(), "data")))
{
    dataPath = Path.Combine(solutionRoot ?? Directory.GetCurrentDirectory(), "data", "board.json");
}
else
{
    dataPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "data", "board.json");
    if (!File.Exists(dataPath))
    {
        dataPath = Path.Combine(Directory.GetCurrentDirectory(), "data", "board.json");
    }
}

builder.Services.AddSingleton(new BoardRepository(Path.GetFullPath(dataPath)));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("AllowFrontend");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseMiddleware<AccountExceptionMiddleware>();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program { }