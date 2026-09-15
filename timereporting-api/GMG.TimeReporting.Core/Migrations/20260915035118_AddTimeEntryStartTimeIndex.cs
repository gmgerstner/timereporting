using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GMG.TimeReporting.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddTimeEntryStartTimeIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_TimeEntries_StartTime",
                table: "TimeEntries",
                column: "StartTime");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TimeEntries_StartTime",
                table: "TimeEntries");
        }
    }
}
