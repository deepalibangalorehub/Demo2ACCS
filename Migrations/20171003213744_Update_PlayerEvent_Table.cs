using Microsoft.EntityFrameworkCore.Migrations;

namespace UniversalTennis.Algorithm.Migrations
{
    public partial class Update_PlayerEvent_Table : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InfoDoc",
                table: "PlayerEvent",
                type: "varchar(2000)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InfoDoc",
                table: "PlayerEvent");
        }
    }
}
