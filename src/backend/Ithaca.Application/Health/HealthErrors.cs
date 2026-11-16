using Ithaca.Application.Common;
using Ithaca.Domain.Errors;

namespace Ithaca.Application.Health;

public static class HealthErrors
{
    public static readonly Error DatabaseUnavailable = new(ErrorCodes.HealthDatabaseUnavailable, "The database can't be reached right now.");
}
