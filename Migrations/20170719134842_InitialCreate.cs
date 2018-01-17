using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;

namespace UniversalTennis.Algorithm.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlayerRating",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    ActualRating = table.Column<double>(type: "float", nullable: true),
                    AlternativeRating = table.Column<double>(type: "float", nullable: true),
                    AlternativeRatingReliability = table.Column<double>(type: "float", nullable: true),
                    BenchmarkRating = table.Column<double>(type: "float", nullable: true),
                    CompetitiveMatchPct = table.Column<double>(type: "float", nullable: true),
                    CompetitiveMatchPctDoubles = table.Column<double>(type: "float", nullable: true),
                    DecisiveMatchPct = table.Column<double>(type: "float", nullable: true),
                    DoublesBenchmarkRating = table.Column<double>(type: "float", nullable: true),
                    DoublesRating = table.Column<double>(type: "float", nullable: true),
                    DoublesReliability = table.Column<double>(type: "float", nullable: true),
                    FinalDoublesRating = table.Column<double>(type: "float", nullable: true),
                    FinalRating = table.Column<double>(type: "float", nullable: true),
                    IsBenchmark = table.Column<bool>(type: "bit", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    PreviousLevel = table.Column<double>(type: "float", nullable: true),
                    PreviousRating = table.Column<double>(type: "float", nullable: true),
                    PreviousRatingReliability = table.Column<double>(type: "float", nullable: true),
                    PublishedRating = table.Column<double>(type: "float", nullable: true),
                    PublishedReliability = table.Column<double>(type: "float", nullable: true),
                    RatingReliability = table.Column<double>(type: "float", nullable: true),
                    RatingStatusDoubles = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RatingStatusSingles = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RoutineMatchPct = table.Column<double>(type: "float", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerRating", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RatingResult",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    ResultId = table.Column<int>(type: "int", nullable: false),
                    Loser1Rating = table.Column<double>(type: "float", nullable: true),
                    Loser1Reliability = table.Column<double>(type: "float", nullable: true),
                    Loser2Rating = table.Column<double>(type: "float", nullable: true),
                    Loser2Reliability = table.Column<double>(type: "float", nullable: true),
                    Winner1Rating = table.Column<double>(type: "float", nullable: true),
                    Winner1Reliability = table.Column<double>(type: "float", nullable: true),
                    Winner2Rating = table.Column<double>(type: "float", nullable: true),
                    Winner2Reliability = table.Column<double>(type: "float", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RatingResult", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerRating");

            migrationBuilder.DropTable(
                name: "RatingResult");
        }
    }
}
