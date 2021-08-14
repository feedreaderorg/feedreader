using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FeedReader.WebServer.Migrations
{
    public partial class AddUserTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserOAuthIds",
                columns: table => new
                {
                    OAuthIssuer = table.Column<string>(type: "text", nullable: false),
                    OAuthId = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserOAuthIds", x => new { x.OAuthIssuer, x.OAuthId });
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RegistrationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserOAuthIds");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
