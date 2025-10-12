using DotnetToMd.Metadata;
using System.Text;

namespace DotnetToMd
{
    internal partial class Parser
    {
        private readonly Dictionary<string /* path */, string /* name */> _markdowns = new(StringComparer.OrdinalIgnoreCase);

        private void GenerateMarkdown()
        {
            var types =
                NameToTypes.Values.Where(t => t is TypeMetadataInformation).Select(t => (TypeMetadataInformation)t);

            foreach (var t in types)
            {
                if (t.Namespace is null)
                {
                    continue;
                }

                var result = GenerateMarkdownForType(t);

                var namespacePath = Path.Join(_outputPath, CreatePathForNamespace(t.Namespace));
                if (!Directory.Exists(namespacePath))
                {
                    _ = Directory.CreateDirectory(namespacePath);
                }

                var fullPath = Path.Join(namespacePath, $"{t.EscapedFilename}.md");
                File.WriteAllText(fullPath, result);

                _markdowns.Add(fullPath, t.EscapedNameForHeader);
            }

            var targetNamespaces = Targets.Select(a => Path.GetFileNameWithoutExtension(a.ManifestModule.Name)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> summaryResultPerTarget = new();

            foreach (var directory in Directory.GetDirectories(_outputPath))
            {
                var targetName = new DirectoryInfo(directory).Name;
                if (targetNamespaces.Contains(targetName))
                {
                    StringBuilder builder = new();
                    WriteSummary(/* ref */ ref builder, directory, 1);

                    summaryResultPerTarget.Add(targetName, builder.ToString());
                }
            }

            var presummaryFilePath = Path.Join(_outputPath, Constants.SummaryInputFileName);
            var summaryFilePath = Path.Join(_outputPath, Constants.SummaryOutputFileName);

            if (File.Exists(presummaryFilePath))
            {
                var summaryFileContent = File.ReadAllText(presummaryFilePath);
                foreach (var target in summaryResultPerTarget.Keys)
                {
                    summaryFileContent = summaryFileContent.Replace($"<{target}-Content>", $"{summaryResultPerTarget[target]}", StringComparison.InvariantCultureIgnoreCase);
                }

                File.WriteAllText(summaryFilePath, summaryFileContent);
            }
        }

        private void WriteSummary(ref StringBuilder text, string directory, int level)
        {
            StringBuilder indent = new();

            // Skip first directory!
            if (level != 1)
            {
                for (var i = 2; i < level; ++i)
                {
                    indent.Append("  ");
                }

                text.Append(indent);
                text.AppendLine($"- [{new DirectoryInfo(directory).Name}]()");
                indent.Append("  ");
            }

            foreach (var subdirectory in Directory.GetDirectories(directory))
            {
                WriteSummary(ref text, subdirectory, level + 1);
            }

            foreach (var file in Directory.GetFiles(directory))
            {
                var relativePath = Path.GetRelativePath(_outputPath, file);
                if (_markdowns.TryGetValue(file, out var label))
                {
                    text.AppendLine($"{indent}- [{label}]({relativePath.Replace('\\', '/')})");
                }
            }
        }

        private static string CreatePathForNamespace(string @namespace)
        {
            return @namespace.Replace('.', Path.DirectorySeparatorChar);
        }

        private string GenerateMarkdownForType(TypeMetadataInformation t)
        {
            StringBuilder builder = new();

            builder.Append($"# {t.EscapedNameForHeader}\n\n");

            builder.Append($"<!-- tc:namespace {t.Namespace} -->\n\n");
            builder.Append($"<!-- tc:assembly {t.Assembly} -->\n\n");

            if (t.Summary is not null)
            {
                builder.Append($"{t.Summary}\n\n");
            }

            builder.Append($"\n```csharp\n{t.Signature}\n```\n\n");

            var prefix = RetrieveRelativePathFromNamespace(t.Namespace);

            var inheritedTypes = t.GetInheritedMembers();
            if (inheritedTypes.Length > 0)
            {
                builder.Append("**Implements:** _");

                for (var i = 0; i < inheritedTypes.Length; ++i)
                {
                    var tt = inheritedTypes[i];
                    builder.Append($"[{tt.EscapedNameForHeader}]({FormatReferenceLink(prefix, tt.ReferenceLink)})");

                    if (i != inheritedTypes.Length - 1)
                    {
                        builder.Append(", ");
                    }
                }

                builder.Append("_\n\n");
            }

            if (t.Constructors?.Count > 0)
            {
                builder.Append("## Constructors\n\n");

                var sortedConstructors = t.Constructors.Values.OrderBy(s => s.FullSignature).ToList();
                foreach (var c in sortedConstructors)
                {
                    builder.Append(MethodToMarkdown(c, prefix));
                }
            }

            if (t.Fields?.Count > 0)
            {
                builder.Append("## Fields\n\n");

                var sortedFields = t.Fields.OrderBy(kv => kv.Key).Select(kv => kv.Value).ToList();
                foreach (var f in sortedFields)
                {
                    builder.Append(PropertyToMarkdown(f, prefix));
                }
            }

            if (t.Properties?.Count > 0)
            {
                builder.Append("## Properties\n\n");

                var sortedProperties = t.Properties.OrderBy(kv => kv.Key).Select(kv => kv.Value).ToList();
                foreach (var p in sortedProperties)
                {
                    builder.Append(PropertyToMarkdown(p, prefix));
                }
            }

            if (t.Events?.Count > 0)
            {
                builder.Append("## Events\n\n");

                var sortedEvents = t.Events.OrderBy(kv => kv.Key).Select(kv => kv.Value).ToList();
                foreach (var p in sortedEvents)
                {
                    builder.Append(PropertyToMarkdown(p, prefix));
                }
            }

            if (t.Methods?.Count > 0)
            {
                builder.Append("## Methods\n\n");

                // TODO: This will not sort methods with types from different namespaces.
                var sortedMethods = t.Methods.Values.OrderBy(s => s.FullSignature).ToList();
                foreach (var m in sortedMethods)
                {
                    builder.Append(MethodToMarkdown(m, prefix));
                }
            }

            AppendAdditionalLinks(t.AdditionalLinks, builder, "## More information");

            return builder.ToString();
        }

        private string FormatReturnTypeMarker(TypeInformation typeInfo, string prefix)
        {
            var returnTypeString = $"{typeInfo.EscapedNameForHeader}";
            if (!string.IsNullOrEmpty(typeInfo.ReferenceLink))
            {
                returnTypeString += $" {FormatReferenceLink(prefix, typeInfo.ReferenceLink)}";
            }
            return $"<!-- tc:return_type {returnTypeString} -->\n";
        }

        private string FormatVersionMarker(TypeInformation typeInfo, string prefix)
        {
            var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(typeInfo.Type.Assembly.Location);
            return $"<!-- tc:version {versionInfo.FileMajorPart}.{versionInfo.FileMinorPart}.{versionInfo.FileMinorPart} -->\n";
        }

        private StringBuilder PropertyToMarkdown(PropertyInformation p, string prefix)
        {
            StringBuilder builder = new();

            builder.Append($"<a name=\"{p.Name}\"></a>\n\n");
            builder.Append($"### `{p.Name}`\n");

            builder.Append($"<!-- tc:scope {p.AccessModifier.ToString().ToLower()} -->\n");

            if (p.Return is ArgumentInformation ret)
            {
                builder.Append(FormatReturnTypeMarker(ret.Type, prefix));
            }

            builder.Append(FormatVersionMarker(p.DeclaringType, prefix));

            if (p.Summary is not null)
            {
                builder.Append($"{p.Summary}\n\n");
            }

            builder.Append(FormatCodeSignature(p));

            AppendAdditionalLinks(p.AdditionalLinks, builder, "#### More information");

            return builder;
        }

        private string FormatCodeSignature(InformationBase info)
        {
            var builder = new StringBuilder();
            builder.Append("\n```csharp\n");

            if (info is MethodInformation methodInfo)
            {
                builder.AppendLine(CodeFormatter.FormatMethodDefinition(methodInfo.Signature));
            }

            if (info is PropertyInformation propInfo)
            {
                builder.AppendLine(propInfo.Signature);
            }

            builder.Append("\n```\n\n");
            return builder.ToString();
        }

        private StringBuilder MethodToMarkdown(MethodInformation m, string prefix)
        {
            StringBuilder builder = new();

            builder.Append($"<a name=\"{m.Name}\"></a>\n\n");
            builder.Append($"### `{m.GetPrettyKey()}`\n");

            builder.Append($"<!-- tc:scope {m.AccessModifier.ToString().ToLower()} -->\n");
            if (m.Return is ArgumentInformation ret)
            {
                builder.Append(FormatReturnTypeMarker(ret.Type, prefix));
            }
            var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(m.DeclaringType.Type.Assembly.Location);
            builder.Append($"<!-- tc:version {versionInfo.FileMajorPart}.{versionInfo.FileMinorPart}.{versionInfo.FileMinorPart} -->\n");


            if (m.Summary is not null)
            {
                builder.Append($"{m.Summary}\n\n");
            }

            builder.Append(FormatCodeSignature(m));

            if (m.Parameters?.Length > 0)
            {
                builder.Append($"**Parameters** {Constants.LineBreak}");

                foreach (var argument in m.Parameters)
                {
                    builder.Append(ArgumentToMarkdown(argument, prefix));
                }

                builder.Append("\n");
            }

            if (m.Exceptions?.Length > 0)
            {
                builder.Append($"**Exceptions** {Constants.LineBreak}");

                foreach (var (tException, summary) in m.Exceptions)
                {
                    builder.Append($"[{tException.EscapedNameForHeader}]({FormatReferenceLink(prefix, tException.ReferenceLink)}) {Constants.LineBreak}");
                    builder.Append($"{summary} {Constants.LineBreak}");
                }
            }

            AppendAdditionalLinks(m.AdditionalLinks, builder, "#### More information");

            return builder;
        }

        private void AppendAdditionalLinks(List<(string Uri, string? Text)> links, StringBuilder builder, string header)
        {
            if (links.Count > 0)
            {
                builder.Append($"{header}\n\n");
                foreach (var link in links)
                {
                    var linkUri = link.Uri;
                    var linkText = link.Text;
                    if (IsReferenceLink(link.Uri))
                    {
                        var marker = link.Uri.Substring(0, 2);
                        var fullPath = link.Uri.Substring(2);
                        var linkSuffix = string.Empty;
                        var typePath = fullPath;
                        switch (marker)
                        {
                            case "T:":
                                if (string.IsNullOrEmpty(linkText)) linkText = typePath;
                                break;
                            case "F:":
                            case "M:":
                            case "P:":
                                var memberName = fullPath.Split('.').Last();
                                typePath = fullPath.Replace($".{memberName}", string.Empty);
                                linkSuffix = $"#{memberName}";
                                if (string.IsNullOrEmpty(linkText)) linkText = $"{typePath}.{memberName}";
                                break;
                            default: continue;
                        }

                        var typeInfo = FetchOrCreate(typePath);
                        if (typeInfo is null)
                        {
                            continue;
                        }

                        var prefix = RetrieveRelativePathFromNamespace(typeInfo.Namespace);
                        linkUri = $"{FormatReferenceLink(prefix, typeInfo.ReferenceLink)}{linkSuffix}";
                    }
                    var text = linkText ?? linkUri;
                    Utilities.Log($"Building additional link. text={text}, uri={linkUri}");
                    builder.Append($"* [{text}]({linkUri})\n");
                }
            }
        }

        private StringBuilder ArgumentToMarkdown(ArgumentInformation arg, string prefix)
        {
            StringBuilder builder = new();

            if (!string.IsNullOrEmpty(arg.Name))
            {
                builder.Append($"`{arg.Name}` ");
            }

            builder.Append($"[{arg.Type.EscapedNameForHeader}]({FormatReferenceLink(prefix, arg.Type.ReferenceLink)}) {Constants.LineBreak}");

            if (arg.Summary is not null)
            {
                builder.Append($"{arg.Summary} {Constants.LineBreak}");
            }

            return builder;
        }

        private string RetrieveRelativePathFromNamespace(string? @namespace)
        {
            if (@namespace is null)
            {
                return string.Empty;
            }

            // var level = @namespace.Count(c => c == '.');
            //
            // StringBuilder builder = new();
            // while (level-- >= 0)
            // {
            //     builder.Append("/noir/reference/");
            // }

            return "/noir/reference/";
        }

        /// <summary>
        /// Format the current reference link to an actual reasonable relative path.
        /// </summary>
        private string FormatReferenceLink(string prefix, string link)
        {
            if (link.Contains("http"))
            {
                return link;
            }

            return prefix.Length == 0 ? link : $"{prefix}{link}";
        }
    }
}
