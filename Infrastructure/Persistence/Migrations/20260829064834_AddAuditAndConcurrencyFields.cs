using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaluExamPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditAndConcurrencyFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Seats",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModifiedAt",
                table: "Seats",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                table: "Seats",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Seats",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Results",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModifiedAt",
                table: "Results",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                table: "Results",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Results",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Fees",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModifiedAt",
                table: "Fees",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                table: "Fees",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Fees",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "EnrollmentWindows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModifiedAt",
                table: "EnrollmentWindows",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                table: "EnrollmentWindows",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "EnrollmentWindows",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Enrollments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModifiedAt",
                table: "Enrollments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                table: "Enrollments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Enrollments",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Colleges",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModifiedAt",
                table: "Colleges",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                table: "Colleges",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Colleges",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "CollegePrograms",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModifiedAt",
                table: "CollegePrograms",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                table: "CollegePrograms",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "CollegePrograms",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModifiedAt",
                table: "AuditLogs",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "AuditLogs",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "AdmitCards",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModifiedAt",
                table: "AdmitCards",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                table: "AdmitCards",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "AdmitCards",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "AcademicYears",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModifiedAt",
                table: "AcademicYears",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                table: "AcademicYears",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "AcademicYears",
                type: "rowversion",
                rowVersion: true,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Seats");

            migrationBuilder.DropColumn(
                name: "LastModifiedAt",
                table: "Seats");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                table: "Seats");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Seats");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Results");

            migrationBuilder.DropColumn(
                name: "LastModifiedAt",
                table: "Results");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                table: "Results");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Results");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Fees");

            migrationBuilder.DropColumn(
                name: "LastModifiedAt",
                table: "Fees");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                table: "Fees");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Fees");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "EnrollmentWindows");

            migrationBuilder.DropColumn(
                name: "LastModifiedAt",
                table: "EnrollmentWindows");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                table: "EnrollmentWindows");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "EnrollmentWindows");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "LastModifiedAt",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Colleges");

            migrationBuilder.DropColumn(
                name: "LastModifiedAt",
                table: "Colleges");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                table: "Colleges");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Colleges");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "CollegePrograms");

            migrationBuilder.DropColumn(
                name: "LastModifiedAt",
                table: "CollegePrograms");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                table: "CollegePrograms");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "CollegePrograms");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "LastModifiedAt",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "AdmitCards");

            migrationBuilder.DropColumn(
                name: "LastModifiedAt",
                table: "AdmitCards");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                table: "AdmitCards");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "AdmitCards");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "AcademicYears");

            migrationBuilder.DropColumn(
                name: "LastModifiedAt",
                table: "AcademicYears");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                table: "AcademicYears");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "AcademicYears");
        }
    }
}
