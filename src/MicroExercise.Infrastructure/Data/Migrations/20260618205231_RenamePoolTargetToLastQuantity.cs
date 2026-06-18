using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroExercise.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenamePoolTargetToLastQuantity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TargetQuantity",
                table: "ExercisePool",
                newName: "LastQuantity");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "LastQuantity",
                table: "ExercisePool",
                newName: "TargetQuantity");
        }
    }
}
