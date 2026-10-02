using System.Reflection;
using Company.Product.Scheduler.Managers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetArchTest.Rules;

namespace Company.Product.Scheduler.Tests.Architecture;

/// <summary>
/// Enforces the iDesign rules in CLAUDE.md. If one of these fails, fix the code, not the test.
/// </summary>
public class ArchitectureTests
{
    private const string Root = "Company.Product.Scheduler";
    private const string JobsNs = Root + ".Jobs";
    private const string ManagersNs = Root + ".Managers";
    private const string EnginesNs = Root + ".Engines";
    private const string AccessorsNs = Root + ".Accessors";
    private const string UtilitiesNs = Root + ".Utilities";
        private const string DtosNs = ManagersNs + ".Models";
    private const string EntitiesNs = AccessorsNs + ".Models";

    /// <summary>Accessor sub-namespaces that hold models, mapping, setup or exceptions rather than Accessors.</summary>
    private static readonly string[] NonComponentSegments = ["Models", "Mapping", "Setup", "Exceptions"];

    private static readonly Assembly Scheduler = typeof(SalesSummaryManager).Assembly;

    [Fact]
    public void Jobs_inject_only_managers_and_utilities()
    {
        var violations = CheckInjection(TypesIn(JobsNs), p =>
            IsUtilityEquivalent(p) || (p.IsInterface && p.Namespace == ManagersNs));

        Assert.Empty(violations);
    }

    [Fact]
    public void Managers_inject_only_engines_accessors_and_utilities()
    {
        var violations = CheckInjection(TypesIn(ManagersNs), p =>
            IsUtilityEquivalent(p)
            || (p.IsInterface && p.Namespace == EnginesNs)
            || (p.IsInterface && IsAccessorComponentNamespace(p.Namespace)));

        Assert.Empty(violations);
    }

    [Fact]
    public void Engines_inject_only_engines_and_utilities()
    {
        var violations = CheckInjection(TypesIn(EnginesNs), p =>
            IsUtilityEquivalent(p) || (p.IsInterface && p.Namespace == EnginesNs));

        Assert.Empty(violations);
    }

    [Fact]
    public void Accessors_do_not_inject_other_layers()
    {
        var violations = CheckInjection(AccessorComponents(), p =>
            !(p.IsInterface && IsAccessorComponentNamespace(p.Namespace))
            && !IsIn(p, JobsNs)
            && !IsIn(p, ManagersNs)
            && !IsIn(p, EnginesNs));

        Assert.Empty(violations);
    }

