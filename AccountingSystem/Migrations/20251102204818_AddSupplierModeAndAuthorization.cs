using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierModeAndAuthorization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AuthorizedOperations",
                table: "Suppliers",
                type: "int",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<string>(
                name: "CreatedById",
                table: "Suppliers",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Mode",
                table: "Suppliers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("UPDATE Suppliers SET AuthorizedOperations = 3 WHERE AuthorizedOperations = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_CreatedById",
                table: "Suppliers",
                column: "CreatedById");

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_AspNetUsers_CreatedById",
                table: "Suppliers",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_AspNetUsers_CreatedById",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_CreatedById",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "AuthorizedOperations",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "Mode",
                table: "Suppliers");
        }
    }
}
