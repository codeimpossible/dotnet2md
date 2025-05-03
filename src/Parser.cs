using DotnetToMd.Metadata;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Xml.Linq;

namespace DotnetToMd
{
    internal partial class Parser
    {
        public ImmutableHashSet<Assembly> Targets;

        public Dictionary<string, TypeInformation> NameToTypes { get; } = new();

        private readonly ImmutableArray<Assembly> _dependencies;

        private readonly ImmutableArray<string> _xmlFiles;
        private readonly string _outputPath;

        internal Parser(List<Assembly> target, List<Assembly> dependencies, string[] xmlFiles, string outputPath)
        {
            Targets = target.ToImmutableHashSet();

            _dependencies = dependencies.ToImmutableArray();

            _xmlFiles = xmlFiles.ToImmutableArray();
            _outputPath = outputPath;
        }

        internal void Generate()
        {
            ReadMetadata();
            ReadXml();

            GenerateMarkdown();
        }

        private void ReadMetadata()
        {
            foreach (var asm in Targets)
            {
                IEnumerable<Type> publicTypes = asm.GetTypes().Where(t => t.IsPublic);
                foreach (var t in publicTypes)
                {
                    if (TypeInformationBuilder.FetchOrCreate(this, t) is null)
                    {
                        Debug.Fail($"Unable to decode metadata type {t.Name}?");
                    }
                }
            }
        }

        private void ReadXml()
        {
            foreach (var file in _xmlFiles)
            {
                var xml = XDocument.Load(file);

                if (xml.Root?.Descendants("members")?.Elements() is not IEnumerable<XElement> members)
                {
                    // No members declared?
                    return;
                }

                foreach (var element in members)
                {
                    ProcessMember(element);
                }
            }
        }

        private void ProcessMember(XElement element)
        {
            var memberName = element.Attribute("name")?.Value;
            var summary = element.Element("summary")?.ToString();
            var remarks = element.Element("remarks")?.ToString();

            if (element.Name != "member")
            {
                return;
            }

            if (string.IsNullOrEmpty(memberName))
            {
                Debug.Fail($"Skipping empty member? '{element}'");
                return;
            }

            var name = memberName.Substring(memberName.LastIndexOf(':') + 1);

            var firstCharacter = memberName[0];
            ProcessAny(element, name);
            switch (firstCharacter)
            {
                case 'T':
                    ProcessTypeMember(element, name, summary);
                    return;

                case 'P':
                case 'F':
                    ProcessFieldOrPropertyMember(element, name, summary);
                    return;

                case 'M':
                    ProcessMethodMember(element, name, summary);
                    return;

                case 'E':
                    ProcessEventMember(element, name, summary);
                    return;

                case 'A':
                case '?': return;

                default:
                    Debug.Fail($"Unsupported scenario? '{element}'");
                    return;
            }
        }

        private void ProcessAny(XElement el, string name)
        {
            if (!NameToTypes.TryGetValue(name, out var typeInfo))
            {
                return;
            }

            if (typeInfo is TypeMetadataInformation metadataInfo)
            {
                metadataInfo.AdditionalLinks.AddRange(FormatAdditionalLinks(el));
            }
        }

        private void ProcessTypeMember(XElement _, string name, string? summary)
        {
            if (!NameToTypes.TryGetValue(name, out var typeInfo))
            {
                return;
            }

            if (typeInfo is TypeMetadataInformation metadataInfo)
            {
                metadataInfo.Summary = FormatSummary(summary, RetrieveRelativePathFromNamespace(metadataInfo.Namespace));
            }
        }

        private void ProcessFieldOrPropertyMember(XElement _, string name, string? summary)
        {
            var declaringType = Utilities.GetDeclaringTypeName(name);
            if (!NameToTypes.TryGetValue(declaringType, out var typeInfo) ||
                typeInfo is not TypeMetadataInformation metadataInfo)
            {
                // Internal types won't be in the list.
                return;
            }

            var propertyName = GetMemberName(declaringType, name);
            if (metadataInfo.Properties?.TryGetValue(propertyName, out var propertyInfo) ?? false)
            {
                propertyInfo.Summary = FormatSummary(summary, RetrieveRelativePathFromNamespace(propertyInfo.DeclaringType.Namespace));
            }
        }

