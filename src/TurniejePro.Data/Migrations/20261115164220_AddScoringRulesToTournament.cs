using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurniejePro.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddScoringRulesToTournament : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ScoringRules_AllowDraws",
                table: "Tournaments",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "ScoringRules_PointsForDraw",
                table: "Tournaments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "ScoringRules_PointsForLoss",
                table: "Tournaments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ScoringRules_PointsForWin",
                table: "Tournaments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 3);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ScoringRules_AllowDraws",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "ScoringRules_PointsForDraw",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "ScoringRules_PointsForLoss",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "ScoringRules_PointsForWin",
                table: "Tournaments");
        }
    }
}
