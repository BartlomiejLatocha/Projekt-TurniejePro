using System.Linq;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurniejePro.Data.Migrations
{
    /// <inheritdoc />
    public partial class MapDisciplinesToCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Stare, wolnotekstowe dyscypliny zamieniamy na kody z DisciplineCatalog.
            // Obejmujemy najczęstsze warianty pisowni; nieznane wartości zostają bez zmian i są pokazywane jako tekst.
            MapDiscipline(migrationBuilder, "football",
                "Pilka nozna", "pilka nozna", "Piłka nożna", "Piłka Nożna", "piłka nożna", "PIŁKA NOŻNA", "Pilka Nozna");
            MapDiscipline(migrationBuilder, "basketball",
                "Koszykowka", "Koszykówka", "koszykówka", "KOSZYKÓWKA");
            MapDiscipline(migrationBuilder, "volleyball",
                "Siatkowka", "Siatkówka", "siatkówka", "SIATKÓWKA");
            MapDiscipline(migrationBuilder, "chess",
                "Szachy", "szachy", "Szachy Błyskawiczne", "Szachy blyskawiczne");
            MapDiscipline(migrationBuilder, "esports",
                "Esport", "E-sport", "E-Sport", "Esports", "Valorant", "LOL", "Fifa");
        }

        private static void MapDiscipline(MigrationBuilder migrationBuilder, string code, params string[] legacyValues)
        {
            var list = string.Join(", ", legacyValues.Select(v => "'" + v.Replace("'", "''") + "'"));
            migrationBuilder.Sql($"UPDATE Tournaments SET Discipline = '{code}' WHERE Discipline IN ({list});");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nie przywracamy dawnych nazw: kody są bezpieczniejsze, a katalog nadal je rozpoznaje.
        }
    }
}
