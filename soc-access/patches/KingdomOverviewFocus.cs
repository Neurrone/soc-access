using System;
using System.Reflection;
using SongsOfConquest.Common.Entities;
using SongsOfConquestAccess.Events;

namespace SongsOfConquestAccess
{
    /// <summary>
    /// What the two kingdom overview menus' click hooks do, which is the same thing twice: read the
    /// map entity the clicked row points at before the game's handler runs, then, after it has moved
    /// the camera, say where the camera went. Both menus keep that entity in a private field, and
    /// both keep a row's cycle in an <c>IMapEntity[]</c> plus an index into it.
    ///
    /// Nothing here holds state, so a reload takes nothing with it.
    /// </summary>
    public static class KingdomOverviewFocus
    {
        /// <summary>Announces where the game's handler has just moved the camera. Nothing to say
        /// when the click had no entity to move to.</summary>
        public static void Focus(IMapEntity entity)
        {
            if (entity != null)
            {
                AccessibilityEventBus.Publish(new MapCameraFocusEvent(entity.Position, announce: true));
            }
        }

        /// <summary>The map entity a row keeps in one of its private fields. <paramref name="what"/>
        /// names the read in the log when the reflection fails.</summary>
        public static IMapEntity ReadEntity(object entry, FieldInfo field, string what)
        {
            if (entry == null || field == null)
            {
                return null;
            }

            try
            {
                return field.GetValue(entry) as IMapEntity;
            }
            catch (Exception ex)
            {
                SocAccessMod.Instance?.LogWarning(what + ": " + ex.Message);
                return null;
            }
        }

        /// <summary>The entity a cycling row points at now: the row's entity array indexed by the
        /// cycle index the game last left there.</summary>
        public static IMapEntity ReadCycled(object entry, FieldInfo entitiesField, FieldInfo indexField, string what)
        {
            if (entry == null || entitiesField == null || indexField == null)
            {
                return null;
            }

            try
            {
                IMapEntity[] entities = entitiesField.GetValue(entry) as IMapEntity[];
                object indexValue = indexField.GetValue(entry);
                int index = indexValue is int ? (int)indexValue : -1;
                if (entities == null || index < 0 || index >= entities.Length)
                {
                    return null;
                }

                return entities[index];
            }
            catch (Exception ex)
            {
                SocAccessMod.Instance?.LogWarning(what + ": " + ex.Message);
                return null;
            }
        }
    }
}
