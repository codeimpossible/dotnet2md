using DotnetToMd.Metadata;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace DotnetToMd
{
    internal partial class Parser
    {
        /// <summary>
        /// This will format a summary with cref parameters with their markdown syntax.
        /// </summary>
        private string? FormatSummary(string prefix, string? text = null, string? remarks = null, string? example = null)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            string[] paras = [GetSummary(text), GetRemarks(remarks), FormatExample(example)];

            text = string.Join(Environment.NewLine, paras).Trim();

            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            text = FormatLinks(text, prefix);

            return text;
        }

        /// <summary>
        /// Return raw string value between <summary>. We do not use XNode here
        /// because it will escape the see cref parameters.
        /// </summary>
        private string GetSummary(string? text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            Regex re = new(@"^(<summary>)((.|\r|\n)*)(?=<\/summary>)");
            var m = re.Match(text);

            return FormatParagraphs(m.Groups[2].Value.Trim());
        }

        private string GetRemarks(string? text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            Regex re = new(@"^(<remarks>)((.|\r|\n)*)(?=<\/remarks>)");
            var m = re.Match(text);

            return FormatParagraphs(m.Groups[2].Value.Trim());
        }

        private string FormatParagraphs(string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            return text.Replace("</para>", string.Empty).Replace("<para>", "<br />");
        }

        /// <param name="fullName">Full name of the target type.</param>
        /// <param name="prefix">Prefix of the current namespace (for appending to a relative path).</param>
        private string ToReferenceLink(string fullName, string prefix, string? overrideName = "")
        {
            var name = fullName.Substring(fullName.LastIndexOf(':') + 1);
            var firstCharacter = fullName[0];

            string? declaringTypeName;
            TypeInformation? type;

            var referenceLink = string.Empty;
            switch (firstCharacter)
            {
                case 'T':
                    type = FetchOrCreate(name);
                    if (type is null)
                    {
                        break;
                    }

                    name = type.Name;
                    referenceLink = FormatReferenceLink(prefix, type.ReferenceLink);
                    break;

                case 'P':
                case 'F':
                case 'E':
                    declaringTypeName = Utilities.GetDeclaringTypeName(name);
                    type = FetchOrCreate(declaringTypeName);
                    if (firstCharacter == 'F')
                    {
                        Utilities.Log($"Building link. typeName={declaringTypeName}, type={type?.Name ?? "null"}");
                    }
                    if (type is null)
                    {
                        break;
                    }

                    var propertyName = GetMemberName(declaringTypeName, name);

                    name = $"{type.Name}.{propertyName}";
                    referenceLink = GetPropertyReferenceLink(type, propertyName, prefix);
                    break;

                case 'M':
                    declaringTypeName = Utilities.GetDeclaringTypeOfMethod(name);

                    type = FetchOrCreate(declaringTypeName);
                    if (type is null)
                    {
                        break;
                    }

                    var methodName = GetMemberName(declaringTypeName, name);

                    name = $"{type.Name}.{methodName}";
                    referenceLink = GetMethodReferenceLink(type, methodName, prefix);
                    break;

                case '!':
                    // So far, I have only seen that happen for generic parameters. Not really supported as of now.
                    break;

                case 'A':
                case 'N':
                    // TODO: Assembly reference?
                    break;

                default:
                    Debug.Fail("Unsupported scenario?");
                    break;
            }

            if (!string.IsNullOrEmpty(overrideName))
            {
                name = overrideName;
            }
            return $"[{name}]({referenceLink})";
        }

        private string GetPropertyReferenceLink(TypeInformation type, string member, string prefix)
        {
            var referenceLink = type.ReferenceLink;

            // TODO: Support external websites!!
            if (type.ReferenceLink.Contains("https"))
            {
                return referenceLink;
            }

            // TODO: Figure out conflicting links.
            return $"{prefix}{referenceLink}#{member.ToLowerInvariant()}";
        }

        private string GetMethodReferenceLink(TypeInformation type, string method, string prefix)
        {
            var referenceLink = type.ReferenceLink;

            // TODO: Support external types.
            if (type is not TypeMetadataInformation metadataType)
            {
                return referenceLink;
            }

            if (!(metadataType.Methods?.TryGetValue(method, out var methodInfo) ?? false))
            {
                return $"{prefix}{referenceLink}";
            }

            method = methodInfo.GetPrettyKey();

            var firstSpaceIndex = method.IndexOf(' ');
            if (firstSpaceIndex != -1)
            {
                method = method.Substring(0, firstSpaceIndex);
            }

            method = method.Trim('(', ')');

            // For now, the header will be method name and the first parameter.
            // TODO: Figure out conflicting links.
            return $"{prefix}{referenceLink}#{method.ToLowerInvariant()}";
        }
    }
}
