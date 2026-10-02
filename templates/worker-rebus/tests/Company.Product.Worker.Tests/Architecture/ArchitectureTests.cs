using System.Reflection;
using Company.Product.Worker.Managers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetArchTest.Rules;
using Rebus.Bus;
using Rebus.Handlers;

namespace Company.Product.Worker.Tests.Architecture;

/// <summary>
/// Enforces the iDesign rules in CLAUDE.md. If one of these fails, fix the code, not the test.
/// </summary>
public class ArchitectureTests
{
    private const string Root = "Company.Product.Worker";
    private const string HandlersNs = Root + ".Handlers";
    private const string ManagersNs = Root + ".Managers";
    private const string EnginesNs = Root + ".Engines";
    private const string AccessorsNs = Root + ".Accessors";
    private const string UtilitiesNs = Root + ".Utilities";
    private const string HandlerModelsNs = HandlersNs + ".Models";
    private const string DtosNs = ManagersNs + ".Models";
    private const string EntitiesNs = AccessorsNs + ".Models";

    /// <summary>Accessor sub-namespaces that hold models, mapping, setup or exceptions rather than Accessors.</summary>
    private static readonly string[] NonComponentSegments = ["Models", "Mapping", "Setup", "Exceptions"];

    private static readonly Assembly Worker = typeof(OrderNotificationManager).Assembly;

    [Fact]
    public void Handlers_inject_only_managers_and_utilities()
    {
        var violations = CheckInjection(TypesIn(HandlersNs), p =>
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
            && !IsIn(p, HandlersNs)
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
    public void Only_accessors_use_the_bus_or_http_factories()
    {
        var violations = ConcreteClasses()
            .Where(t => !IsIn(t, AccessorsNs))
            .SelectMany(t => Injected(t)
                .Where(p => p == typeof(IHttpClientFactory) || p == typeof(IBus))
                .Select(p => Describe(t, p)))
            .ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Engines_do_no_io()
    {
        var result = Types.InAssembly(Worker)
            .That().ResideInNamespace(EnginesNs)
            .ShouldNot().HaveDependencyOnAny("System.Net.Http", "System.IO", "Rebus", "Microsoft.Extensions.Caching", AccessorsNs)
            .GetResult();

        Assert.True(result.IsSuccessful, Failing(result));
    }

    [Fact]
    public void Utilities_do_not_depend_on_layers()
    {
        var result = Types.InAssembly(Worker)
            .That().ResideInNamespace(UtilitiesNs)
            .ShouldNot().HaveDependencyOnAny(HandlersNs, ManagersNs, EnginesNs, AccessorsNs)
            .GetResult();

        Assert.True(result.IsSuccessful, Failing(result));
    }

    [Fact]
    public void Entities_stay_inside_accessors()
    {
        var result = Types.InAssembly(Worker)
            .That().DoNotResideInNamespace(AccessorsNs)
            .ShouldNot().HaveDependencyOn(EntitiesNs)
            .GetResult();

        Assert.True(result.IsSuccessful, Failing(result));
    }

    [Fact]
    public void Handler_models_stay_in_handlers()
    {
        var result = Types.InAssembly(Worker)
            .That().DoNotResideInNamespace(HandlersNs)
            .ShouldNot().HaveDependencyOn(HandlerModelsNs)
            .GetResult();

        Assert.True(result.IsSuccessful, Failing(result));
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
            .Concat(AllTypesIn(HandlerModelsNs).Where(t =>
                t.Name.EndsWith("Dto", StringComparison.Ordinal) || t.Name.EndsWith("Entity", StringComparison.Ordinal)))
            .Select(t => t.FullName)
            .ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Message_handlers_live_in_the_handlers_namespace_and_end_in_Handler()
    {
        var violations = ConcreteClasses()
            .Where(t => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IHandleMessages<>)))
            .Where(t => t.Namespace != HandlersNs || !t.Name.EndsWith("Handler", StringComparison.Ordinal))
            .Select(t => t.FullName)
            .ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Worker_has_no_background_services_or_timers()
    {
        // A worker autoscales, so a timer loop would run once per replica. Scheduled work belongs in a scheduler app.
        var violations = ConcreteClasses()
            .Where(t => typeof(IHostedService).IsAssignableFrom(t) || typeof(BackgroundService).IsAssignableFrom(t))
            .Select(t => t.FullName)
            .ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Handlers_are_the_only_message_receivers()
    {
        var result = Types.InAssembly(Worker)
            .That().DoNotResideInNamespace(HandlersNs)
            .ShouldNot().HaveDependencyOn("Rebus.Handlers")
            .GetResult();

        Assert.True(result.IsSuccessful, Failing(result));
    }

    /// <summary>Top-level, non-compiler-generated types declared directly in a namespace.</summary>
    private static IEnumerable<Type> AllTypesIn(string ns) =>
        Worker.GetTypes().Where(t => t.Namespace == ns && !t.IsNested && !t.Name.StartsWith('<'));

    /// <summary>Concrete classes declared directly in a namespace.</summary>
    private static List<Type> TypesIn(string ns) =>
        AllTypesIn(ns).Where(t => t is { IsClass: true, IsAbstract: false }).ToList();

    /// <summary>Concrete Accessor classes: in <c>Accessors</c> or a sub-namespace such as <c>Accessors.Http</c>.</summary>
    private static List<Type> AccessorComponents() =>
        Worker.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsNested: false }
                && !t.Name.StartsWith('<')
                && IsAccessorComponentNamespace(t.Namespace))
            .ToList();

    private static bool IsAccessorComponentNamespace(string? ns) =>
        ns is not null
        && (ns == AccessorsNs || ns.StartsWith(AccessorsNs + ".", StringComparison.Ordinal))
        && !NonComponentSegments.Contains(ns.Split('.')[^1]);

    private static IEnumerable<Type> ConcreteClasses() =>
        Worker.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false } && !t.Name.StartsWith('<'));

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
