using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;

namespace UniversalTennis.Algorithm.Migrations
{
    public partial class PlayerRating : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PlayerGender",
                table: "PlayerRating",
                nullable: true,
                type: "varchar(1)");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlayerGender",
                table: "PlayerRating");
        }
    }
}
