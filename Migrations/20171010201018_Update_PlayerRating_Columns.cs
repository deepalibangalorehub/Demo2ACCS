using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;

namespace UniversalTennis.Algorithm.Migrations
{
    public partial class Update_PlayerRating_Columns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreviousLevel",
                table: "PlayerRating");

            migrationBuilder.DropColumn(
                name: "PublishedRating",
                table: "PlayerRating");

            migrationBuilder.DropColumn(
                name: "PublishedReliability",
                table: "PlayerRating");

            migrationBuilder.AddColumn<double>(
                name: "InactiveRating",
                table: "PlayerRating",
                type: "float",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InactiveRating",
                table: "PlayerRating");

            migrationBuilder.AddColumn<double>(
                name: "PreviousLevel",
                table: "PlayerRating",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PublishedRating",
                table: "PlayerRating",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PublishedReliability",
                table: "PlayerRating",
                nullable: true);
        }
    }
}
