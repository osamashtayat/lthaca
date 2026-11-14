using Ithaca.Application.Common;

namespace Ithaca.Application.Users;

/// <summary>
/// The signed-in user's profile and account (api-contract.md §14): display name, password change, account deletion.
/// Contract stub until BE-16.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822", Justification = ContractStub.Justification)]
public sealed class ProfileService
{
    public Task<Result<ProfileDto>> GetAsync(CancellationToken ct) => ContractStub.Of<ProfileDto>();

    /// <summary>Sets the display name (trimmed); room member names already shown keep theirs.</summary>
    public Task<Result<ProfileDto>> UpdateAsync(UpdateProfileRequest request, CancellationToken ct) => ContractStub.Of<ProfileDto>();

    /// <summary>Checks the current password, sets the new one, keeps this session and ends the user's other sessions.</summary>
    public Task<Result> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct) => ContractStub.Command();

    /// <summary>
    /// Checks the password, closes the open rooms the user hosts, deletes the account and everything that belongs to it,
    /// and signs the caller out.
    /// </summary>
    public Task<Result> DeleteAccountAsync(DeleteAccountRequest request, CancellationToken ct) => ContractStub.Command();
}
