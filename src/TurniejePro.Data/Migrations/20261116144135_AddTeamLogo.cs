using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurniejePro.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamLogo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "Logo",
                table: "Teams",
                type: "BLOB",
                maxLength: 1048576,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoContentType",
                table: "Teams",
                type: "TEXT",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Logo",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "LogoContentType",
                table: "Teams");
        }
    }
}
