using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;

namespace UniversalTennis.Algorithm.Migrations
{
    public partial class Add_SubRating_Table : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SubRating",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    ClayCourt = table.Column<double>(type: "float", nullable: true),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateLastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EightWeek = table.Column<double>(type: "float", nullable: true),
                    GrandSlamMasters = table.Column<double>(type: "float", nullable: true),
                    GrassCourt = table.Column<double>(type: "float", nullable: true),
                    HardCourt = table.Column<double>(type: "float", nullable: true),
                    OneMonth = table.Column<double>(type: "float", nullable: true),
                    PlayerRatingId = table.Column<int>(type: "int", nullable: false),
                    SixWeek = table.Column<double>(type: "float", nullable: true),
                    ThreeMonth = table.Column<double>(type: "float", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubRating", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubRating_PlayerRating_PlayerRatingId",
                        column: x => x.PlayerRatingId,
                        principalTable: "PlayerRating",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SubRating_PlayerRatingId",
                table: "SubRating",
                column: "PlayerRatingId",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubRating");
        }
    }
}
