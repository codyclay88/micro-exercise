using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroExercise.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBurstResistance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BandLabel",
                table: "WorkoutLogs",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ResistanceAmount",
                table: "WorkoutLogs",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResistanceType",
                table: "WorkoutLogs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Bodyweight");

            migrationBuilder.AddColumn<string>(
                name: "WeightUnit",
                table: "WorkoutLogs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BandLabel",
                table: "WorkoutLogs");

            migrationBuilder.DropColumn(
                name: "ResistanceAmount",
                table: "WorkoutLogs");

            migrationBuilder.DropColumn(
                name: "ResistanceType",
                table: "WorkoutLogs");

            migrationBuilder.DropColumn(
                name: "WeightUnit",
                table: "WorkoutLogs");
        }
    }
}
