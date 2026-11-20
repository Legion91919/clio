using System.Reflection;
using Clio.Application;
using Clio.Domain;
using Clio.Infrastructure;
using Xunit;

namespace Clio.Architecture.Tests;

/// <summary>Clean Architecture rule: dependencies point inwards (Api -> Infrastructure -> Application -> Domain).</summary>
public class DependencyDirectionTests
{
    private static readonly Assembly DomainAssembly = typeof(TermSheetDocument).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(TermSheetService).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(DependencyInjection).Assembly;
    private static readonly Assembly ApiAssembly = typeof(Clio.Api.ClioApp).Assembly;

    private static string[] References(Assembly a) => a.GetReferencedAssemblies().Select(r => r.Name!).ToArray();

    private static bool IsFramework(string name) => name.StartsWith("System", StringComparison.Ordinal) || name is "netstandard" or "mscorlib";

    [Fact]
    public void Domain_depends_only_on_the_framework_and_the_shared_kernel() =>
        Assert.All(References(DomainAssembly), r => Assert.True(IsFramework(r) || r == "Agentic.Blocks.Core", $"Domain must not reference {r}"));

    [Fact]
    public void Application_depends_only_on_domain_and_the_shared_kernel() =>
        Assert.All(References(ApplicationAssembly), r => Assert.True(IsFramework(r) || r is "Agentic.Blocks.Core" or "Clio.Domain", $"Application must not reference {r}"));

    [Fact]
    public void Infrastructure_never_references_the_api()
    {
        Assert.DoesNotContain(References(InfrastructureAssembly), r => r == ApiAssembly.GetName().Name);
        Assert.DoesNotContain(References(DomainAssembly), r => r.StartsWith("Clio.", StringComparison.Ordinal));
    }

    [Fact]
    public void Inner_layers_have_no_web_or_hosting_types() =>
        Assert.All(References(DomainAssembly).Concat(References(ApplicationAssembly)), r => Assert.False(r.StartsWith("Microsoft.", StringComparison.Ordinal), $"{r} leaks into an inner layer"));

    [Fact]
    public void Infrastructure_keeps_its_adapters_internal()
    {
        var allowed = new[] { nameof(DependencyInjection), nameof(EvalSetLoader) };
        var exposed = InfrastructureAssembly.GetExportedTypes().Select(t => t.Name).ToArray();
        Assert.All(exposed, t => Assert.True(allowed.Contains(t), $"Infrastructure type {t} should be internal"));
    }
}
