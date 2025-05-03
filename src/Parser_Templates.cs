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
                builder.Append("## 🛠 Constructors\n\n");

                var sortedConstructors = t.Constructors.Values.OrderBy(s => s.FullSignature).ToList();
                foreach (var c in sortedConstructors)
                {
                    builder.Append(MethodToMarkdown(c, prefix));
                }
            }

            if (t.Properties?.Count > 0)
            {
                builder.Append("## 📦 Properties\n\n");

                var sortedProperties = t.Properties.OrderBy(kv => kv.Key).Select(kv => kv.Value).ToList();
                foreach (var p in sortedProperties)
                {
                    builder.Append(PropertyToMarkdown(p, prefix));
                }
            }

            if (t.Events?.Count > 0)
            {
                builder.Append("## ⚡ Events\n\n");

                var sortedEvents = t.Events.OrderBy(kv => kv.Key).Select(kv => kv.Value).ToList();
                foreach (var p in sortedEvents)
                {
                    builder.Append(PropertyToMarkdown(p, prefix));
                }
            }

            if (t.Methods?.Count > 0)
            {
                builder.Append("## ⛹️‍♀️ Methods\n\n");

                // TODO: This will not sort methods with types from different namespaces.
                var sortedMethods = t.Methods.Values.OrderBy(s => s.FullSignature).ToList();
                foreach (var m in sortedMethods)
                {
                    builder.Append(MethodToMarkdown(m, prefix));
                }
            }

            if (t.AdditionalLinks.Count > 0)
            {
                builder.Append("## More information\n\n");
                foreach (var link in t.AdditionalLinks)
                {
                    var text = link.Text ?? link.Uri;
                    builder.Append($"* [{text}]({link.Uri})\n");
                }
            }


            return builder.ToString();
        }

        private string FormatReturnTypeMarker(TypeInformation typeInfo, string prefix)
        {
            var returnTypeString = $"{typeInfo.EscapedNameForHeader}";
            if (!string.IsNullOrEmpty(typeInfo.ReferenceLink))
            {
                returnTypeString = $"[{typeInfo.EscapedNameForHeader}]({FormatReferenceLink(prefix, typeInfo.ReferenceLink)})";
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

            builder.Append($"\n```csharp\n{p.Signature}\n```\n\n");

            if (p.AdditionalLinks.Count > 0)
            {
                builder.Append("#### More information\n\n");
                foreach (var link in p.AdditionalLinks)
                {
                    var text = link.Text ?? link.Uri;
                    builder.Append($"* [{text}]({link.Uri})\n");
                }
            }

            return builder;
        }

        private StringBuilder MethodToMarkdown(MethodInformation m, string prefix)
        {
            StringBuilder builder = new();

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

            builder.Append($"\n```csharp\n{m.Signature}\n```\n\n");

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

            if (m.AdditionalLinks.Count > 0)
            {
                builder.Append("#### More information\n\n");
                foreach (var link in m.AdditionalLinks)
                {
                    var text = link.Text ?? link.Uri;
                    builder.Append($"* [{text}]({link.Uri})\n");
                }
            }

            return builder;
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

            var level = @namespace.Count(c => c == '.');

            StringBuilder builder = new();
            while (level-- >= 0)
            {
                builder.Append("../");
            }

            return builder.ToString();
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
