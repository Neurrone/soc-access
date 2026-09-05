using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using UnityEngine;

namespace SongsOfConquestAccess.Loader.Dev
{
    /// <summary>
    /// Dumps the live Unity object hierarchy as JSON so a developer or agent who cannot see the
    /// screen can find out what is on it. Deliberately game-agnostic: it knows nothing about
    /// the game's own GUI framework, only about Transforms, Components and any component
    /// that exposes its caption as a string property called "text" or "Text". That is enough to
    /// reverse-engineer an unfamiliar UI, and it works unchanged in any Unity game.
    ///
    /// A whole scene is far too big to read at once, so callers narrow it: "path" starts the dump
    /// at a named object, "depth" limits how far below that it walks, and a hard node cap bounds
    /// the response whatever they ask for.
    ///
    /// Main-thread only (reads live scene objects).
    /// </summary>
    internal static class GuiDump
    {
        internal static void ClearReflectionCache()
        {
            TextProperties.Clear();
        }
        public const int DefaultDepth = 6;

        private const int MaxNodes = 5000;
        private const int MaxTextLength = 200;

        // Component types every object carries; listing them buries the interesting ones.
        private static readonly string[] StructuralComponents =
        {
            "Transform",
            "RectTransform",
            "CanvasRenderer",
        };

        private static readonly Dictionary<Type, PropertyInfo> TextProperties =
            new Dictionary<Type, PropertyInfo>();

        private sealed class Budget
        {
            public int Written;
            public bool Truncated;
        }

        public static string Dump(string path, int depth)
        {
            List<Transform> roots = Roots(path);
            Budget budget = new Budget();

            return DevJson.Write(json =>
            {
                json.WriteStartObject();
                json.WritePropertyName("path");
                json.WriteValue(path ?? "");
                json.WritePropertyName("depth");
                json.WriteValue(depth);
                json.WritePropertyName("roots");
                json.WriteStartArray();
                foreach (Transform root in roots)
                {
                    WriteNode(json, root, depth, budget);
                }

                json.WriteEndArray();
                json.WritePropertyName("nodeCount");
                json.WriteValue(budget.Written);
                json.WritePropertyName("truncated");
                json.WriteValue(budget.Truncated);
                json.WriteEndObject();
            });
        }

        private static void WriteNode(
            JsonTextWriter json,
            Transform node,
            int depth,
            Budget budget
        )
        {
            if (budget.Written >= MaxNodes)
            {
                budget.Truncated = true;
                return;
            }

            budget.Written++;
            GameObject owner = node.gameObject;

            json.WriteStartObject();
            json.WritePropertyName("name");
            json.WriteValue(owner.name);
            json.WritePropertyName("active");
            json.WriteValue(owner.activeInHierarchy);

            json.WritePropertyName("components");
            json.WriteStartArray();
            string text = null;
            foreach (Component component in owner.GetComponents<Component>())
            {
                if (component == null)
                {
                    continue; // a script the game failed to load
                }

                Type type = component.GetType();
                if (Array.IndexOf(StructuralComponents, type.Name) >= 0)
                {
                    continue;
                }

                json.WriteValue(type.Name);
                if (text == null)
                {
                    text = ReadText(component, type);
                }
            }

            json.WriteEndArray();

            if (!string.IsNullOrEmpty(text))
            {
                json.WritePropertyName("text");
                json.WriteValue(text);
            }

            if (depth > 0 && node.childCount > 0)
            {
                json.WritePropertyName("children");
                json.WriteStartArray();
                for (int i = 0; i < node.childCount; i++)
                {
                    WriteNode(json, node.GetChild(i), depth - 1, budget);
                }

                json.WriteEndArray();
            }

            json.WriteEndObject();
        }

        // Whatever this component calls its caption, as long as it calls it "text" or "Text".
        private static string ReadText(Component component, Type type)
        {
            PropertyInfo property;
            if (!TextProperties.TryGetValue(type, out property))
            {
                property = FindTextProperty(type);
                TextProperties[type] = property;
            }

            if (property == null)
            {
                return null;
            }

            try
            {
                return Shorten(property.GetValue(component, null) as string);
            }
            catch (Exception)
            {
                return null; // a getter that needs state this object does not have yet
            }
        }

        private static PropertyInfo FindTextProperty(Type type)
        {
            foreach (string name in new[] { "text", "Text" })
            {
                PropertyInfo property;
                try
                {
                    property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                }
                catch (AmbiguousMatchException)
                {
                    continue;
                }

                if (
                    property != null
                    && property.PropertyType == typeof(string)
                    && property.CanRead
                    && property.GetIndexParameters().Length == 0
                )
                {
                    return property;
                }
            }

            return null;
        }

        // Objects named by the path, or every scene root when no path was given.
        private static List<Transform> Roots(string path)
        {
            List<Transform> sceneRoots = SceneRoots();
            if (string.IsNullOrEmpty(path))
            {
                return sceneRoots;
            }

            string[] names = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            List<Transform> matches = new List<Transform>();
            foreach (Transform root in sceneRoots)
            {
                if (root.name == names[0])
                {
                    matches.Add(root);
                }
            }

            for (int i = 1; i < names.Length; i++)
            {
                List<Transform> children = new List<Transform>();
                foreach (Transform parent in matches)
                {
                    for (int c = 0; c < parent.childCount; c++)
                    {
                        Transform child = parent.GetChild(c);
                        if (child.name == names[i])
                        {
                            children.Add(child);
                        }
                    }
                }

                matches = children;
            }

            return matches;
        }

        // Every parentless object that lives in a scene, active or not. FindObjectsOfTypeAll is
        // what reaches inactive objects at all; the scene check then drops prefabs and other
        // loaded assets, which have no scene, and hideFlags drops engine-internal objects.
        private static List<Transform> SceneRoots()
        {
            List<Transform> roots = new List<Transform>();
            foreach (Transform transform in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (transform.parent != null)
                {
                    continue;
                }

                GameObject owner = transform.gameObject;
                if (!owner.scene.IsValid() || (owner.hideFlags & HideFlags.HideInHierarchy) != 0)
                {
                    continue;
                }

                roots.Add(transform);
            }

            roots.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
            return roots;
        }

        private static string Shorten(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            string collapsed = text.Replace("\r", "").Replace("\n", "\\n");
            return collapsed.Length > MaxTextLength
                ? collapsed.Substring(0, MaxTextLength) + "..."
                : collapsed;
        }
    }
}
