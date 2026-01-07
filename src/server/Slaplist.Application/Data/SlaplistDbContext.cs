using Microsoft.EntityFrameworkCore;
using Slaplist.Application.Domain;

namespace Slaplist.Infrastructure.Data;

public class SlaplistDbContext : DbContext
{
    public SlaplistDbContext(DbContextOptions<SlaplistDbContext> options) : base(options) { }

    public DbSet<Track> Tracks => this.Set<Track>();
    public DbSet<Collection> Collections => this.Set<Collection>();
    public DbSet<CollectionTrack> CollectionTracks => this.Set<CollectionTrack>();
    public DbSet<SearchCache> SearchCaches => this.Set<SearchCache>();
    public DbSet<QuotaTracker> QuotaTrackers => this.Set<QuotaTracker>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Track>(e =>
        {
            e.HasKey(t => t.Id);
            
            // Identity
            e.Property(t => t.Artist).HasMaxLength(300).IsRequired();
            e.Property(t => t.Title).HasMaxLength(500).IsRequired();
            e.Property(t => t.NormalizedArtist).HasMaxLength(300).IsRequired();
            e.Property(t => t.NormalizedTitle).HasMaxLength(500).IsRequired();
            
            // Metadata
            e.Property(t => t.Label).HasMaxLength(200);
            e.Property(t => t.Genre).HasMaxLength(100);
            e.Property(t => t.Key).HasMaxLength(10);
            
            // External IDs
            e.Property(t => t.YoutubeVideoId).HasMaxLength(20);
            e.Property(t => t.DiscogsReleaseId).HasMaxLength(20);
            e.Property(t => t.DiscogsMasterId).HasMaxLength(20);
            e.Property(t => t.BandcampUrl).HasMaxLength(500);
            
            // Raw titles - PostgreSQL array
            e.Property(t => t.RawTitlesEncountered).HasColumnType("text[]");
            
            // Indexes
            e.HasIndex(t => t.YoutubeVideoId).IsUnique().HasFilter("\"YoutubeVideoId\" IS NOT NULL");
            e.HasIndex(t => t.DiscogsReleaseId).HasFilter("\"DiscogsReleaseId\" IS NOT NULL");
            e.HasIndex(t => new { t.NormalizedArtist, t.NormalizedTitle });
            e.HasIndex(t => t.Label);
            e.HasIndex(t => t.Genre);
        });

        modelBuilder.Entity<Collection>(e =>
        {
            e.HasKey(c => c.Id);
            
            e.Property(c => c.ExternalId).HasMaxLength(100).IsRequired();
            e.Property(c => c.Title).HasMaxLength(500).IsRequired();
            e.Property(c => c.OwnerName).HasMaxLength(200);
            e.Property(c => c.OwnerExternalId).HasMaxLength(100);
            e.Property(c => c.ThumbnailUrl).HasMaxLength(500);
            
            // Unique constraint: source + external ID
            e.HasIndex(c => new { c.Source, c.ExternalId }).IsUnique();
            e.HasIndex(c => c.LastSyncedAt);
            e.HasIndex(c => new { c.Source, c.Type });
        });

        modelBuilder.Entity<CollectionTrack>(e =>
        {
            e.HasKey(ct => new { ct.CollectionId, ct.TrackId });
            
            e.HasOne(ct => ct.Collection)
                .WithMany(c => c.CollectionTracks)
                .HasForeignKey(ct => ct.CollectionId)
                .OnDelete(DeleteBehavior.Cascade);
                
            e.HasOne(ct => ct.Track)
                .WithMany(t => t.CollectionTracks)
                .HasForeignKey(ct => ct.TrackId)
                .OnDelete(DeleteBehavior.Cascade);
                
            e.HasIndex(ct => ct.TrackId);
        });

        modelBuilder.Entity<SearchCache>(e =>
        {
            e.HasKey(s => s.Id);
            
            e.Property(s => s.Query).HasMaxLength(500).IsRequired();
            e.Property(s => s.NormalizedQuery).HasMaxLength(500).IsRequired();
            
            // PostgreSQL arrays for result IDs
            e.Property(s => s.ResultCollectionIds).HasColumnType("integer[]");
            e.Property(s => s.ResultTrackIds).HasColumnType("integer[]");
            
            e.HasIndex(s => new { s.NormalizedQuery, s.Source, s.SearchType, s.SearchedAt });
        });

        modelBuilder.Entity<QuotaTracker>(e =>
        {
            e.HasKey(q => q.Id);
            e.HasIndex(q => new { q.Date, q.Source }).IsUnique();
        });
    }
}