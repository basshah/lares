using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lares.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAutomations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Automations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HomeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    TriggerType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TriggerTimeOfDay = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    TriggerDaysOfWeekCsv = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    TriggerDeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    TriggerState = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    LastTriggeredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Automations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Automations_Devices_TriggerDeviceId",
                        column: x => x.TriggerDeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Automations_Homes_HomeId",
                        column: x => x.HomeId,
                        principalTable: "Homes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AutomationSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AutomationId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ParamsJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomationSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AutomationSteps_Automations_AutomationId",
                        column: x => x.AutomationId,
                        principalTable: "Automations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AutomationSteps_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Automations_HomeId",
                table: "Automations",
                column: "HomeId");

            migrationBuilder.CreateIndex(
                name: "IX_Automations_TriggerDeviceId",
                table: "Automations",
                column: "TriggerDeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_AutomationSteps_AutomationId_Order",
                table: "AutomationSteps",
                columns: new[] { "AutomationId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationSteps_DeviceId",
                table: "AutomationSteps",
                column: "DeviceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AutomationSteps");

            migrationBuilder.DropTable(
                name: "Automations");
        }
    }
}
