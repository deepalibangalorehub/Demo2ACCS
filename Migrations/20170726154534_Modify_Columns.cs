using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;

namespace UniversalTennis.Algorithm.Migrations
{
    public partial class Modify_Columns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RatingStatusDoubles",
                table: "PlayerRating");

            migrationBuilder.DropColumn(
                name: "RatingStatusSingles",
                table: "PlayerRating");

            migrationBuilder.AddColumn<string>(
                name: "InfoDoc",
                table: "ResultEvent",
                type: "nvarchar(250)",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Loser1CalculatedRating",
                table: "RatingResult",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Loser2CalculatedRating",
                table: "RatingResult",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Winner1CalculatedRating",
                table: "RatingResult",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Winner2CalculatedRating",
                table: "RatingResult",
                type: "float",
                nullable: true);

            migrationBuilder.AddUniqueConstraint("IX_PlayerRating", "PlayerRating", "PlayerId");
            migrationBuilder.AddUniqueConstraint("IX_RatingResult", "RatingResult", "ResultId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InfoDoc",
                table: "ResultEvent");

            migrationBuilder.DropColumn(
                name: "Loser1CalculatedRating",
                table: "RatingResult");

            migrationBuilder.DropColumn(
                name: "Loser2CalculatedRating",
                table: "RatingResult");

            migrationBuilder.DropColumn(
                name: "Winner1CalculatedRating",
                table: "RatingResult");

            migrationBuilder.DropColumn(
                name: "Winner2CalculatedRating",
                table: "RatingResult");

            migrationBuilder.AddColumn<string>(
                name: "RatingStatusDoubles",
                table: "PlayerRating",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RatingStatusSingles",
                table: "PlayerRating",
                nullable: true);

            migrationBuilder.DropUniqueConstraint("IX_PlayerRating", "PlayerRating");
            migrationBuilder.DropUniqueConstraint("IX_ResultRating", "RatingResult");
        }
    }
}
