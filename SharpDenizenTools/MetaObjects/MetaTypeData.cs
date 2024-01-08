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
        /// <summary>Creates a new <see cref="MetaObject"/> and sets its type.</summary>
        MetaObject CreateNewMeta();

        /// <summary>Gets a <see cref="MetaObject"/> by name, or <c>null</c> if one by that name doesn't exist.</summary>
        MetaObject MetaByNameIfPresent(string name);

        /// <summary>Returns all <see cref="MetaObject"/>s of this type.</summary>
        IEnumerable<MetaObject> AllMetaObjects();
    }

    /// <summary>Data for a specific <see cref="MetaType"/>.</summary>
    public record MetaTypeData<T>(Dictionary<string, T> Meta, Func<T> Getter, MetaType Type) : IMetaTypeData where T : MetaObject
    {
        /// <inheritdoc/>
        public MetaObject CreateNewMeta()
        {
            MetaObject newMeta = Getter();
            newMeta.Type = Type;
            return newMeta;
        }

        /// <inheritdoc/>
        public MetaObject MetaByNameIfPresent(string name)
        {
            return Meta?.GetValueOrDefault(name);
        }

        /// <inheritdoc/>
        public IEnumerable<MetaObject> AllMetaObjects()
        {
            return Meta?.Values ?? Enumerable.Empty<MetaObject>();
        }
    }
}
