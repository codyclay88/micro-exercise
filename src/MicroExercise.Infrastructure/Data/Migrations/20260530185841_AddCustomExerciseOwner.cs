using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroExercise.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomExerciseOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OwnerUserId",
                table: "ExerciseTypes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "ExerciseTypes",
                keyColumn: "Id",
                keyValue: 1,
                column: "OwnerUserId",
                value: null);

            migrationBuilder.UpdateData(
                table: "ExerciseTypes",
                keyColumn: "Id",
                keyValue: 2,
                column: "OwnerUserId",
                value: null);

            migrationBuilder.UpdateData(
                table: "ExerciseTypes",
                keyColumn: "Id",
                keyValue: 3,
                column: "OwnerUserId",
                value: null);

            migrationBuilder.UpdateData(
                table: "ExerciseTypes",
                keyColumn: "Id",
                keyValue: 4,
                column: "OwnerUserId",
                value: null);

            migrationBuilder.UpdateData(
                table: "ExerciseTypes",
                keyColumn: "Id",
                keyValue: 5,
                column: "OwnerUserId",
                value: null);

            migrationBuilder.UpdateData(
                table: "ExerciseTypes",
                keyColumn: "Id",
                keyValue: 6,
                column: "OwnerUserId",
                value: null);

            migrationBuilder.UpdateData(
                table: "ExerciseTypes",
                keyColumn: "Id",
                keyValue: 7,
                column: "OwnerUserId",
                value: null);

            migrationBuilder.UpdateData(
                table: "ExerciseTypes",
                keyColumn: "Id",
                keyValue: 8,
                column: "OwnerUserId",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_ExerciseTypes_OwnerUserId",
                table: "ExerciseTypes",
                column: "OwnerUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ExerciseTypes_AspNetUsers_OwnerUserId",
                table: "ExerciseTypes",
                column: "OwnerUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExerciseTypes_AspNetUsers_OwnerUserId",
                table: "ExerciseTypes");

            migrationBuilder.DropIndex(
                name: "IX_ExerciseTypes_OwnerUserId",
                table: "ExerciseTypes");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "ExerciseTypes");
        }
    }
}
