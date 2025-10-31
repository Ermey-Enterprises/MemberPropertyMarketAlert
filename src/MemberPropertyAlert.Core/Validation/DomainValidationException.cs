using System;
using System.Collections.Generic;

namespace MemberPropertyAlert.Core.Validation;

public sealed class DomainValidationException : Exception
{
    public DomainValidationException(string message, IReadOnlyCollection<string>? errors = null)
        : base(message)
    {
        Errors = errors ?? Array.Empty<string>();
    }

    public IReadOnlyCollection<string> Errors { get; }
}
