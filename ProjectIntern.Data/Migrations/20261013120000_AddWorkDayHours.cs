using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectIntern.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkDayHours : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeOnly>(
                name: "EndTime",
                table: "WorkDayAssignments",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "StartTime",
                table: "WorkDayAssignments",
                type: "time without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EndTime",
                table: "WorkDayAssignments");

            migrationBuilder.DropColumn(
                name: "StartTime",
                table: "WorkDayAssignments");
        }
    }
}
