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
        private string? FormatSummary(string? text, string prefix)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            string[] paras = [GetSummary(text), GetRemarks(text)];

            text = string.Join(Environment.NewLine, paras).Trim();

            if (text is null || string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            Regex re = new("(<see cref=\")(.*)(\"[ ]?/>)");
            var matchCollection = re.Matches(text);

            foreach (Match match in matchCollection)
            {
                if (!match.Success)
                {
                    continue;
                }

                var replaceString = match.Value;
                var memberName = match.Groups[2].Value;

                text = text.Replace(replaceString, ToReferenceLink(memberName, prefix));
            }

            return text;
        }

        /// <summary>
        /// Return raw string value between <summary>. We do not use XNode here
        /// because it will escape the see cref parameters.
        /// </summary>
        private string? GetSummary(string text)
        {
            Regex re = new(@"^(<summary>)((.|\r|\n)*)(?=<\/summary>)");
            var m = re.Match(text);

            return m.Groups[2].Value.Trim();
        }

        private string? GetRemarks(string text)
        {
            Regex re = new(@"^(<remarks>)((.|\r|\n)*)(?=<\/remarks>)");
            var m = re.Match(text);

            return m.Groups[2].Value.Trim();
        }

        /// <param name="fullName">Full name of the target type.</param>
        /// <param name="prefix">Prefix of the current namespace (for appending to a relative path).</param>
        private string ToReferenceLink(string fullName, string prefix)
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
