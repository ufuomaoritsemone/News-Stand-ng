using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NewsApi.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthorAndContentType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Author",
                table: "Articles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                table: "Articles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Articles_ContentType",
                table: "Articles",
                column: "ContentType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Articles_ContentType",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "Author",
                table: "Articles");

            migrationBuilder.DropColumn(
                name: "ContentType",
                table: "Articles");
        }
    }
}
