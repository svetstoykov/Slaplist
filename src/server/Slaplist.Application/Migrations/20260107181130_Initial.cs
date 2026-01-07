using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Slaplist.Application.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Collections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    OwnerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    OwnerExternalId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ThumbnailUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReportedTrackCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SyncComplete = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Collections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QueryStatistics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InputQueries = table.Column<string[]>(type: "text[]", nullable: false),
                    ApiSearchCalls = table.Column<int>(type: "integer", nullable: false),
                    ApiFetchCalls = table.Column<int>(type: "integer", nullable: false),
                    CacheHits = table.Column<int>(type: "integer", nullable: false),
                    NotEnoughQuota = table.Column<int>(type: "integer", nullable: false),
                    QuotaUsed = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QueryStatistics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QuotaTrackers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    UnitsUsed = table.Column<int>(type: "integer", nullable: false),
                    SearchCalls = table.Column<int>(type: "integer", nullable: false),
                    FetchCalls = table.Column<int>(type: "integer", nullable: false),
                    DailyLimit = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuotaTrackers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SearchCaches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Query = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    NormalizedQuery = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    SearchType = table.Column<int>(type: "integer", nullable: false),
                    SearchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResultCount = table.Column<int>(type: "integer", nullable: false),
                    QuotaUsed = table.Column<int>(type: "integer", nullable: false),
                    ResultCollectionIds = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    ResultTrackIds = table.Column<List<Guid>>(type: "uuid[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SearchCaches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tracks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Artist = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    NormalizedArtist = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    NormalizedTitle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Genre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Bpm = table.Column<int>(type: "integer", nullable: true),
                    Key = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ReleaseYear = table.Column<int>(type: "integer", nullable: true),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: true),
                    YoutubeVideoId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DiscogsReleaseId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DiscogsMasterId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    BandcampUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RawTitlesEncountered = table.Column<List<string>>(type: "text[]", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastEnrichedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tracks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CollectionTracks",
                columns: table => new
                {
                    CollectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TrackId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    AddedToCollectionAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DiscoveredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionTracks", x => new { x.CollectionId, x.TrackId });
                    table.ForeignKey(
                        name: "FK_CollectionTracks_Collections_CollectionId",
                        column: x => x.CollectionId,
                        principalTable: "Collections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CollectionTracks_Tracks_TrackId",
                        column: x => x.TrackId,
                        principalTable: "Tracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Collections_LastSyncedAt",
                table: "Collections",
                column: "LastSyncedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Collections_Source_ExternalId",
                table: "Collections",
                columns: new[] { "Source", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Collections_Source_Type",
                table: "Collections",
                columns: new[] { "Source", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_CollectionTracks_TrackId",
                table: "CollectionTracks",
                column: "TrackId");

            migrationBuilder.CreateIndex(
                name: "IX_QuotaTrackers_Date_Source",
                table: "QuotaTrackers",
                columns: new[] { "Date", "Source" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SearchCaches_NormalizedQuery_Source_SearchType_SearchedAt",
                table: "SearchCaches",
                columns: new[] { "NormalizedQuery", "Source", "SearchType", "SearchedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Tracks_DiscogsReleaseId",
                table: "Tracks",
                column: "DiscogsReleaseId",
                filter: "\"DiscogsReleaseId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Tracks_Genre",
                table: "Tracks",
                column: "Genre");

            migrationBuilder.CreateIndex(
                name: "IX_Tracks_Label",
                table: "Tracks",
                column: "Label");

            migrationBuilder.CreateIndex(
                name: "IX_Tracks_NormalizedArtist_NormalizedTitle",
                table: "Tracks",
                columns: new[] { "NormalizedArtist", "NormalizedTitle" });

            migrationBuilder.CreateIndex(
                name: "IX_Tracks_YoutubeVideoId",
                table: "Tracks",
                column: "YoutubeVideoId",
                unique: true,
                filter: "\"YoutubeVideoId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CollectionTracks");

            migrationBuilder.DropTable(
                name: "QueryStatistics");

            migrationBuilder.DropTable(
                name: "QuotaTrackers");

            migrationBuilder.DropTable(
                name: "SearchCaches");

            migrationBuilder.DropTable(
                name: "Collections");

            migrationBuilder.DropTable(
                name: "Tracks");
        }
    }
}
