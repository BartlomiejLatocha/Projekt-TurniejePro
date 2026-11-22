using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurniejePro.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTeamIsAccepted : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsAccepted",
                table: "Teams");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
