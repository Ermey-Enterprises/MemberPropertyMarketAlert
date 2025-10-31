using System;
using System.Collections.Generic;

namespace MemberPropertyAlert.Core.Validation;

public static class Guard
{
    public static string AgainstNullOrWhiteSpace(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{parameterName} cannot be null or whitespace.", parameterName);
        }

        return value;
    }

    public static T AgainstNull<T>(T? value, string parameterName) where T : class
    {
        return value ?? throw new ArgumentNullException(parameterName);
    }

    public static IReadOnlyCollection<T> AgainstEmpty<T>(IReadOnlyCollection<T> collection, string parameterName)
    {
        if (collection.Count == 0)
        {
            throw new ArgumentException($"{parameterName} must contain at least one item.", parameterName);
        }

        return collection;
    }
}