        private void ProcessMethodMember(XElement element, string name, string? summary)
        {
            var declaringType = Utilities.GetDeclaringTypeOfMethod(name);
            if (!NameToTypes.TryGetValue(declaringType, out var typeInfo) ||
                typeInfo is not TypeMetadataInformation metadataInfo)
            {
                // Internal types won't be in the list.
                return;
            }

            var methodName = GetMemberName(declaringType, name);
            if ((metadataInfo.Methods?.TryGetValue(methodName, out var methodInfo) ?? false) ||
                (metadataInfo.Constructors?.TryGetValue(methodName, out methodInfo) ?? false))
            {
                var relativePathFromNamespace = RetrieveRelativePathFromNamespace(methodInfo.DeclaringType.Namespace);

                methodInfo.Summary = FormatSummary(summary, relativePathFromNamespace);

                List<XElement>? parameters = element.Elements("param")?.ToList();
                if (methodInfo.Parameters is not null && parameters?.Count > 0)
                {
                    foreach (var parameter in parameters)
                    {
                        var parameterName = parameter.Attribute("name")?.Value.Trim();
                        var parameterSummary = FormatSummary(parameter.Value.Trim(), relativePathFromNamespace) ?? string.Empty;

                        if (parameterName is not null && 
                            methodInfo.Parameters.Value.FirstOrDefault(p => p.Name == parameterName) is ArgumentInformation argument)
                        {
                            argument.Summary = FormatSummary(parameterSummary, relativePathFromNamespace);
                        }
                    }
                }

                var @return = element.Element("returns");
                if (@return is not null && methodInfo.Return is ArgumentInformation returnInfo)
                {
                    var returnSummary = @return.Value.Trim();
                    returnInfo.Summary = FormatSummary(returnSummary, relativePathFromNamespace);
                }

                List<XElement>? exceptions = element.Elements("exception")?.ToList();
                if (exceptions?.Count > 0)
                {
                    var builder = ImmutableArray.CreateBuilder<(TypeInformation Type, string Summary)>();
                    foreach (var e in exceptions)
                    {
                        var parameterRefName = e.Attribute("cref")?.Value.Trim();
                        parameterRefName = parameterRefName?.Substring(parameterRefName.LastIndexOf(':') + 1);

                        var exceptionSummary = FormatSummary(e.Value.Trim(), relativePathFromNamespace) ?? string.Empty;

                        if (parameterRefName is not null && 
                            FetchOrCreate(parameterRefName) is TypeInformation typeInformation)
                        {
                            builder.Add((typeInformation, exceptionSummary));
                        }
                    }

                    methodInfo.Exceptions = builder.ToImmutableArray();
                }
            }
        }

        private void ProcessEventMember(XElement _, string name, string? summary)
        {
            var declaringType = Utilities.GetDeclaringTypeName(name);
            if (!NameToTypes.TryGetValue(declaringType, out var typeInfo) ||
                typeInfo is not TypeMetadataInformation metadataInfo)
            {
                // Internal types won't be in the list.
                return;
            }

            var eventName = GetMemberName(declaringType, name);
            if (metadataInfo.Events?.TryGetValue(eventName, out var eventInfo) ?? false)
            {
                eventInfo.Summary = FormatSummary(summary, RetrieveRelativePathFromNamespace(eventInfo.DeclaringType.Namespace));
            }
        }

        private string GetMemberName(string declaringTypeName, string name)
        {
            return name.Substring(declaringTypeName.Length + 1);
        }

        public TypeInformation? FetchOrCreate(string typeName)
        {
            if (NameToTypes.TryGetValue(typeName, out var typeInfo))
            {
                return typeInfo;
            }

            var t = FindType(typeName);
            if (t is not null)
            {
                return TypeInformationBuilder.CreateTypeInformationFromType(this, t);
            }

            return null;
        }

        private Type? FindType(string name)
        {
            var t = typeof(string).Assembly.GetType(name);
            if (t is not null)
            {
                return t;
            }

            foreach (var a in _dependencies)
            {
                t = a.GetType(name);
                if (t is not null)
                {
                    return t;
                }
            }

            return null;
        }
    }
}
