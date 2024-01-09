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
        bool TryGetMeta(string name, out MetaObject meta);

        /// <summary>Returns all <see cref="MetaObject"/>s of this type.</summary>
        IEnumerable<MetaObject> AllMetaObjects();
    }

    /// <summary>Data for a specific <see cref="MetaType"/>.</summary>
    public record MetaTypeData<T>(Dictionary<string, T> Meta, MetaType Type) : IMetaTypeData where T : MetaObject, new()
    {
        /// <inheritdoc/>
        public MetaObject CreateNewMeta()
        {
            return new T { Type = Type };
        }

        /// <inheritdoc/>
        public bool TryGetMeta(string name, out MetaObject meta)
        {
            meta = Meta?.GetValueOrDefault(name);
            return meta != null;
        }

        /// <inheritdoc/>
        public IEnumerable<MetaObject> AllMetaObjects()
        {
            return Meta?.Values ?? Enumerable.Empty<MetaObject>();
        }
    }
}
