using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using System;

namespace UniversalTennis.Algorithm.Migrations
{
    public partial class Create_RatingHistory_Tables : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyRating",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    Algorithm = table.Column<string>(type: "varchar(50)", nullable: true),
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    PlayerRatingId = table.Column<int>(type: "int", nullable: false),
                    Rating = table.Column<double>(type: "float", nullable: false),
                    Reliability = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyRating", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailyRating_PlayerRating",
                        column: x => x.PlayerRatingId,
                        principalTable: "PlayerRating",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WeeklyAverage",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    PlayerRatingId = table.Column<int>(type: "int", nullable: false),
                    Rating = table.Column<double>(type: "float", nullable: false),
                    RatingStatus = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "varchar(50)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeeklyAverage", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeeklyAverage_PlayerRating",
                        column: x => x.PlayerRatingId,
                        principalTable: "PlayerRating",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyRating_PlayerRatingId",
                table: "DailyRating",
                column: "PlayerRatingId");

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyAverage_PlayerRatingId",
                table: "WeeklyAverage",
                column: "PlayerRatingId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyRating");

            migrationBuilder.DropTable(
                name: "WeeklyAverage");
        }
    }
}
