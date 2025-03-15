using FreneticUtilities.FreneticExtensions;
using SharpDenizenTools.MetaObjects;
using SharpDenizenTools.ScriptAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpDenizenTools.MetaHandlers
{

    /// <summary>Helper class to trace and analyze a written tag.</summary>
    public class TagTracer
    {
        /// <summary>The relevant docs object.</summary>
        public MetaDocs Docs;

        /// <summary>The relevant tag.</summary>
        public SingleTag Tag;

        /// <summary>An action to display error messages.</summary>
        public Action<string> Error = (_) => { /* Default Ignore */ };

        /// <summary>An action to display deprecation error messages.</summary>
        public Action<string, SingleTag.Part> DeprecationError = (_, _) => { /* Default Ignore */ };

        /// <summary>Special tags that used to exist and get special handling.</summary>
        [Obsolete("Deprecated tag support")]
        public static HashSet<string> LegacySpecialTags = ["permission", "text", "name", "amount"];

        /// <summary>Traces through a written tag, trying to find the documented tag parts inside it.</summary>
        public void Trace()
        {
            if (Tag.Parts.IsEmpty())
            {
                return;
            }
            string root = Tag.Parts[0].Text;
            if (root == "")
            {
                root = "definition";
            }
            if (root == "context" || root == "entry")
            {
                TraceTagParts([.. Docs.ObjectTypes.Values], 2);
            }
            else if (LegacySpecialTags.Contains(root))
            {
                Error($"Tag base '{root}' is deprecated: write it as a definition, like '<[{root}]>'.");
                return;
            }
            else if (Tag.Parts.Count >= 4 && Docs.Tags.TryGetValue(root + "." + Tag.Parts[1].Text + "." + Tag.Parts[2].Text + "." + Tag.Parts[3].Text, out MetaTag superComplexBaseTag))
            {
                for (int i = 0; i < 4; i++)
                {
                    Tag.Parts[i].PossibleTags.Add(superComplexBaseTag);
                }
                TraceTagParts(ParsePossibleTypes(superComplexBaseTag.Returns, superComplexBaseTag.ReturnType), 4);
            }
            else if (Tag.Parts.Count >= 3 && Docs.Tags.TryGetValue(root + "." + Tag.Parts[1].Text + "." + Tag.Parts[2].Text, out MetaTag veryComplexBaseTag))
            {
                for (int i = 0; i < 3; i++)
                {
                    Tag.Parts[i].PossibleTags.Add(veryComplexBaseTag);
                }
                TraceTagParts(ParsePossibleTypes(veryComplexBaseTag.Returns, veryComplexBaseTag.ReturnType), 3);
            }
            else if (Tag.Parts.Count >= 2 && Docs.Tags.TryGetValue(root + "." + Tag.Parts[1].Text, out MetaTag complexBaseTag))
            {
                for (int i = 0; i < 2; i++)
                {
                    Tag.Parts[i].PossibleTags.Add(complexBaseTag);
                }
                TraceTagParts(ParsePossibleTypes(complexBaseTag.Returns, complexBaseTag.ReturnType), 2);
            }
            else if (Docs.Tags.TryGetValue(root, out MetaTag realBaseTag))
            {
                Tag.Parts[0].PossibleTags.Add(realBaseTag);
                if (Tag.Parts[0].Parameter == null)
                {
                    if (realBaseTag.RequiresParam)
                    {
                        Error($"Tag base '{root}' requires an input [tag parameter] value.");
                        return;
                    }
                }
                else
                {
                    if (!realBaseTag.AllowsParam)
                    {
                        Error($"Tag base '{root}' cannot have a [tag parameter].");
                        return;
                    }
                }
                TraceTagParts(ParsePossibleTypes(realBaseTag.Returns, realBaseTag.ReturnType), 1);
            }
            else if (Docs.ObjectTypes.TryGetValue(root, out MetaObjectType documentedObjectBase))
            {
                if (documentedObjectBase.Prefix.ToLowerFast() == "none")
                {
                    Error($"Tag base '{Tag.Parts[0].Text}' seems to refer to a pseudo-object-type, but not one that can be used as a free-standing tag base.");
                }
                TraceTagParts([documentedObjectBase], 1);
            }
            else
            {
                Error($"Tag base '{Tag.Parts[0].Text}' does not exist.");
            }
            Tag.Parts[0].PossibleSubTypes = GetFullComplexSetFrom([.. Tag.Parts[0].PossibleTags.Select(t => t.ReturnType).Where(t => t is not null)]);
            foreach (SingleTag.Part part in Tag.Parts)
            {
                List<MetaTag> deprecated = [.. part.PossibleTags.Where(p => p.Deprecated is not null)];
                if (deprecated.Any() && deprecated.Count == part.PossibleTags.Count)
                {
                    MetaTag tag = deprecated[0];
                    DeprecationError($"Deprecated tag `{tag.CleanedName}`: {tag.Deprecated}", part);
                }
            }
        }

        /// <summary>Converts tag return data to something usable for the trace.</summary>
        public HashSet<MetaObjectType> ParsePossibleTypes(string returnType, MetaObjectType known)
        {
            returnType = returnType.ToLowerFast();
            if (returnType == "objecttag")
            {
                return [.. Docs.ObjectTypes.Values];
            }
            if (known == null)
            {
                known = Docs.ObjectTypes.GetValueOrDefault(returnType);
            }
            if (known != null)
            {
                HashSet<MetaObjectType> result = [known];
                result.UnionWith(known.ExtendedBy);
                return result;
            }
            Error($"(Internal) Unknown object return type '{returnType}'");
            return null;
        }

        /// <summary>Traces the parts of the tag, after the base has been traced.</summary>
        public void TraceTagParts(HashSet<MetaObjectType> possibleRoots, int index)
        {
            if (Tag.Parts.Count <= index || possibleRoots == null)
            {
                return;
            }
            while (index < Tag.Parts.Count)
            {
                List<(MetaTag, int)> result = TraceTagPartSingle(possibleRoots, index);
                if (result == null)
                {
                    return;
                }
                string part = Tag.Parts[index].Text.ToLowerFast();
                if (result.IsEmpty())
                {
                    if (possibleRoots.Count == 1)
                    {
                        Error($"Tag part '{part}' does not exist for object type {possibleRoots.First().Name}");
                    }
                    else if (possibleRoots.Count < 5)
                    {
                        Error($"Tag part '{part}' does not exist for object types {string.Join(", ", possibleRoots.Select(r => r.Name))}");
                    }
                    else
                    {
                        Error($"Tag part '{part}' does not exist for any applicable object types");
                    }
                    return;
                }
                if (Tag.Parts[index].Parameter == null)
                {
                    result = [.. result.Where(t => !t.Item1.RequiresParam)];
                    if (result.IsEmpty())
                    {
                        Error($"Tag part '{part}' requires an input [tag parameter] value.");
                        return;
                    }
                }
                else
                {
                    result = [.. result.Where(t => t.Item1.AllowsParam)];
                    if (result.IsEmpty())
                    {
                        Error($"Tag part '{part}' cannot have a [tag parameter].");
                        return;
                    }
                }
                int longestPart = result.Max(p => p.Item2);
                result = [.. result.Where(p => p.Item2 == longestPart)];
                possibleRoots = [.. result.SelectMany(p =>
                {
                    if (p.Item1.BaseType == Docs.ObjectTagType && p.Item1.AfterDotCleaned == "as")
                    {
                        string type = Tag.Parts[index].Parameter.ToLowerFast();
                        if (!type.EndsWith("tag"))
                        {
                            type = $"{type}tag";
                        }
                        MetaObjectType wantedType = Docs.ObjectTypes.GetValueOrDefault(type);
                        if (wantedType == null)
                        {
                            Error($"Tag part 'as[{type}]' is invalid: type name given doesn't appear to be a real object type.");
                            return [];
                        }
                        return ParsePossibleTypes(type, wantedType);
                    }
                    return ParsePossibleTypes(p.Item1.Returns, p.Item1.ReturnType);
                })];
                Tag.Parts[index].PossibleSubTypes = GetFullComplexSetFrom(possibleRoots);
                index += longestPart;
            }
        }

        /// <summary>Gets all possible object types from a set of known types, based on their bases and implements.</summary>
        public HashSet<MetaObjectType> GetFullComplexSetFrom(HashSet<MetaObjectType> original)
        {
            HashSet<MetaObjectType> result = new(original.Count * 2);
            foreach (MetaObjectType type in original)
            {
                if (type == Docs.ObjectTagType)
                {
                    return [.. Docs.ObjectTypes.Values];
                }
                result.Add(type);
                MetaObjectType baseType = type.BaseType;
                while (baseType != null)
                {
                    result.Add(baseType);
                    baseType = baseType.BaseType;
                }
                foreach (MetaObjectType implType in type.Implements)
                {
                    result.Add(implType);
                }
                foreach (MetaObjectType extendType in Docs.ObjectTypes.Values)
                {
                    if (extendType.Implements.Contains(type))
                    {
                        result.Add(extendType);
                    }
                }
            }
            result.Add(Docs.ObjectTagType);
            return result;
        }

        /// <summary>Traces just one part of the tag, gathering a list of possible subtags.</summary>
        public List<(MetaTag, int)> TraceTagPartSingle(HashSet<MetaObjectType> possibleRoots, int index)
        {
            if (possibleRoots == null || possibleRoots.Contains(null))
            {
                return null;
            }
            List<(MetaTag, int)> result = [];
            string part = Tag.Parts[index].Text.ToLowerFast();
            foreach (MetaObjectType type in GetFullComplexSetFrom(possibleRoots))
            {
                if (index + 2 < Tag.Parts.Count && type.SubTags.TryGetValue(part + "." + Tag.Parts[index + 1].Text + "." + Tag.Parts[index + 2].Text, out MetaTag veryComplexTag))
                {
                    for (int i = 0; i < 3; i++)
                    {
                        Tag.Parts[index + i].PossibleTags.Add(veryComplexTag);
                    }
                    result.Add((veryComplexTag, 3));
                }
                else if (index + 1 < Tag.Parts.Count && type.SubTags.TryGetValue(part + "." + Tag.Parts[index + 1].Text, out MetaTag complexTag))
                {
                    for (int i = 0; i < 2; i++)
                    {
                        Tag.Parts[index + i].PossibleTags.Add(complexTag);
                    }
                    result.Add((complexTag, 2));
                }
                else if (type.SubTags.TryGetValue(part, out MetaTag subTag))
                {
                    Tag.Parts[index].PossibleTags.Add(subTag);
                    result.Add((subTag, 1));
                }
            }
            return result;
        }
    }
}
