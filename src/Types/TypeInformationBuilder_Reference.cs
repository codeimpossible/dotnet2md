namespace DotnetToMd.Metadata
{
    internal static partial class TypeInformationBuilder
    {
        private static TypeInformation CreateGenericParameterTypeInformation(Parser parser, Type t)
        {
            var result = new TypeGenericInformation(t);
            parser.NameToTypes.Add(
                key: t.AsKey(),
                result);

            return result;
        }

        private static TypeInformation CreateTypeReferenceInformation(Parser parser, Type t)
        {
            var reference = TryGetReferenceLink(t);

            TypeReferenceInformation typeRefInfo = new(t, reference);

            parser.NameToTypes.Add(
                key: t.AsKey(),
                typeRefInfo);

            return typeRefInfo;
        }

        /// <summary>
        /// Fetches the reference link to a type <paramref name="t"/>.
        /// </summary>
        /// <remarks>
        /// <![CDATA[
        /// ??? info
        ///     Currently this method only supports Microsoft ("System."), MonoGame and Unity types.
        /// ]]>
        /// </remarks>
        /// <returns>
        /// If the type is a known third-party type, a string containing a valid URI to an external documentation
        /// source, <c>string.Empty</c> otherwise.
        /// </returns>
        /// <param name="t">The type info to use when looking up external documentation URIs.</param>
        private static string? TryGetReferenceLink(Type t)
        {
            if (t.Namespace is not string @namespace)
            {
                return null;
            }

            var linkPath = t.FullName ?? t.Name;
            if (t.IsGenericType)
            {
                linkPath = $"{t.Namespace}.{Utilities.EscapeNameForFilename(t)}";
            }

            if (@namespace.Equals("System", StringComparison.OrdinalIgnoreCase) ||
                @namespace.StartsWith("System.", StringComparison.OrdinalIgnoreCase))
            {
                return $"https://learn.microsoft.com/en-us/dotnet/api/{linkPath}?view=net-7.0";
            }

            if (@namespace.StartsWith("Microsoft.Xna.Framework", StringComparison.OrdinalIgnoreCase) || 
                @namespace.StartsWith("MonoGame.Framework", StringComparison.OrdinalIgnoreCase))
            {
                return $"https://docs.monogame.net/api/{linkPath}.html";
            }

            if (@namespace.StartsWith("UnityEngine", StringComparison.OrdinalIgnoreCase) ||
                @namespace.StartsWith("UnityEditor", StringComparison.OrdinalIgnoreCase))
            {
                var rootNamespace = @namespace.Split('.')[0];
                var pagePrefix = @namespace.Replace($"{rootNamespace}", string.Empty);
                if (pagePrefix.StartsWith('.'))
                {
                    pagePrefix = pagePrefix[1..];
                }

                if (pagePrefix.Length > 0)
                {
                    pagePrefix = $"{pagePrefix}.";
                }
                return $"https://docs.unity3d.com/ScriptReference/{pagePrefix}{t.Name}.html";
            }

            if (@namespace.Equals("Unity", StringComparison.OrdinalIgnoreCase) ||
                @namespace.StartsWith("Unity.", StringComparison.OrdinalIgnoreCase))
            {
                return $"https://docs.unity3d.com/ScriptReference/{@namespace}.{t.Name}.html";
            }

            return string.Empty;
        }
    }
}
