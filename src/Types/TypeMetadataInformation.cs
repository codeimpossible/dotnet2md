using System.Collections.Immutable;
using System.Text;

namespace DotnetToMd.Metadata
{
    /// <summary>
    /// This is a metadata type currently decoded and targeted to be an actual documentation type.
    /// See <see cref="Parser._target"/>.
    /// </summary>
    public class TypeMetadataInformation : TypeInformation
    {
        public readonly MemberKind Kind;

        public string? Signature;

        public TypeInformation? InheritedType;
        public ImmutableArray<TypeInformation>? InheritedInterfaces;

        public ImmutableDictionary<string, MethodInformation>? Constructors;
        public ImmutableDictionary<string, PropertyInformation>? Properties;
        public ImmutableDictionary<string, PropertyInformation>? Fields;
        public ImmutableDictionary<string, MethodInformation>? Methods;
        public ImmutableDictionary<string, PropertyInformation>? Events;

        public override string ReferenceLink
        {
            get
            {
                if (Namespace is not null)
                {
                    return Entrypoint.Options.InternalReferenceLinkFormat == ConfigurationOptions.ReferenceLinksHtmlFiles ?
                        $"{Namespace.Replace('.', '/')}/{EscapedFilename}.html" :
                        $"{Namespace.Replace('.', '/')}/{EscapedFilename}/";
                }
                return string.Empty;
            }
        }

        internal TypeMetadataInformation(
            Type metadata,
            MemberKind kind) :
            base(metadata, metadata.Namespace)
        {
            Kind = kind;
        }

        public ImmutableArray<TypeInformation> GetInheritedMembers()
        {
            var builder = ImmutableArray.CreateBuilder<TypeInformation>();

            if (InheritedType is not null)
            {
                builder.Add(InheritedType);
            }

            if (InheritedInterfaces?.Length > 0)
            {
                builder.AddRange(InheritedInterfaces);
            }

            return builder.ToImmutableArray();
        }
    }
}
