using Microsoft.EntityFrameworkCore;
using SmartShop.Api.Data;
using SmartShop.Api.Middleware;
using SmartShop.Api.Services;
using SmartShop.Api.Services.Gemini;

var builder = WebApplication.CreateBuilder(args);

// ---- Configuration ------------------------------------------------------------------------

builder.Services.Configure<GeminiOptions>(builder.Configuration.GetSection(GeminiOptions.SectionName));

// Allow the API key to be supplied as a plain environment variable (GEMINI_API_KEY) in
// addition to the Gemini__ApiKey / User Secrets convention, which is convenient for
// containerized or CI environments.
var envApiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
if (!string.IsNullOrWhiteSpace(envApiKey))
{
    builder.Services.PostConfigure<GeminiOptions>(opt => opt.ApiKey = envApiKey);
}

// ---- Services -------------------------------------------------------------------------------

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<SmartShopDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("SmartShopDb")));

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IChatHistoryStore, InMemoryChatHistoryStore>();

builder.Services.AddHttpClient<IGeminiClient, GeminiClient>((sp, client) =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<GeminiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(60);
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

// ---- Database: apply pending migrations automatically on startup --------------------------

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SmartShopDbContext>();
    db.Database.Migrate();
}

// ---- Pipeline -------------------------------------------------------------------------------

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Smart Shop Gemini API v1"));

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseCors("AllowFrontend");

app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();

app.Run();
