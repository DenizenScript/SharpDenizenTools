using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FreneticUtilities.FreneticExtensions;
using SharpDenizenTools.MetaHandlers;

namespace SharpDenizenTools.MetaObjects
{
    /// <summary>
    /// A documented type of object.
    /// </summary>
    public class MetaObjectType : MetaObject
    {
        /// <summary><see cref="MetaObject.Type"/></summary>
        public override MetaType Type => MetaDocs.META_TYPE_OBJECT;

        /// <summary><see cref="MetaObject.Name"/></summary>
        public override string Name => TypeName;

        /// <summary><see cref="MetaObject.CleanName"/></summary>
        public override string CleanName => TypeName.ToLowerFast();

        /// <summary><see cref="MetaObject.AddTo(MetaDocs)"/></summary>
        public override void AddTo(MetaDocs docs)
        {
            docs.ObjectTypes.Add(CleanName, this);
        }

        /// <summary>
        /// The name of the object type.
        /// </summary>
        public string TypeName;

        /// <summary>
        /// The object identity prefix for this type.
        /// </summary>
        public string Prefix;

        /// <summary>
        /// The name of the base type.
        /// </summary>
        public string BaseTypeName;

        /// <summary>
        /// The base type.
        /// </summary>
        public MetaObjectType BaseType;

        /// <summary>
        /// A human-readable explanation of the identity format of the tag.
        /// </summary>
        public string Format;

        /// <summary>
        /// A human-readable description of the object type.
        /// </summary>
        public string Description;

        /// <summary>
        /// The names of other types or pseudo-types implemented by this type.
        /// </summary>
        public string[] ImplementsNames = Array.Empty<string>();

        /// <summary>
        /// Other types or pseudo-types implemented by this type.
        /// </summary>
        public MetaObjectType[] Implements = Array.Empty<MetaObjectType>();

        /// <summary><see cref="MetaObject.ApplyValue(string, string)"/></summary>
        public override bool ApplyValue(string key, string value)
        {
            switch (key)
            {
                case "name":
                    TypeName = value;
                    return true;
                case "prefix":
                    Prefix = value.ToLowerFast();
                    return true;
                case "base":
                    BaseTypeName = value;
                    return true;
                case "format":
                    Format = value;
                    return true;
                case "description":
                    Description = value;
                    return true;
                case "implements":
                    ImplementsNames = value.Replace(" ", "").SplitFast(',');
                    return true;
                default:
                    return base.ApplyValue(key, value);
            }
        }

        /// <summary><see cref="MetaObject.PostCheck(MetaDocs)"/></summary>
        public override void PostCheck(MetaDocs docs)
        {
            Require(docs, TypeName, Prefix, BaseTypeName, Format, Description);
            if (BaseTypeName.ToLowerFast() != "none")
            {
                BaseType = docs.ObjectTypes.GetValueOrDefault(BaseTypeName.ToLowerFast());
                if (BaseType == null)
                {
                    docs.LoadErrors.Add($"Object type name '{TypeName}' specifies basetype '{BaseType}' which is invalid.");
                }
            }
            Implements = new MetaObjectType[ImplementsNames.Length];
            for (int i = 0; i < Implements.Length; i++)
            {
                Implements[i] = docs.ObjectTypes.GetValueOrDefault(ImplementsNames[i].ToLowerFast());
                if (Implements[i] == null)
                {
                    docs.LoadErrors.Add($"Object type name '{TypeName}' specifies implement type '{Implements[i]}' which is invalid.");
                }
            }
            PostCheckSynonyms(docs, docs.ObjectTypes);
            if (!TypeName.EndsWith("Tag") && !TypeName.EndsWith("Object"))
            {
                docs.LoadErrors.Add($"Object type name '{TypeName}' has unrecognized format.");
            }
            if (Prefix != "none" && docs.ObjectTypes.Values.Any(t => t != this && t.Prefix == Prefix))
            {
                docs.LoadErrors.Add($"Object type name '{TypeName}' uses prefix '{Prefix}' which is also used by another object type.");
            }
            PostCheckLinkableText(docs, Description);
        }

        /// <summary><see cref="MetaObject.GetAllSearchableText"/></summary>
        public override string GetAllSearchableText()
        {
            string baseText = base.GetAllSearchableText();
            return $"{baseText}\n{TypeName}\n{Prefix}@\n{Format}\n{Description}\n"; // Intentionally exclude basetype / implements
        }
    }
}
