using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaluExamPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAcademicYearStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "AcademicYears",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "AcademicYears");
        }
    }
}
