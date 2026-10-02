using System.Reflection;
using Company.Product.Api.Managers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetArchTest.Rules;

namespace Company.Product.Api.Tests.Architecture;

/// <summary>
/// Enforces the iDesign rules in CLAUDE.md. If one of these fails, fix the code, not the test.
/// </summary>
public class ArchitectureTests
{
    private const string Root = "Company.Product.Api";
    private const string ControllersNs = Root + ".Controllers";
    private const string ManagersNs = Root + ".Managers";
    private const string EnginesNs = Root + ".Engines";
    private const string AccessorsNs = Root + ".Accessors";
    private const string UtilitiesNs = Root + ".Utilities";
    private const string ApiModelsNs = Root + ".Models";
    private const string DtosNs = ManagersNs + ".Models";
    private const string EntitiesNs = AccessorsNs + ".Models";
    private const string HttpAccessorsNs = AccessorsNs + ".Http";

    /// <summary>Namespaces that hold Accessor components: database Accessors and HTTP Accessors.</summary>
    private static readonly string[] AccessorComponentNs = [AccessorsNs, HttpAccessorsNs];

    private static readonly Assembly Api = typeof(OrderManager).Assembly;

    [Fact]
    public void Controllers_inject_only_managers()
    {
        var controllers = Api.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .ToList();

        Assert.NotEmpty(controllers);

        var violations = controllers
            .SelectMany(c => Injected(c)
                .Where(p => !(p.IsInterface && p.Namespace == ManagersNs))
                .Select(p => Describe(c, p)))
            .ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Managers_inject_only_engines_accessors_and_utilities()
    {
        var violations = CheckInjection([ManagersNs], p =>
            IsUtilityEquivalent(p)
            || (p.IsInterface && p.Namespace == EnginesNs)
            || IsAccessorInterface(p));

        Assert.Empty(violations);
    }

    [Fact]
    public void Engines_inject_only_engines_and_utilities()
    {
        var violations = CheckInjection([EnginesNs], p =>
            IsUtilityEquivalent(p)
            || (p.IsInterface && p.Namespace == EnginesNs));

        Assert.Empty(violations);
    }

    [Fact]
    public void Accessors_do_not_inject_other_layers()
    {
        var violations = CheckInjection(AccessorComponentNs, p =>
            !IsAccessorInterface(p)
            && !IsIn(p, ControllersNs)
            && !IsIn(p, ManagersNs)
            && !IsIn(p, EnginesNs));

        Assert.Empty(violations);
    }

    [Fact]
    public void DbContext_is_never_injected_directly()
    {
        // Components are Singletons: a DbContext must come from IDbContextFactory, one short-lived context per call.
        var violations = ConcreteClasses()
            .SelectMany(t => Injected(t)
                .Where(p => typeof(DbContext).IsAssignableFrom(p))
                .Select(p => Describe(t, p) + " (inject IDbContextFactory<T> instead)"))
            .ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void HttpClient_is_never_injected_directly()
    {
        // Typed clients are transient; a Singleton that captures one never sees DNS or handler changes.
        var violations = ConcreteClasses()
            .SelectMany(t => Injected(t)
                .Where(p => p == typeof(HttpClient))
                .Select(p => Describe(t, p) + " (inject IHttpClientFactory and call CreateClient(name) instead)"))
            .ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Only_accessors_use_database_or_http_factories()
    {
        var violations = ConcreteClasses()
            .Where(t => !IsIn(t, AccessorsNs))
            .SelectMany(t => Injected(t)
                .Where(p => p == typeof(IHttpClientFactory)
                    || (p.IsGenericType && p.GetGenericTypeDefinition() == typeof(IDbContextFactory<>)))
                .Select(p => Describe(t, p)))
            .ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Engines_do_no_io()
    {
        var result = Types.InAssembly(Api)
            .That().ResideInNamespace(EnginesNs)
            .ShouldNot().HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Npgsql",
                "System.Net.Http",
                "System.IO",
                "Microsoft.Extensions.Caching",
                AccessorsNs)
            .GetResult();

        Assert.True(result.IsSuccessful, Failing(result));
    }

    [Fact]
    public void Utilities_do_not_depend_on_layers()
    {
        var result = Types.InAssembly(Api)
            .That().ResideInNamespace(UtilitiesNs)
            .ShouldNot().HaveDependencyOnAny(ControllersNs, ManagersNs, EnginesNs, AccessorsNs)
            .GetResult();

        Assert.True(result.IsSuccessful, Failing(result));
    }

    [Fact]
    public void Entities_stay_inside_accessors()
    {
        var result = Types.InAssembly(Api)
            .That().DoNotResideInNamespace(AccessorsNs)
            .ShouldNot().HaveDependencyOn(EntitiesNs)
            .GetResult();

        Assert.True(result.IsSuccessful, Failing(result));
    }

    [Fact]
    public void Api_models_stay_in_controllers()
    {
        var result = Types.InAssembly(Api)
            .That().DoNotResideInNamespace(ControllersNs)
            .And().DoNotResideInNamespace(ApiModelsNs)
            .ShouldNot().HaveDependencyOn(ApiModelsNs)
            .GetResult();

        Assert.True(result.IsSuccessful, Failing(result));
    }

    [Fact]
    public void Components_have_matching_interface()
    {
        var components = new[] { ManagersNs, EnginesNs, AccessorsNs, HttpAccessorsNs }
            .SelectMany(ns => TypesIn(ns).Where(t => t is { IsClass: true, IsAbstract: false }))
            .ToList();

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
        var violations = TypesIn(DtosNs).Where(t => !t.Name.EndsWith("Dto", StringComparison.Ordinal))
            .Concat(TypesIn(EntitiesNs).Where(t => !t.Name.EndsWith("Entity", StringComparison.Ordinal)))
            .Concat(TypesIn(ApiModelsNs).Where(t =>
                t.Name.EndsWith("Dto", StringComparison.Ordinal)
                || t.Name.EndsWith("Entity", StringComparison.Ordinal)))
            .Select(t => t.FullName)
            .ToList();

        Assert.Empty(violations);
    }

    /// <summary>Top-level, non-compiler-generated types declared directly in a namespace (not sub-namespaces).</summary>
    private static IEnumerable<Type> TypesIn(string ns) =>
        Api.GetTypes().Where(t => t.Namespace == ns && !t.IsNested && !t.Name.StartsWith('<'));

    private static IEnumerable<Type> ConcreteClasses() =>
        Api.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false } && !t.Name.StartsWith('<'));

    private static bool IsAccessorInterface(Type type) =>
        type.IsInterface && type.Namespace is { } ns && AccessorComponentNs.Contains(ns);

    private static List<string> CheckInjection(string[] layerNamespaces, Func<Type, bool> isAllowed)
    {
        var components = layerNamespaces
            .SelectMany(ns => TypesIn(ns).Where(t => t is { IsClass: true, IsAbstract: false }))
            .ToList();

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
