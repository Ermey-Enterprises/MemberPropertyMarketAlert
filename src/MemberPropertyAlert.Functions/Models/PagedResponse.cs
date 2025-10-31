using System.Collections.Generic;

namespace MemberPropertyAlert.Functions.Models;

public sealed record PagedResponse<T>(IReadOnlyCollection<T> Items, long TotalCount, int PageNumber, int PageSize);
