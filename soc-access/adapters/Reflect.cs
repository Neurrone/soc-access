using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Zenject;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>The reflected reads every adapter needs: one field read, one container resolve, one
    /// installer container. Each adapter resolves its own <see cref="FieldInfo"/> handles once and
    /// passes them here; the by-name overloads are for the objects whose runtime type the mod cannot
    /// name at compile time, and they cache one handle per type and name.</summary>
    public static class Reflect
    {
        private static readonly Dictionary<Type, Dictionary<string, FieldInfo>> FieldHandles =
            new Dictionary<Type, Dictionary<string, FieldInfo>>();
        private static readonly PropertyInfo InstallerContainerProperty =
            AccessTools.Property(typeof(MonoInstallerBase), "Container");

        /// <summary>The field's value, or null when the owner, the handle or the value is missing or
        /// of another type.</summary>
        public static T Get<T>(object owner, FieldInfo field) where T : class
        {
            return owner != null && field != null ? field.GetValue(owner) as T : null;
        }

        /// <summary>The field's value cast to <typeparamref name="T"/>, for the value types
        /// <see cref="Get{T}"/> cannot answer. Throws if the field holds another type.</summary>
        public static T Cast<T>(object owner, FieldInfo field)
        {
            return owner != null && field != null ? (T)field.GetValue(owner) : default(T);
        }

        /// <summary>The named field's value cast to <typeparamref name="T"/>, the handle looked up on
        /// the owner's own runtime type and cached.</summary>
        public static T Cast<T>(object owner, string name)
        {
            if (owner == null)
            {
                return default(T);
            }

            FieldInfo field = Field(owner.GetType(), name);
            return field != null ? (T)field.GetValue(owner) : default(T);
        }

        /// <summary>The named field's value when it is a <typeparamref name="T"/>, and
        /// <c>default(T)</c> when it is anything else.</summary>
        public static T Typed<T>(object owner, string name)
        {
            if (owner == null)
            {
                return default(T);
            }

            FieldInfo field = Field(owner.GetType(), name);
            object value = field != null ? field.GetValue(owner) : null;
            return value is T ? (T)value : default(T);
        }

        /// <summary>The handle for one named field of one runtime type, resolved once. The field is
        /// looked up on the object's own type, as a browser's items are subclasses of what its
        /// public surface names.</summary>
        public static FieldInfo Field(Type type, string name)
        {
            if (type == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            Dictionary<string, FieldInfo> byName;
            if (!FieldHandles.TryGetValue(type, out byName))
            {
                byName = new Dictionary<string, FieldInfo>();
                FieldHandles[type] = byName;
            }

            FieldInfo field;
            if (!byName.TryGetValue(name, out field))
            {
                field = AccessTools.Field(type, name);
                byName[name] = field;
            }

            return field;
        }

        /// <summary>The binding, or null when the container has none: Zenject throws for an unbound
        /// type and the mod asks for bindings the game does not always install.</summary>
        public static T Resolve<T>(DiContainer container) where T : class
        {
            if (container == null)
            {
                return null;
            }

            try
            {
                return container.Resolve<T>();
            }
            catch
            {
                // Silent on purpose: an absent binding is an expected answer here and throwing is
                // how Zenject says so, so the null the caller already handles is the whole report.
                return null;
            }
        }

        /// <summary>The container a scene installer was injected with. <c>Container</c> is protected
        /// on <see cref="MonoInstallerBase"/>, so every installer answers through the one handle.</summary>
        public static DiContainer InstallerContainer(MonoInstallerBase installer)
        {
            if (installer == null || InstallerContainerProperty == null)
            {
                return null;
            }

            return InstallerContainerProperty.GetValue(installer, null) as DiContainer;
        }
    }
}
