using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaluExamPortal.Infrastructure.UniversityAdmission.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreateUniversityAdmissionDatabase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UniversityAdmissionApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ApplicationNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    FormStateJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UniversityAdmissionApplications", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UniversityAdmissionApplications_ApplicationNumber",
                table: "UniversityAdmissionApplications",
                column: "ApplicationNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UniversityAdmissionApplications_UserId",
                table: "UniversityAdmissionApplications",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UniversityAdmissionApplications");
        }
    }
}
