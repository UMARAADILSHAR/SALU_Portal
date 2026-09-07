using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaluExamPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateEnrollmentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BoardName",
                table: "Enrollments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DivisionObtained",
                table: "Enrollments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EligibilityCertNo",
                table: "Enrollments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExamFromSalu",
                table: "Enrollments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MigrationDistrict",
                table: "Enrollments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MigrationProvince",
                table: "Enrollments",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Nationality",
                table: "Enrollments",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "PassingYear",
                table: "Enrollments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceNumber",
                table: "Enrollments",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Religion",
                table: "Enrollments",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SaluExamYear",
                table: "Enrollments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SaluSeatNo",
                table: "Enrollments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_ReferenceNumber",
                table: "Enrollments",
                column: "ReferenceNumber",
                unique: true,
                filter: "[ReferenceNumber] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Enrollments_ReferenceNumber",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "BoardName",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "DivisionObtained",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "EligibilityCertNo",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "ExamFromSalu",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "MigrationDistrict",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "MigrationProvince",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "Nationality",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "PassingYear",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "ReferenceNumber",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "Religion",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "SaluExamYear",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "SaluSeatNo",
                table: "Enrollments");
        }
    }
}
