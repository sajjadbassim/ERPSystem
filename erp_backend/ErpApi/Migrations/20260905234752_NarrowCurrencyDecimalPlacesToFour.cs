using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpApi.Migrations
{
    // ترحيل مستقل بغرض واحد: تضييق CK_Currency_DecimalPlaces من 0..6 إلى 0..4.
    //
    // السبب: FxRoundingToleranceBase يُشتق بالصيغة 10^(−DecimalPlaces) (R-AMT-07-a)
    // ويُخزَّن في عمود HasPrecision(19, 4). فعملة بخمس خانات تُنتج 0.00001 ويقطعه
    // العمود إلى 0.0000 — أي تسامح صفري صامت لا القيمة المقصودة. السقف الجديد يجعل
    // ما لا يمكن تمثيله مستحيل الإدخال، بدل أن يمرّ ويُقطع بلا إعلان.
    //
    // آمن على البيانات القائمة: العملات المبذورة 0 و2 خانة، وأعلى عملة واقعية 3 (KWD).
    /// <inheritdoc />
    public partial class NarrowCurrencyDecimalPlacesToFour : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Currency_DecimalPlaces",
                table: "Currencies");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Currency_DecimalPlaces",
                table: "Currencies",
                sql: "[DecimalPlaces] >= 0 AND [DecimalPlaces] <= 4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Currency_DecimalPlaces",
                table: "Currencies");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Currency_DecimalPlaces",
                table: "Currencies",
                sql: "[DecimalPlaces] >= 0 AND [DecimalPlaces] <= 6");
        }
    }
}
