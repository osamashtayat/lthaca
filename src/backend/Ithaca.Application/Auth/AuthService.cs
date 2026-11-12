using Ithaca.Application.Common;

namespace Ithaca.Application.Auth;

/// <summary>Sessions for users and guests (ADR-0012, api-contract.md §4). Contract stub until BE-02.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822", Justification = ContractStub.Justification)]
public sealed class AuthService
{
    public Task<Result<CurrentUserDto>> GetCurrentAsync(CancellationToken ct) => ContractStub.Of<CurrentUserDto>();

    public Task<Result<CurrentUserDto>> RegisterAsync(RegisterRequest request, CancellationToken ct) => ContractStub.Of<CurrentUserDto>();

    public Task<Result<CurrentUserDto>> SignInAsync(SignInRequest request, CancellationToken ct) => ContractStub.Of<CurrentUserDto>();

    public Task<Result> SignOutAsync(CancellationToken ct) => ContractStub.Command();
}
