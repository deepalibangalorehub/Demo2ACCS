using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;

namespace UniversalTennis.Algorithm.Migrations
{
    public partial class Update_SubRating_Table : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClayCourtCount",
                table: "SubRating",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EightWeekCount",
                table: "SubRating",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GrandSlamMastersCount",
                table: "SubRating",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GrassCourtCount",
                table: "SubRating",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HardCourtCount",
                table: "SubRating",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OneMonthCount",
                table: "SubRating",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResultCount",
                table: "SubRating",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SixWeekCount",
                table: "SubRating",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ThreeMonthCount",
                table: "SubRating",
                type: "int",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClayCourtCount",
                table: "SubRating");

            migrationBuilder.DropColumn(
                name: "EightWeekCount",
                table: "SubRating");

            migrationBuilder.DropColumn(
                name: "GrandSlamMastersCount",
                table: "SubRating");

            migrationBuilder.DropColumn(
                name: "GrassCourtCount",
                table: "SubRating");

            migrationBuilder.DropColumn(
                name: "HardCourtCount",
                table: "SubRating");

            migrationBuilder.DropColumn(
                name: "OneMonthCount",
                table: "SubRating");

            migrationBuilder.DropColumn(
                name: "ResultCount",
                table: "SubRating");

            migrationBuilder.DropColumn(
                name: "SixWeekCount",
                table: "SubRating");

            migrationBuilder.DropColumn(
                name: "ThreeMonthCount",
                table: "SubRating");
        }
    }
}
