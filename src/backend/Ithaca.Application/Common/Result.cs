using System.Diagnostics.CodeAnalysis;

namespace Ithaca.Application.Common;

/// <summary>
/// The one success-or-error type returned by Application services (ADR-0004). Services return an <see cref="Error"/>
/// for expected failures (not found, validation, business rules) and throw only for bugs and infrastructure faults.
/// Controllers and hubs turn it into a response with <c>ResultExtensions.ToActionResult</c>; they never branch on it.
/// </summary>
public class Result
{
    protected Result(Error? error) => Error = error;

    public Error? Error { get; }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public static Result Success() => new(null);

    public static Result Failure(Error error) => new(error ?? throw new ArgumentNullException(nameof(error)));

    public static implicit operator Result(Error error) => Failure(error);
}

/// <summary>A <see cref="Result"/> carrying a value on success.</summary>
#pragma warning disable CA1000 // Static factory members on a generic type are the point of a Result type.
public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T value) : base(null) => _value = value;

    private Result(Error error) : base(error) { }

    /// <summary>The value; throws when read from a failure (a programming error, never a user error).</summary>
    public T Value => IsSuccess ? _value! : throw new InvalidOperationException($"No value: the result failed with '{Error.Code}'.");

    public static Result<T> Success(T value) => new(value);

    public static new Result<T> Failure(Error error) => new(error ?? throw new ArgumentNullException(nameof(error)));

    public static implicit operator Result<T>(T value) => Success(value);

    public static implicit operator Result<T>(Error error) => Failure(error);
}
#pragma warning restore CA1000
