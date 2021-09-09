using Microsoft.EntityFrameworkCore.Migrations;

namespace FeedReader.ServerCore.Migrations
{
    public partial class AddCategoryToFeedItem : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "FeedItems",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FeedItems_Category",
                table: "FeedItems",
                column: "Category");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FeedItems_Category",
                table: "FeedItems");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "FeedItems");
        }
    }
}
