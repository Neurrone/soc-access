using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>
    /// The game's own screen-input response, made to read as a mouse sitting at a screen point
    /// while the mod invokes a native click or drag, and put back exactly as it was afterwards.
    /// Each caller overrides only the properties the native path it invokes reads: the battle's
    /// click path reads five, a troop drag reads the position alone.
    /// </summary>
    public sealed class ScreenInputOverride : IDisposable
    {
        private readonly object _response;
        private readonly PropertyInfo[] _properties;
        private readonly object[] _oldValues;
        private bool _restored;

        private ScreenInputOverride(object response, PropertyInfo[] properties, object[] newValues)
        {
            _response = response;
            _properties = properties;
            _oldValues = new object[properties.Length];
            for (int index = 0; index < properties.Length; index++)
            {
                _oldValues[index] = properties[index].GetValue(response, null);
            }

            for (int index = 0; index < properties.Length; index++)
            {
                properties[index].SetValue(response, newValues[index], null);
            }
        }

        /// <summary>
        /// The object whose input properties the game actually reads: the primary screen input
        /// itself where it is writable, else the response it is currently delegating to.
        /// </summary>
        public static object ResolveWritableResponse(object primary)
        {
            if (primary == null)
            {
                return null;
            }

            if (HasWritableProperty(primary, "Position"))
            {
                return primary;
            }

            FieldInfo currentResponseField = AccessTools.Field(primary.GetType(), "_currentResponse");
            object currentResponse = currentResponseField != null ? currentResponseField.GetValue(primary) : null;
            if (HasWritableProperty(currentResponse, "Position"))
            {
                return currentResponse;
            }

            FieldInfo mouseResponseField = AccessTools.Field(primary.GetType(), "_mouseResponse");
            object mouseResponse = mouseResponseField != null ? mouseResponseField.GetValue(primary) : null;
            return HasWritableProperty(mouseResponse, "Position") ? mouseResponse : null;
        }

        /// <summary>The pointer position alone, for a native path that reads nothing else.</summary>
        public static ScreenInputOverride ApplyPosition(object response, Vector2 screenPosition, string owner)
        {
            return Apply(
                response,
                owner,
                new[] { "Position" },
                new object[] { screenPosition });
        }

        /// <summary>
        /// A still mouse over the world at a screen point: the position, no movement this frame,
        /// and not over, panning from, or activated over the UI.
        /// </summary>
        public static ScreenInputOverride ApplyMouseClick(object response, Vector2 screenPosition, string owner)
        {
            return Apply(
                response,
                owner,
                new[] { "Position", "Delta", "IsOverUI", "IsPanning", "WasActivatedOverUI" },
                new object[] { screenPosition, Vector2.zero, false, false, false });
        }

        public void Restore()
        {
            if (_restored)
            {
                return;
            }

            for (int index = 0; index < _properties.Length; index++)
            {
                _properties[index].SetValue(_response, _oldValues[index], null);
            }

            _restored = true;
        }

        public void Dispose()
        {
            Restore();
        }

        private static ScreenInputOverride Apply(object response, string owner, string[] names, object[] values)
        {
            if (response == null)
            {
                return null;
            }

            Type responseType = response.GetType();
            PropertyInfo[] properties = new PropertyInfo[names.Length];
            for (int index = 0; index < names.Length; index++)
            {
                PropertyInfo property = AccessTools.Property(responseType, names[index]);
                if (property == null || !property.CanWrite)
                {
                    SocAccessMod.Instance?.LogWarning(owner + " could not override native screen input because required writable properties were missing on " + responseType.FullName);
                    return null;
                }

                properties[index] = property;
            }

            return new ScreenInputOverride(response, properties, values);
        }

        private static bool HasWritableProperty(object target, string name)
        {
            if (target == null)
            {
                return false;
            }

            PropertyInfo property = AccessTools.Property(target.GetType(), name);
            return property != null && property.CanWrite;
        }
    }
}
