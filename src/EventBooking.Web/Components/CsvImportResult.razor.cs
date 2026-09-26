using Microsoft.AspNetCore.Components;
using EventBooking.Web.Services;

namespace EventBooking.Web.Components;

public partial class CsvImportResult
{
    [Parameter] public bool Accepted { get; set; }
    [Parameter] public IReadOnlyList<FieldProblem> Errors { get; set; } = [];
    [Parameter] public int ImportedCount { get; set; }
}
