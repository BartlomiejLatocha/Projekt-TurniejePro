using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurniejePro.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupStage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GroupNumber",
                table: "Teams",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsGroupStage",
                table: "Matches",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GroupNumber",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "IsGroupStage",
                table: "Matches");
        }
    }
}
