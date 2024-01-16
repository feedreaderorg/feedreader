using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FeedReader.ServerCore.Migrations
{
    /// <inheritdoc />
    public partial class AddTimestampToEventsTableAddDisableRefreshToFeedsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DisableRefresh",
                table: "FeedInfos",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "Timestamp",
                table: "Events",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisableRefresh",
                table: "FeedInfos");

            migrationBuilder.DropColumn(
                name: "Timestamp",
                table: "Events");
        }
    }
}
