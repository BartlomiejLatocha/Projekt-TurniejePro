using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurniejePro.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTournamentStatusAndRemoveTeamIsAccepted : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Tournaments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.Sql("UPDATE Tournaments SET Status = 3 WHERE IsStarted = 1;");

            migrationBuilder.DropColumn(
                name: "IsStarted",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "IsAccepted",
                table: "Teams");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsStarted",
                table: "Tournaments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("UPDATE Tournaments SET IsStarted = 1 WHERE Status IN (3, 4);");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Tournaments");

            migrationBuilder.AddColumn<bool>(
                name: "IsAccepted",
                table: "Teams",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("UPDATE Teams SET IsAccepted = 1 WHERE Status = 2;");
        }
    }
}
