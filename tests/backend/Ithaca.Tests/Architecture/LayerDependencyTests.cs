using System.Reflection;
using Ithaca.Api.Controllers;
using Ithaca.Api.Hubs;
using Ithaca.Application.Common;
using Ithaca.Domain.Films;
using Ithaca.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Ithaca.Tests.Architecture;

/// <summary>The layering rules of docs/architecture.md, enforced. A failure here means code is in the wrong layer.</summary>
public sealed class LayerDependencyTests
{
    private static readonly Assembly Domain = typeof(Film).Assembly;
    private static readonly Assembly Application = typeof(IAppDbContext).Assembly;
    private static readonly Assembly Infrastructure = typeof(AppDbContext).Assembly;
    private static readonly Assembly Api = typeof(FilmsController).Assembly;

    private static string[] References(Assembly assembly) =>
        [.. assembly.GetReferencedAssemblies().Select(a => a.Name!)];

    [Fact]
    public void Domain_ReferencesNoOtherLayerAndNoPackages() =>
        References(Domain).ShouldAllBe(name => name.StartsWith("System", StringComparison.Ordinal) || name == "netstandard");

    [Fact]
    public void Application_ReferencesOnlyDomain_AndNoProvider()
    {
        var references = References(Application);
        references.ShouldNotContain("Ithaca.Infrastructure");
        references.ShouldNotContain("Ithaca.Api");
        references.ShouldNotContain(name => name.StartsWith("Npgsql", StringComparison.Ordinal) || name.StartsWith("Pgvector", StringComparison.Ordinal));
        references.ShouldNotContain(name => name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
    }

    [Fact]
    public void Infrastructure_DoesNotReferenceApi() => References(Infrastructure).ShouldNotContain("Ithaca.Api");

    [Fact]
    public void ControllersAndHubs_NeverTakeADbContext()
    {
        var entryPoints = Api.GetTypes().Where(t => typeof(ControllerBase).IsAssignableFrom(t) || typeof(Hub).IsAssignableFrom(t));
        var offenders = entryPoints
            .SelectMany(t => t.GetConstructors().SelectMany(c => c.GetParameters()).Select(p => (t.Name, p.ParameterType)))
            .Where(x => typeof(DbContext).IsAssignableFrom(x.ParameterType) || x.ParameterType == typeof(IAppDbContext))
            .Select(x => x.Name);

        offenders.ShouldBeEmpty("Controllers and hubs call one service; only services use IAppDbContext.");
    }

    [Fact]
    public void Hubs_HaveNoLogicDependenciesBeyondServices()
    {
        typeof(RoomHub).GetConstructors().SelectMany(c => c.GetParameters())
            .ShouldAllBe(p => p.ParameterType.Assembly == Application, "A hub may only depend on Application services.");
    }

    [Fact]
    public void ApplicationServices_AreSealed() =>
        Application.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("Service", StringComparison.Ordinal))
            .ShouldAllBe(t => t.IsSealed);
}
