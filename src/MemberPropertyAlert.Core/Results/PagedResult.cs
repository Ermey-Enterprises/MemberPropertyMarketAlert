using System.Collections.Generic;

namespace MemberPropertyAlert.Core.Results;

public sealed record PagedResult<T>(IReadOnlyCollection<T> Items, long TotalCount, int PageNumber, int PageSize);
