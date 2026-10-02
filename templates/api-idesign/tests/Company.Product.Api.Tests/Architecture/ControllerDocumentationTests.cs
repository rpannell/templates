using System.Reflection;
using System.Xml.Linq;
using Company.Product.Api.Managers;
using Microsoft.AspNetCore.Mvc;

namespace Company.Product.Api.Tests.Architecture;

/// <summary>
/// XML docs are required everywhere (CS1591 is a build error), and controllers need more than that:
/// the docs feed the OpenAPI document shown in Scalar. If this fails, add the missing docs, not an exception.
/// </summary>
public class ControllerDocumentationTests
{
    private static readonly Assembly Api = typeof(OrderManager).Assembly;

    [Fact]
    public void Controllers_and_actions_have_complete_xml_docs()
    {
        var xmlPath = Path.ChangeExtension(Api.Location, ".xml");
        Assert.True(File.Exists(xmlPath), $"XML documentation file not found at {xmlPath}. Is GenerateDocumentationFile on?");

        var members = XDocument.Load(xmlPath)
            .Descendants("member")
            .ToDictionary(m => (string)m.Attribute("name")!, m => m);

        var controllers = Api.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .ToList();

        Assert.NotEmpty(controllers);

        var violations = new List<string>();

        foreach (var controller in controllers)
        {
            if (!members.TryGetValue("T:" + DocName(controller), out var typeDoc) || IsBlank(typeDoc.Element("summary")))
            {
                violations.Add($"{controller.Name}: missing <summary>");
            }

            var actions = controller
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName && m.GetCustomAttribute<NonActionAttribute>() is null);

            foreach (var action in actions)
            {
                var label = $"{controller.Name}.{action.Name}";

                if (!members.TryGetValue(MemberId(action), out var doc))
                {
                    violations.Add($"{label}: no XML docs");
                    continue;
                }

                if (IsBlank(doc.Element("summary")))
                {
                    violations.Add($"{label}: missing <summary>");
                }

                if (IsBlank(doc.Element("returns")))
                {
                    violations.Add($"{label}: missing <returns>");
                }

                foreach (var parameter in action.GetParameters())
                {
                    var paramDoc = doc.Elements("param").FirstOrDefault(p => (string?)p.Attribute("name") == parameter.Name);
                    if (IsBlank(paramDoc))
                    {
                        violations.Add($"{label}: missing <param name=\"{parameter.Name}\">");
                    }
                }

                var documentedCodes = doc.Elements("response")
                    .Where(r => !IsBlank(r))
                    .Select(r => (string?)r.Attribute("code"))
                    .ToHashSet();

                foreach (var produces in action.GetCustomAttributes<ProducesResponseTypeAttribute>())
                {
                    var code = produces.StatusCode.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    if (!documentedCodes.Contains(code))
                    {
                        violations.Add($"{label}: missing <response code=\"{code}\">");
                    }
                }
            }
        }

        Assert.Empty(violations);
    }

    private static bool IsBlank(XElement? element) => string.IsNullOrWhiteSpace(element?.Value);

    /// <summary>Builds the XML doc member id, for example <c>M:Ns.Type.Method(System.Guid,System.Threading.CancellationToken)</c>.</summary>
    private static string MemberId(MethodInfo method)
    {
        var id = $"M:{DocName(method.DeclaringType!)}.{method.Name}";
        var parameters = method.GetParameters();

        return parameters.Length == 0
            ? id
            : $"{id}({string.Join(",", parameters.Select(p => DocName(p.ParameterType)))})";
    }

    private static string DocName(Type type)
    {
        if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition().FullName!;
            definition = definition[..definition.IndexOf('`')];
            var arguments = string.Join(",", type.GetGenericArguments().Select(DocName));

            return $"{definition.Replace('+', '.')}{{{arguments}}}";
        }

        return (type.FullName ?? type.Name).Replace('+', '.');
    }
}
