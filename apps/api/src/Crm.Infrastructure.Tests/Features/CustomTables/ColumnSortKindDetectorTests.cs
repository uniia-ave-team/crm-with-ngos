using Crm.Infrastructure.Persistence.Sorting;
using Shouldly;

namespace Crm.Infrastructure.Tests.Features.CustomTables;

/// <summary>
/// Unit tests for <see cref="ColumnSortKindDetector"/> verifying type inference across numeric,
/// boolean, ISO date, ISO timestamp, text, empty, and mixed-value column samples.
/// </summary>
public class ColumnSortKindDetectorTests
{
    [Fact]
    public void DetectWhenSampleIsEmptyOrOnlyWhitespaceShouldReturnText()
    {
        ColumnSortKindDetector.Detect([]).ShouldBe(InferredSortKind.Text);
        ColumnSortKindDetector.Detect([string.Empty, " ", "\t\r\n"]).ShouldBe(InferredSortKind.Text);
    }

    [Fact]
    public void DetectWhenValuesAreIntegersNegativesAndDecimalsShouldReturnNumber()
    {
        string[] sample = ["0", "2", "10", "-5", "10.5", "+3.14", "1e3", "  42  "];

        ColumnSortKindDetector.Detect(sample).ShouldBe(InferredSortKind.Number);
    }

    [Fact]
    public void DetectWhenValuesContainCommasOrNonNumericStringsShouldReturnText()
    {
        string[] sample = ["1,000", "2,500", "10,000"];

        ColumnSortKindDetector.Detect(sample).ShouldBe(InferredSortKind.Text);
    }

    [Fact]
    public void DetectWhenValuesAreBooleansShouldReturnBoolean()
    {
        string[] sample = ["true", "false", "TRUE", "False", " true "];

        ColumnSortKindDetector.Detect(sample).ShouldBe(InferredSortKind.Boolean);
    }

    [Fact]
    public void DetectWhenValuesAreIsoDatesShouldReturnDate()
    {
        string[] sample = ["2026-01-15", "2026-05-20", "2025-12-31", " 2024-02-29 "];

        ColumnSortKindDetector.Detect(sample).ShouldBe(InferredSortKind.Date);
    }

    [Fact]
    public void DetectWhenValuesAreIsoDateTimesShouldReturnDateTime()
    {
        string[] sample =
        [
            "2026-01-15T10:30:00Z",
            "2026-05-20T14:00:00+02:00",
            "2025-12-31t23:59:59.123Z",
        ];

        ColumnSortKindDetector.Detect(sample).ShouldBe(InferredSortKind.DateTime);
    }

    [Fact]
    public void DetectWhenColumnHasAtLeastEightyPercentNumbersAndTextOutliersShouldReturnNumber()
    {
        // 4 out of 5 non-empty values are numeric (80%), empty values are ignored.
        string[] sample = ["10", "20", "30", "40", "уточнити", string.Empty, "   "];

        ColumnSortKindDetector.Detect(sample).ShouldBe(InferredSortKind.Number);
    }

    [Fact]
    public void DetectWhenColumnHasBelowEightyPercentNumbersShouldFallbackToText()
    {
        // 3 out of 4 non-empty values are numeric (75% < 80%).
        string[] sample = ["10", "20", "30", "уточнити"];

        ColumnSortKindDetector.Detect(sample).ShouldBe(InferredSortKind.Text);
    }

    [Fact]
    public void DetectWhenValuesAreMultilingualPlainTextShouldReturnText()
    {
        string[] sample = ["Alpha", "Beta", "Запис 2", "Contract 10", "Qırım", "Беларусь"];

        ColumnSortKindDetector.Detect(sample).ShouldBe(InferredSortKind.Text);
    }
}
