using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenbaNippo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkItemVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "WorkItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1); // 既存の工程は版番号 1 から始める
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Version",
                table: "WorkItems");
        }
    }
}
