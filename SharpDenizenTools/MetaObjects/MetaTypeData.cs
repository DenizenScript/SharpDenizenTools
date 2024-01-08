using FreneticUtilities.FreneticExtensions;
using SharpDenizenTools.MetaHandlers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SharpDenizenTools.MetaObjects
{
    /// <summary>A generic-less <see cref="MetaTypeData{T}"/> interface.</summary>
    public interface IMetaTypeData
    {
        /// <summary>Getter to create a new instance of this meta type.</summary>
        Func<MetaObject> Getter { get; }

        /// <summary>All meta docs of this type, by name.</summary>
        Dictionary<string, MetaObject> Meta {  get; }

        /// <summary>The meta type this data is for.</summary>
        MetaType Type { get; }

        /// <summary>Registers a new <see cref="MetaTypeData{T}"/> with the provided values.</summary>
        public static void Register<T>(MetaDocs meta, MetaType type, Dictionary<string, T> storage, Func<T> getter)
            where T : MetaObject
        {
            MetaTypeData<T> newData = new(storage, getter, type);
            meta.MetaTypesData.Add(type.Name.ToLowerFast(), newData);
        }

        /// <summary>Registers a new <see cref="MetaTypeData{T}"/> with the provided values.</summary>
        public static void Register<T>(MetaDocs meta, string type, Dictionary<string, T> storage, Func<T> getter)
            where T : MetaObject
        {
            MetaTypeData<T> newData = new(storage, getter, null);
            meta.MetaTypesData.Add(type, newData);
        }

        /// <summary>Creates a new <see cref="MetaObject"/> using <see cref="Getter"/> and sets it's type.</summary>
        public MetaObject CreateNewMeta()
        {
            MetaObject newMeta = Getter();
            newMeta.Type = Type;
            return newMeta;
        }
    }

    /// <summary>Data for a specific <see cref="MetaType"/></summary>
    public record MetaTypeData<T>(Dictionary<string, T> Meta, Func<T> Getter, MetaType Type) : IMetaTypeData where T : MetaObject
    {
        Func<MetaObject> IMetaTypeData.Getter => Getter;
        Dictionary<string, MetaObject> IMetaTypeData.Meta => Meta?.ToDictionary(pair => pair.Key, pair => pair.Value as MetaObject);
        MetaType IMetaTypeData.Type => Type;
    }
}
