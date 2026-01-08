using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Slaplist.API.Mapping;
using Slaplist.API.Models;
using Slaplist.Application.Data;
using Slaplist.Application.Domain;
using Slaplist.Application.Helpers;
using Slaplist.Application.Interfaces;
using Slaplist.Application.Models;
using Slaplist.Application.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<RecommendationOptions>()
    .Bind(builder.Configuration.GetSection(RecommendationOptions.SectionName));
builder.Services.AddOptions<YoutubeOptions>()
    .Bind(builder.Configuration.GetSection(YoutubeOptions.SectionName));

builder.Services.AddDbContext<SlaplistDbContext>(options =>
{
    var conn = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseNpgsql(conn);
});

builder.Services.AddScoped<IYoutubeDiscoveryService, YoutubeDiscoveryService>();
builder.Services.AddScoped<IRecommendationOrchestrator, RecommendationOrchestrator>();
builder.Services.AddScoped<IQuotaService, QuotaService>();
builder.Services.AddScoped<ITrackService, TrackService>();

// Swagger/OpenAPI setup
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SlaplistDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok", time = DateTime.UtcNow }))
    .WithName("Health");

app.MapGet("/catalog-stats", async (SlaplistDbContext db) =>
{
    var stats = new
    {
        tracks = await db.Tracks.CountAsync(),
        collections = await db.Collections.CountAsync(),
        youtubeCollections = await db.Collections.CountAsync(c => c.Source == CollectionSource.YouTube)
    };
    return Results.Ok(stats);
}).WithName("GetCatalogStats");

app.MapGet("/quota", async (SlaplistDbContext db) =>
{
    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    var source = CollectionSource.YouTube;

    var quota = await db.QuotaTrackers
        .FirstOrDefaultAsync(q => q.Date == today && q.Source == source);

    if (quota is null)
    {
        quota = new QuotaTracker
        {
            Date = today,
            Source = source,
            UnitsUsed = 0,
            DailyLimit = QuotaTracker.GetDefaultLimit(source)
        };
        db.QuotaTrackers.Add(quota);
        await db.SaveChangesAsync();
    }

    return Results.Ok(new
    {
        date = quota.Date,
        source = quota.Source.ToString(),
        unitsUsed = quota.UnitsUsed,
        dailyLimit = quota.DailyLimit,
        remaining = quota.Remaining,
        usagePercent = quota.UsagePercent
    });
}).WithName("GetQuota");

app.MapPost("/recommendations", async (
    RecommendationRequest req,
    IOptions<YoutubeOptions> ytOptions,
    IRecommendationOrchestrator orchestrator,
    CancellationToken ct) =>
{
    if (req?.Tracks is null || req.Tracks.Count == 0)
        return Results.BadRequest(new { error = "Tracks list is required" });

    if (string.IsNullOrWhiteSpace(ytOptions.Value.ApiKey))
        return Results.BadRequest(new { error = "YouTube ApiKey is not configured." });

    var trackIds = req.Tracks.Select(t => YoutubeHelper.ExtractVideoId(t)).ToList();
    
    var result = await orchestrator.GetRecommendationsAsync(
        trackIds,
        req.CollectionsPerTrack ?? 5,
        req.ResultsToReturn ?? 50,
        ct);
    
    return Results.Ok(RecommendationMapper.ToRecommendationResponse(result));
}).WithName("GetRecommendations");

app.Run();
