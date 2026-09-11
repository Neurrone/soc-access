using System;
using System.Collections.Generic;
using UnityEngine;

namespace SongsOfConquestAccess.UI
{
    /// <summary>
    /// PUT A BAND IN THE ORDER THE GAME DRAWS IT, measured from the live transforms every build, so
    /// a layout the game changes - a button the state hides, a column it reflows - is followed
    /// rather than guessed at from declaration order.
    ///
    /// Every sort here is a STABLE insertion sort: the bands are a handful of controls, and two of
    /// them sharing an edge must keep the order the screen declared them in rather than swapping
    /// about from frame to frame. Left to right is ascending x; top to bottom is DESCENDING y,
    /// because Unity's y grows upwards.
    /// </summary>
    public static class DrawnOrder
    {
        /// <summary>The drawn left edge of a control, 0 for one that is not there.</summary>
        public static float LeftOf(Component component)
        {
            return component != null && component.transform != null ? component.transform.position.x : 0f;
        }

        /// <summary>The drawn top edge of a control, 0 for one that is not there.</summary>
        public static float TopOf(Component component)
        {
            return component != null && component.transform != null ? component.transform.position.y : 0f;
        }

        /// <summary>Order the band left to right by the drawn left edges of the controls behind
        /// it.</summary>
        public static void SortByLeft<T>(List<T> items, Func<T, Component> component)
        {
            List<float> lefts = new List<float>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                lefts.Add(LeftOf(component(items[i])));
            }

            SortAscending(items, lefts);
        }

        /// <summary>Order the band top to bottom by the drawn top edges of the controls behind
        /// it.</summary>
        public static void SortByTop<T>(List<T> items, Func<T, Component> component)
        {
            List<float> tops = new List<float>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                tops.Add(TopOf(component(items[i])));
            }

            SortDescending(items, tops);
        }

        /// <summary>Order a band whose entries already carry their drawn edge as the pair's key,
        /// smallest first.</summary>
        public static void SortByKey<T>(List<KeyValuePair<float, T>> items)
        {
            for (int i = 1; i < items.Count; i++)
            {
                KeyValuePair<float, T> moving = items[i];
                int j = i - 1;
                while (j >= 0 && items[j].Key > moving.Key)
                {
                    items[j + 1] = items[j];
                    j--;
                }

                items[j + 1] = moving;
            }
        }

        /// <summary>Order a band by a key list measured alongside it, smallest first. The key list is
        /// reordered with the band.</summary>
        public static void SortAscending<T>(List<T> items, List<float> keys)
        {
            for (int i = 1; i < items.Count; i++)
            {
                T moving = items[i];
                float key = keys[i];
                int j = i - 1;
                while (j >= 0 && keys[j] > key)
                {
                    items[j + 1] = items[j];
                    keys[j + 1] = keys[j];
                    j--;
                }

                items[j + 1] = moving;
                keys[j + 1] = key;
            }
        }

        /// <summary>Order a band by a key list measured alongside it, largest first. The key list is
        /// reordered with the band.</summary>
        public static void SortDescending<T>(List<T> items, List<float> keys)
        {
            for (int i = 1; i < items.Count; i++)
            {
                T moving = items[i];
                float key = keys[i];
                int j = i - 1;
                while (j >= 0 && keys[j] < key)
                {
                    items[j + 1] = items[j];
                    keys[j + 1] = keys[j];
                    j--;
                }

                items[j + 1] = moving;
                keys[j + 1] = key;
            }
        }
    }
}
