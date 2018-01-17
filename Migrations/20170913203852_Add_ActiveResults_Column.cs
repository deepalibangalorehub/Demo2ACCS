using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;

namespace UniversalTennis.Algorithm.Migrations
{
    public partial class Add_ActiveResults_Column : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActiveDoublesResults",
                table: "PlayerRating",
                type: "varchar(4000)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActiveSinglesResults",
                table: "PlayerRating",
                type: "varchar(4000)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActiveDoublesResults",
                table: "PlayerRating");

            migrationBuilder.DropColumn(
                name: "ActiveSinglesResults",
                table: "PlayerRating");
        }
    }
}
