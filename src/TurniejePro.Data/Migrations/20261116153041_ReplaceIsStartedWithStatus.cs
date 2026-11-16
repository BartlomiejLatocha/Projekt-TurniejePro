using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurniejePro.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceIsStartedWithStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Tournaments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // Turnieje już rozpoczęte (IsStarted = 1) dostają status „W trakcie”, zanim kolumna zostanie usunięta.
            migrationBuilder.Sql("UPDATE Tournaments SET Status = 2 WHERE IsStarted = 1;");

            migrationBuilder.DropColumn(
                name: "IsStarted",
                table: "Tournaments");
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

            migrationBuilder.Sql("UPDATE Tournaments SET IsStarted = 1 WHERE Status >= 2;");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Tournaments");
        }
    }
}
