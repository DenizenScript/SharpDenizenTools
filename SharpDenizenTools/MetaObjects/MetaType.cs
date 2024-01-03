using FreneticUtilities.FreneticExtensions;
using SharpDenizenTools.MetaHandlers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SharpDenizenTools.MetaObjects
{

    /// <summary>Generic-less <see cref="MetaType{T}"/> interface.</summary>
    public interface IMetaType
    {
        /// <summary>The name of this meta type.</summary>
        string Name { get; }

        /// <summary>The webpath for this meta type (eg "Commands").</summary>
        string WebPath { get; }

        /// <summary>All currently loaded meta objects of this type.</summary>
        Dictionary<string, MetaObject> Meta { get; }

        /// <summary>A function to create a new meta object of this type.</summary>
        Func<MetaObject> Getter { get; }

        /// <summary>Creates a new meta object of this type.</summary>
        public MetaObject CreateNewMeta()
        {
            MetaObject newMeta = Getter();
            newMeta.Type = this;
            return newMeta;
        }
    }

    /// <summary>Represents a type of meta documentations.</summary>
    public class MetaType<T> : IMetaType where T : MetaObject
    {
        string IMetaType.Name => Name;
        string IMetaType.WebPath => WebPath;
        Func<MetaObject> IMetaType.Getter => Getter;
        Dictionary<string, MetaObject> IMetaType.Meta => Meta.ToDictionary(pair => pair.Key, pair => pair.Value as MetaObject);

        /// <summary>The name of the meta type.</summary>
        public string Name;

        /// <summary>The webpath for this meta type (eg "Commands").</summary>
        public string WebPath;

        /// <summary>All currently loaded meta objects of this type.</summary>
        public Dictionary<string, T> Meta;

        /// <summary>A function to create a new meta object of this type.</summary>
        public Func<T> Getter;


        /// <summary>Creates a new meta type with the specified dictionary size and registers it into <paramref name="docs"/>.</summary>
        public MetaType(MetaDocs docs, string name, string webPath, int dictionarySize, Func<T> getter) {
            Name = name;
            WebPath = webPath;
            Meta = new(dictionarySize);
            Getter = getter;
            docs.MetaTypes.Add(name.ToLowerFast(), this);
        }
    }
}