    [Fact]
    public void HttpClient_is_never_injected_directly()
    {
        var violations = ConcreteClasses()
            .SelectMany(t => Injected(t)
                .Where(p => p == typeof(HttpClient))
                .Select(p => Describe(t, p) + " (inject IHttpClientFactory and call CreateClient(name) instead)"))
            .ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Only_accessors_use_http_factories()
    {
        var violations = ConcreteClasses()
            .Where(t => !IsIn(t, AccessorsNs))
            .SelectMany(t => Injected(t)
                .Where(p => p == typeof(IHttpClientFactory))
                .Select(p => Describe(t, p)))
            .ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Engines_do_no_io()
    {
        var result = Types.InAssembly(Scheduler)
            .That().ResideInNamespace(EnginesNs)
            .ShouldNot().HaveDependencyOnAny("System.Net.Http", "System.IO", "Microsoft.Extensions.Caching", AccessorsNs)
            .GetResult();

        Assert.True(result.IsSuccessful, Failing(result));
    }

    [Fact]
    public void Utilities_do_not_depend_on_layers()
    {
        var result = Types.InAssembly(Scheduler)
            .That().ResideInNamespace(UtilitiesNs)
            .ShouldNot().HaveDependencyOnAny(JobsNs, ManagersNs, EnginesNs, AccessorsNs)
            .GetResult();

        Assert.True(result.IsSuccessful, Failing(result));
    }

    [Fact]
    public void Entities_stay_inside_accessors()
    {
        var result = Types.InAssembly(Scheduler)
            .That().DoNotResideInNamespace(AccessorsNs)
            .ShouldNot().HaveDependencyOn(EntitiesNs)
            .GetResult();

        Assert.True(result.IsSuccessful, Failing(result));
    }

    [Fact]
    public void Jobs_run_through_the_guard()
    {
        // Every job must use JobRunGuard, so overlap protection, logs and metrics are the same for all jobs.
        var jobs = TypesIn(JobsNs).Where(t => t.Name.EndsWith("Job", StringComparison.Ordinal)).ToList();

        Assert.NotEmpty(jobs);

        var violations = jobs
            .Where(job => !Injected(job).Any(p => p.Name == "JobRunGuard"))
            .Select(job => $"{job.FullName} does not inject JobRunGuard")
            .ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Components_have_matching_interface()
    {
        var components = TypesIn(ManagersNs).Concat(TypesIn(EnginesNs)).Concat(AccessorComponents()).ToList();

        Assert.NotEmpty(components);

        var violations = components
            .Where(t => t.GetInterfaces().All(i => i.Name != "I" + t.Name))
            .Select(t => $"{t.FullName} does not implement I{t.Name}")
            .ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Model_suffixes_match_tier()
    {
        var violations = AllTypesIn(DtosNs).Where(t => !t.Name.EndsWith("Dto", StringComparison.Ordinal))
            .Concat(AllTypesIn(EntitiesNs).Where(t => !t.Name.EndsWith("Entity", StringComparison.Ordinal)))
            .Select(t => t.FullName)
            .ToList();

        Assert.Empty(violations);
    }

    /// <summary>Top-level, non-compiler-generated types declared directly in a namespace.</summary>
    private static IEnumerable<Type> AllTypesIn(string ns) =>
        Scheduler.GetTypes().Where(t => t.Namespace == ns && !t.IsNested && !t.Name.StartsWith('<'));

    /// <summary>Concrete classes declared directly in a namespace.</summary>
    private static List<Type> TypesIn(string ns) =>
        AllTypesIn(ns).Where(t => t is { IsClass: true, IsAbstract: false }).ToList();

    /// <summary>Concrete Accessor classes: in <c>Accessors</c> or a sub-namespace such as <c>Accessors.Http</c>.</summary>
    private static List<Type> AccessorComponents() =>
        Scheduler.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsNested: false }
                && !t.Name.StartsWith('<')
                && IsAccessorComponentNamespace(t.Namespace))
            .ToList();

    private static bool IsAccessorComponentNamespace(string? ns) =>
        ns is not null
        && (ns == AccessorsNs || ns.StartsWith(AccessorsNs + ".", StringComparison.Ordinal))
        && !NonComponentSegments.Contains(ns.Split('.')[^1]);

    private static IEnumerable<Type> ConcreteClasses() =>
        Scheduler.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false } && !t.Name.StartsWith('<'));

    private static List<string> CheckInjection(List<Type> components, Func<Type, bool> isAllowed)
    {
        Assert.NotEmpty(components);

        return components
            .SelectMany(c => Injected(c).Where(p => !isAllowed(p)).Select(p => Describe(c, p)))
            .ToList();
    }

    private static IEnumerable<Type> Injected(Type type) =>
        type.GetConstructors().SelectMany(c => c.GetParameters()).Select(p => p.ParameterType);

    /// <summary>Framework abstractions CLAUDE.md treats as Utilities, plus anything in the Utilities namespace.</summary>
    private static bool IsUtilityEquivalent(Type type)
    {
        if (type == typeof(TimeProvider) || IsIn(type, UtilitiesNs))
        {
            return true;
        }

        if (type.FullName == "Microsoft.FeatureManagement.IFeatureManager")
        {
            return true;
        }

        if (!type.IsGenericType)
        {
            return false;
        }

        var definition = type.GetGenericTypeDefinition();

        return definition == typeof(ILogger<>)
            || definition == typeof(IOptions<>)
            || definition == typeof(IOptionsMonitor<>)
            || definition == typeof(IOptionsSnapshot<>);
    }

    private static bool IsIn(Type type, string ns) =>
        type.Namespace == ns || (type.Namespace?.StartsWith(ns + ".", StringComparison.Ordinal) ?? false);

    private static string Describe(Type component, Type injected) =>
        $"{component.FullName} injects {injected.FullName ?? injected.Name}";

    private static string Failing(TestResult result) =>
        "Violating types: " + string.Join(", ", result.FailingTypeNames ?? []);
}
