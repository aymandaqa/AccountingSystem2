using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierShowAccountStatement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ShowAccountStatement",
                table: "Suppliers",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShowAccountStatement",
                table: "Suppliers");
        }
    }
}
