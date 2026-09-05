using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Newtonsoft.Json;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Loader;
using SongsOfConquestAccess.Loader.Dev;
using TMPro;
using UnityEngine;

namespace SongsOfConquestAccess.Dev
{
    /// <summary>
    /// Dumps the game's own UI as the meaning a screen reader would need, rather than as the Unity
    /// components <c>/gui/game</c> reports. Where that raw dump answers "what objects exist", this
    /// answers "what is on screen, what does it say, and what can the player operate": it reads
    /// captions through <see cref="UITextMeshTextUtility"/>, reads each control's tooltip without
    /// hovering through <see cref="NativeTooltipUtility"/>, works out interactability over the whole
    /// ancestor chain, and throws away the frames and backdrops that make a raw dump unreadable.
    /// Its purpose is coverage: what the game draws that the mod never declares.
    ///
    /// Roots mirror what the player can reach. Every root <see cref="Canvas"/> in the loaded scenes
    /// is a root here, topmost first - this game has no modal registry, so the canvas sorting order
    /// it draws by is the modality signal (the system popup canvas sits at 32767, the adventure HUD
    /// canvases below zero). The top-level "windows" array names them all, visible or not, so a
    /// caller can pick a <c>path=</c>.
    ///
    /// Query parameters:
    ///   path=Name      dump this node instead of the whole screen. Matched, in order, against the
    ///                  top-level roots by exact then case-insensitive name, then breadth-first
    ///                  against every named transform under them by exact name, then by
    ///                  case-insensitive substring. A name nothing answers to is a 404 carrying
    ///                  windows[], never silence; a match with nothing to report answers a "note"
    ///                  saying which of depth= or visibleOnly= emptied it.
    ///   depth=N        levels below each root (default <see cref="DefaultDepth"/>). A node sitting
    ///                  on that cutoff is reported even when it is a bare container, carrying
    ///                  "more": true - it has children this dump did not walk, and pruning it as
    ///                  decoration would report an empty tree for a window that is fully drawn.
    ///   visibleOnly=0  include hidden roots and subtrees (default 1, skip them). Visible means
    ///                  active in the hierarchy, with no CanvasGroup faded to nothing and no
    ///                  disabled Canvas above.
    ///   fields=a,b,c   answer plain text instead of JSON: one line per node in tree order,
    ///                  indented two spaces per level, carrying only these fields separated by
    ///                  " | " and only where they have something to say (see <see cref="Fields"/>).
    ///
    /// Per node: "name" (GameObject), "kind" (button/toggle/slider/dropdown/input/text/image/
    /// canvas/panel), "text", "tooltip", "value", "rect", "children", and "interactable" - true
    /// only when the node carries a control and it and every ancestor are active, unfaded and
    /// enabled, since a disabled ancestor kills a whole subtree. "visible" appears only when false.
    /// The rect is screen pixels with a top-left origin, which is what crop-shot.ps1 crops by;
    /// z-order occlusion is not considered. Nodes with nothing to say - no control, no text, no
    /// tooltip, no value and no surviving children - are pruned, which is what keeps the tree
    /// screen-reader sized.
    ///
    /// Main-thread only (reads live scene objects), and side-effect free: it never hovers, selects
    /// or focuses anything, and tooltip text comes from the same <c>ITooltipable.GetDetails</c> read
    /// the mod already uses at draw time. Every per-node read is guarded: a getter that throws costs
    /// that one field, not the dump.
    /// </summary>
    internal static class UnityDump
    {
        public const int DefaultDepth = 6;

        private const int MaxNodes = 6000;
        private const int MaxTextLength = 200;
        private const int MaxTooltipLines = 12;

        // How much of the hierarchy a path= lookup may sweep looking for a named node. Large enough
        // to reach the leaves of every screen this game draws, bounded so a lookup cannot hang.
        private const int MaxSearchNodes = 40000;

        /// <summary>The field names <c>?fields=</c> understands.</summary>
        public static readonly string[] Fields =
        {
            "name",
            "kind",
            "text",
            "tooltip",
            "value",
            "interactable",
            "visible",
            "rect",
            "more",
        };

        /// <summary>A finished answer and the status it deserves, so a <c>path=</c> that matched
        /// nothing can be a 404 in whichever format the caller asked for.</summary>
        internal sealed class Answer
        {
            public int Status = 200;
            public string Body;
        }

        /// <summary>One interpreted node. Built in full before anything is written because pruning a
        /// node depends on whether its children survived, which a streaming writer cannot take
        /// back.</summary>
        private sealed class Node
        {
            public string Name;
            public string Kind;
            public string Text;
            public string Tooltip;
            public string Value;
            public bool Visible = true;
            public bool Interactable;
            public bool HasControl;
            public bool More;
            public bool HasRect;
            public int X;
            public int Y;
            public int Width;
            public int Height;
            public readonly List<Node> Children = new List<Node>();

            public bool Speaks
            {
                get
                {
                    return HasControl
                        || Text != null
                        || Tooltip != null
                        || Value != null
                        || Children.Count > 0
                        || More;
                }
            }
        }

        private sealed class Window
        {
            public string Name;
            public bool Visible;
        }

        /// <summary>One walk of the hierarchy and everything an answer needs to describe it, so the
        /// JSON dump and the plain-text projection report the same tree rather than two walks that
        /// could disagree.</summary>
        private sealed class Scan
        {
            public string Error;
            public string Note;
            public string Matched;
            public int Status = 200;
            public int Visited;
            public bool Truncated;
            public ILocalizationHandler Localization;
            public readonly List<Window> Windows = new List<Window>();
            public readonly List<Node> Nodes = new List<Node>();
        }

        /// <summary>The whole route: parse, reject nonsense loudly, then answer from the main
        /// thread.</summary>
        public static DevResponse Route(DevRequest request, ModHost host)
        {
            string path = request.QueryValue("path");

            // QueryInt falls back silently, so depth=banana would have read as the default and the
            // caller would never know: a declared parameter has to be rejected like an undeclared
            // one when its value makes no sense.
            string depthText = request.QueryValue("depth");
            int depth = DefaultDepth;
            if (depthText != null && (!int.TryParse(depthText, out depth) || depth < 0))
            {
                return DevResponse.Json(
                    400,
                    DevJson.Error("depth= expects a whole number of levels, not '" + depthText + "'")
                );
            }

            bool visibleOnly;
            string visibleText = request.QueryValue("visibleOnly");
            if (!ModRoutes.ParseFlag(visibleText, true, out visibleOnly))
            {
                return DevResponse.Json(
                    400,
                    DevJson.Error(
                        "visibleOnly= expects 1/0 or true/false, not '" + visibleText + "'"
                    )
                );
            }

            string projection = request.QueryValue("fields");
            if (projection == null)
            {
                Answer json = (Answer)host.MainThread.Run(() => Dump(path, depth, visibleOnly));
                return DevResponse.Json(json.Status, json.Body);
            }

            List<string> fields = ParseFields(projection);
            if (fields.Count == 0)
            {
                return DevResponse.Json(
                    400,
                    DevJson.Error("fields= names no field; /gui/unity can project: " + KnownFields())
                );
            }

            string unknown = UnknownField(fields);
            if (unknown != null)
            {
                return DevResponse.Json(
                    400,
                    DevJson.Error(
                        "unknown field '" + unknown + "' in fields=; /gui/unity can project: "
                            + KnownFields()
                    )
                );
            }

            Answer plain = (Answer)
                host.MainThread.Run(() => Lines(path, depth, visibleOnly, fields));
            return new DevResponse
            {
                StatusCode = plain.Status,
                ContentType = "text/plain; charset=utf-8",
                Body = Encoding.UTF8.GetBytes(plain.Body),
            };
        }

        public static Answer Dump(string path, int depth, bool visibleOnly)
        {
            Scan scan = Walk(path, depth, visibleOnly);
            string body = DevJson.Write(json =>
            {
                json.WriteStartObject();
                if (!string.IsNullOrEmpty(path))
                {
                    json.WritePropertyName("path");
                    json.WriteValue(path);
                }

                json.WritePropertyName("depth");
                json.WriteValue(depth);
                json.WritePropertyName("visibleOnly");
                json.WriteValue(visibleOnly);
                if (scan.Error != null)
                {
                    json.WritePropertyName("error");
                    json.WriteValue(scan.Error);
                }

                if (scan.Note != null)
                {
                    json.WritePropertyName("note");
                    json.WriteValue(scan.Note);
                }

                json.WritePropertyName("windows");
                json.WriteStartArray();
                foreach (Window window in scan.Windows)
                {
                    json.WriteStartObject();
                    json.WritePropertyName("name");
                    json.WriteValue(window.Name);
                    json.WritePropertyName("visible");
                    json.WriteValue(window.Visible);
                    json.WriteEndObject();
                }

                json.WriteEndArray();

                json.WritePropertyName("roots");
                json.WriteStartArray();
                int written = 0;
                foreach (Node node in scan.Nodes)
                {
                    written += Write(json, node);
                }

                json.WriteEndArray();
                json.WritePropertyName("nodeCount");
                json.WriteValue(written);
                json.WritePropertyName("visitedCount");
                json.WriteValue(scan.Visited);
                json.WritePropertyName("truncated");
                json.WriteValue(scan.Truncated);
                json.WriteEndObject();
            });

            return new Answer { Status = scan.Status, Body = body };
        }

        /// <summary>The same tree as <see cref="Dump"/>, projected onto the requested fields: one
        /// line per node in tree order, two spaces of indent per level. A node with none of the
        /// requested fields prints no line at all, so asking for text gives back the screen's words
        /// and nothing else.</summary>
        public static Answer Lines(string path, int depth, bool visibleOnly, List<string> fields)
        {
            Scan scan = Walk(path, depth, visibleOnly);
            StringBuilder text = new StringBuilder();
            if (scan.Error != null)
            {
                text.Append("error: ").Append(scan.Error).Append('\n');
                text.Append("windows: ").Append(WindowNames(scan)).Append('\n');
            }

            if (scan.Note != null)
            {
                text.Append("note: ").Append(scan.Note).Append('\n');
            }

            foreach (Node node in scan.Nodes)
            {
                Flatten(text, node, 0, fields);
            }

            if (scan.Truncated)
            {
                text.Append("(truncated at ").Append(MaxNodes).Append(" nodes)\n");
            }

            return new Answer { Status = scan.Status, Body = text.ToString() };
        }

        /// <summary>The field names in a <c>?fields=</c> value, trimmed and lower-cased; empty when
        /// the caller asked for nothing.</summary>
        public static List<string> ParseFields(string raw)
        {
            List<string> fields = new List<string>();
            foreach (string part in (raw ?? string.Empty).Split(','))
            {
                string name = part.Trim().ToLowerInvariant();
                if (name.Length > 0)
                {
                    fields.Add(name);
                }
            }

            return fields;
        }

        /// <summary>The first requested field this dump cannot project, or null when all of them are
        /// known.</summary>
        public static string UnknownField(List<string> fields)
        {
            foreach (string field in fields)
            {
                if (Array.IndexOf(Fields, field) < 0)
                {
                    return field;
                }
            }

            return null;
        }

        public static string KnownFields()
        {
            return string.Join(", ", Fields);
        }

        private static Scan Walk(string path, int depth, bool visibleOnly)
        {
            Scan scan = new Scan();
            try
            {
                scan.Localization = GlobalLocalizationVariables.LocalizationHandler;
            }
            catch (Exception) { }

            List<Canvas> canvases = RootCanvases();
            foreach (Canvas canvas in canvases)
            {
                scan.Windows.Add(
                    new Window { Name = canvas.gameObject.name, Visible = OnScreen(canvas) }
                );
            }

            List<Transform> roots = new List<Transform>();
            if (!string.IsNullOrEmpty(path))
            {
                Transform found = Locate(canvases, path);
                if (found == null)
                {
                    scan.Status = 404;
                    scan.Error =
                        "no node named '" + path + "'; see windows[] for the top-level roots";
                    return scan;
                }

                scan.Matched = found.name;
                roots.Add(found);
            }
            else
            {
                foreach (Canvas canvas in canvases)
                {
                    if (!visibleOnly || OnScreen(canvas))
                    {
                        roots.Add(canvas.transform);
                    }
                }

                // A canvas nested inside another selected canvas - every adventure menu sits inside
                // the menu parent canvas and overrides its sorting, which makes it a root canvas of
                // its own - is already in the tree; listing it again would dump it twice.
                DropNested(roots);
            }

            foreach (Transform root in roots)
            {
                Node node = Build(root, depth, visibleOnly, true, true, CanvasAbove(root), scan);
                if (node != null)
                {
                    scan.Nodes.Add(node);
                }
            }

            if (scan.Nodes.Count == 0)
            {
                scan.Note = EmptyReason(roots, depth, visibleOnly, scan.Matched);
            }

            return scan;
        }

        /// <summary>Why an answer came back empty. A dump that found its node and still reports
        /// nothing is the dangerous case - the caller reads silence as "not drawn" - so it always
        /// says which of depth= or visibleOnly= emptied it.</summary>
        private static string EmptyReason(
            List<Transform> roots,
            int depth,
            bool visibleOnly,
            string matched
        )
        {
            string subject = matched == null ? "nothing is on screen" : "'" + matched + "' matched";
            if (roots.Count == 0)
            {
                return visibleOnly
                    ? subject + ", and no root canvas is visible; retry with visibleOnly=0"
                    : subject + " and there is no root canvas to dump";
            }

            if (depth < 1)
            {
                return subject + ", but depth=" + depth + " walks nothing below it";
            }

            if (visibleOnly && !Visible(roots[0], true))
            {
                return subject + ", but it is not visible; retry with visibleOnly=0";
            }

            return subject + ", but it holds nothing with anything to say at depth=" + depth;
        }

        // Every canvas the engine draws as a root of its own, topmost first. Ties keep hierarchy
        // order, which is the order the raw dump reports.
        private static List<Canvas> RootCanvases()
        {
            List<Canvas> found = new List<Canvas>();
            foreach (Canvas canvas in Resources.FindObjectsOfTypeAll<Canvas>())
            {
                GameObject owner = canvas.gameObject;
                if (!owner.scene.IsValid() || (owner.hideFlags & HideFlags.HideInHierarchy) != 0)
                {
                    continue;
                }

                if (!canvas.isRootCanvas)
                {
                    continue;
                }

                found.Add(canvas);
            }

            List<Canvas> ordered = new List<Canvas>(found);
            ordered.Sort((left, right) =>
            {
                int byOrder = right.sortingOrder.CompareTo(left.sortingOrder);
                return byOrder != 0 ? byOrder : found.IndexOf(left).CompareTo(found.IndexOf(right));
            });
            return ordered;
        }

        private static bool OnScreen(Canvas canvas)
        {
            try
            {
                return canvas != null && canvas.enabled && canvas.gameObject.activeInHierarchy;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void DropNested(List<Transform> roots)
        {
            for (int i = roots.Count - 1; i >= 0; i--)
            {
                for (Transform parent = roots[i].parent; parent != null; parent = parent.parent)
                {
                    if (roots.Contains(parent))
                    {
                        roots.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        // The node a path= means: a top-level root by name first, then anything named under the
        // roots, so a banner or a button can be asked for by name whether or not it is shown.
        private static Transform Locate(List<Canvas> canvases, string path)
        {
            foreach (Canvas canvas in canvases)
            {
                if (string.Equals(canvas.gameObject.name, path, StringComparison.Ordinal))
                {
                    return canvas.transform;
                }
            }

            foreach (Canvas canvas in canvases)
            {
                if (string.Equals(canvas.gameObject.name, path, StringComparison.OrdinalIgnoreCase))
                {
                    return canvas.transform;
                }
            }

            List<Transform> queue = new List<Transform>();
            foreach (Canvas canvas in canvases)
            {
                queue.Add(canvas.transform);
            }

            Transform contains = null;
            for (int i = 0; i < queue.Count && i < MaxSearchNodes; i++)
            {
                Transform node = queue[i];
                if (string.Equals(node.name, path, StringComparison.Ordinal))
                {
                    return node;
                }

                if (
                    contains == null
                    && node.name.IndexOf(path, StringComparison.OrdinalIgnoreCase) >= 0
                )
                {
                    contains = node;
                }

                for (int child = 0; child < node.childCount; child++)
                {
                    queue.Add(node.GetChild(child));
                }
            }

            return contains;
        }

        private static Node Build(
            Transform transform,
            int depth,
            bool visibleOnly,
            bool ancestorsVisible,
            bool ancestorsInteractive,
            Canvas canvas,
            Scan scan
        )
        {
            if (transform == null)
            {
                return null;
            }

            if (scan.Visited >= MaxNodes)
            {
                scan.Truncated = true;
                return null;
            }

            GameObject owner = transform.gameObject;
            Canvas ownCanvas = owner.GetComponent<Canvas>();
            if (ownCanvas != null)
            {
                canvas = ownCanvas;
            }

            float alpha = 1f;
            bool groupInteractable = true;
            CanvasGroup group = owner.GetComponent<CanvasGroup>();
            if (group != null)
            {
                alpha = group.alpha;
                groupInteractable = group.interactable;
            }

            bool visible =
                ancestorsVisible
                && owner.activeInHierarchy
                && alpha > 0.001f
                && (ownCanvas == null || ownCanvas.enabled);
            if (visibleOnly && !visible)
            {
                return null;
            }

            scan.Visited++;

            Component control = null;
            Component textComponent = null;
            bool hasImage = false;
            foreach (Component component in owner.GetComponents<Component>())
            {
                if (component == null)
                {
                    continue; // a script the game failed to load
                }

                if (IsControl(component))
                {
                    if (control == null)
                    {
                        control = component;
                    }

                    continue;
                }

                if (IsText(component))
                {
                    if (textComponent == null)
                    {
                        textComponent = component;
                    }

                    continue;
                }

                if (
                    component is UnityEngine.UI.Graphic
                    || component is UIImage
                    || component is UIRawImage
                )
                {
                    hasImage = true;
                }
            }

            bool ownInteractable = control == null || ControlInteractable(control);
            Node node = new Node
            {
                Name = owner.name,
                Visible = visible,
                HasControl = control != null,
                Interactable = control != null && ancestorsInteractive && visible && ownInteractable,
                Kind = Kind(control, textComponent, ownCanvas, hasImage),
                Text = Clean(ReadText(textComponent, control)),
                Tooltip = ReadTooltip(transform, scan.Localization),
                Value = Clean(ReadValue(control)),
            };
            ReadRect(transform, canvas, node);

            if (depth > 0)
            {
                UISelectionLayer layer = owner.GetComponent<UISelectionLayer>();
                bool childrenInteractive =
                    ancestorsInteractive
                    && visible
                    && groupInteractable
                    && ownInteractable
                    && (layer == null || LayerInteractable(layer));
                for (int i = 0; i < transform.childCount; i++)
                {
                    Node built = Build(
                        transform.GetChild(i),
                        depth - 1,
                        visibleOnly,
                        visible,
                        childrenInteractive,
                        canvas,
                        scan
                    );
                    if (built != null)
                    {
                        node.Children.Add(built);
                    }
                }
            }
            else
            {
                // The depth cutoff, not decoration: this node has children the walk did not look at,
                // so it says nothing yet. Pruning it here would empty whole canvases whose top
                // layers are bare containers, for a screen that is fully drawn.
                node.More = HasChildren(transform, visibleOnly);
            }

            // Decoration: a frame, image or empty container that says nothing and holds nothing.
            return node.Speaks ? node : null;
        }

        private static bool HasChildren(Transform transform, bool visibleOnly)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                if (!visibleOnly || Visible(transform.GetChild(i), true))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Visible(Transform transform, bool ancestorsVisible)
        {
            try
            {
                if (
                    transform == null
                    || !ancestorsVisible
                    || !transform.gameObject.activeInHierarchy
                )
                {
                    return false;
                }

                CanvasGroup group = transform.gameObject.GetComponent<CanvasGroup>();
                return group == null || group.alpha > 0.001f;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool IsControl(Component component)
        {
            return component is UIButton
                || component is UIToggle
                || component is UISlider
                || component is UITextMeshDropdown
                || component is UIDropdown
                || component is UITextMeshInputField
                || component is UITimeInputField
                || component is UIInputField;
        }

        private static bool IsText(Component component)
        {
            return component is UITextMesh
                || component is TMP_Text
                || component is UnityEngine.UI.Text
                || component is UIText;
        }

        private static string Kind(
            Component control,
            Component textComponent,
            Canvas canvas,
            bool hasImage
        )
        {
            if (control != null)
            {
                if (control is UIButton)
                {
                    return "button";
                }

                if (control is UIToggle)
                {
                    return "toggle";
                }

                if (control is UISlider)
                {
                    return "slider";
                }

                if (control is UITextMeshDropdown || control is UIDropdown)
                {
                    return "dropdown";
                }

                return "input";
            }

            if (textComponent != null)
            {
                return "text";
            }

            if (canvas != null)
            {
                return "canvas";
            }

            return hasImage ? "image" : "panel";
        }

        // The caption a node shows. A control that carries no text component of its own still has
        // one: the framework keeps a button's label on the button.
        private static string ReadText(Component textComponent, Component control)
        {
            string text = ReadTextComponent(textComponent);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            return ReadControlText(control);
        }

        private static string ReadTextComponent(Component component)
        {
            try
            {
                UITextMesh mesh = component as UITextMesh;
                if (mesh != null)
                {
                    return UITextMeshTextUtility.GetEffectiveText(mesh);
                }

                TMP_Text tmp = component as TMP_Text;
                if (tmp != null)
                {
                    return tmp.text;
                }

                UnityEngine.UI.Text legacy = component as UnityEngine.UI.Text;
                if (legacy != null)
                {
                    return legacy.text;
                }

                UIText uiText = component as UIText;
                if (uiText != null)
                {
                    return uiText.Text;
                }
            }
            catch (Exception) { }

            return null;
        }

        private static string ReadControlText(Component control)
        {
            try
            {
                UIButton button = control as UIButton;
                if (button != null)
                {
                    return UITextMeshTextUtility.GetEffectiveButtonText(button);
                }

                UIToggle toggle = control as UIToggle;
                if (toggle != null)
                {
                    // Through the text mesh, because a hot reload can leave the toggle's own Text
                    // reading empty while the mesh still holds the caption.
                    UITextMesh caption = toggle.GetTextMesh();
                    return caption != null
                        ? UITextMeshTextUtility.GetEffectiveText(caption)
                        : toggle.Text;
                }

                UISlider slider = control as UISlider;
                if (slider != null)
                {
                    return slider.Text;
                }

                UITextMeshDropdown dropdown = control as UITextMeshDropdown;
                if (dropdown != null)
                {
                    return dropdown.Text;
                }

                UIDropdown legacyDropdown = control as UIDropdown;
                if (legacyDropdown != null)
                {
                    return legacyDropdown.Text;
                }

                // An input field's own Text is its label; what the player typed is the value.
                UIText labelled = control as UIText;
                if (labelled != null)
                {
                    return labelled.Text;
                }
            }
            catch (Exception) { }

            return null;
        }

        /// <summary>The control's state as one readable string.</summary>
        private static string ReadValue(Component control)
        {
            try
            {
                UIToggle toggle = control as UIToggle;
                if (toggle != null)
                {
                    return toggle.ToggleValue ? "on" : "off";
                }

                UISlider slider = control as UISlider;
                if (slider != null)
                {
                    return Number(slider.SliderValue)
                        + " of "
                        + Number(slider.SliderMinValue)
                        + ".."
                        + Number(slider.SliderMaxValue);
                }

                UITextMeshDropdown dropdown = control as UITextMeshDropdown;
                if (dropdown != null)
                {
                    // The framework's own Text is the row's caption and is often empty; the option
                    // the player picked lives on the TMP dropdown the control wraps.
                    return SelectedOption(dropdown)
                        + " ("
                        + (dropdown.DropdownValue + 1)
                        + " of "
                        + dropdown.DropdownValueCount
                        + ")";
                }

                UIDropdown legacyDropdown = control as UIDropdown;
                if (legacyDropdown != null)
                {
                    return "option " + (legacyDropdown.DropdownValue + 1);
                }

                UITextMeshInputField input = control as UITextMeshInputField;
                if (input != null)
                {
                    return input.InputFieldValue;
                }

                UITimeInputField time = control as UITimeInputField;
                if (time != null)
                {
                    return time.MinutesValue + ":" + time.SecondsValue;
                }

                UIInputField legacyInput = control as UIInputField;
                if (legacyInput != null)
                {
                    return legacyInput.InputFieldValue;
                }
            }
            catch (Exception) { }

            return null;
        }

        private static string SelectedOption(UITextMeshDropdown dropdown)
        {
            try
            {
                TMP_Dropdown selectable = dropdown.GetSelectable() as TMP_Dropdown;
                if (
                    selectable != null
                    && selectable.value >= 0
                    && selectable.value < selectable.options.Count
                )
                {
                    return selectable.options[selectable.value].text;
                }
            }
            catch (Exception) { }

            return dropdown.Text;
        }

        // The tooltip the game would show on hover, read without hovering: the framework populates
        // tooltip content at bind time, and this is the same ITooltipable.GetDetails read the mod
        // already uses at draw time. The game appends "why this is disabled" into the same string,
        // so this is free narration for a disabled control.
        private static string ReadTooltip(Transform transform, ILocalizationHandler localization)
        {
            try
            {
                IReadOnlyList<string> lines = NativeTooltipUtility.GetTooltipLinesForComponent(
                    transform,
                    localization
                );
                if (lines == null || lines.Count == 0)
                {
                    return null;
                }

                StringBuilder text = new StringBuilder();
                for (int i = 0; i < lines.Count && i < MaxTooltipLines; i++)
                {
                    if (string.IsNullOrEmpty(lines[i]))
                    {
                        continue;
                    }

                    if (text.Length > 0)
                    {
                        text.Append(" / ");
                    }

                    text.Append(lines[i]);
                }

                return Clean(text.ToString());
            }
            catch (Exception)
            {
                return null;
            }
        }

        // The uGUI Selectable behind a control is the honest answer: IsInteractable() folds in the
        // CanvasGroups above it, which the framework's own Interactable properties do not.
        private static bool ControlInteractable(Component control)
        {
            try
            {
                IUISelectableHolder holder = control as IUISelectableHolder;
                if (holder != null)
                {
                    UnityEngine.UI.Selectable selectable = holder.GetSelectable();
                    if (selectable != null)
                    {
                        return selectable.IsInteractable();
                    }
                }

                UIDropdown dropdown = control as UIDropdown;
                if (dropdown != null)
                {
                    return dropdown.Interactable;
                }

                UIInputField input = control as UIInputField;
                if (input != null)
                {
                    return input.Interactable;
                }

                UITimeInputField time = control as UITimeInputField;
                if (time != null)
                {
                    return time.Interactable;
                }
            }
            catch (Exception) { }

            return true;
        }

        private static bool LayerInteractable(UISelectionLayer layer)
        {
            try
            {
                return layer.Interactable;
            }
            catch (Exception)
            {
                return true;
            }
        }

        // Screen pixels with a top-left origin, so crop-shot.ps1 can crop straight from the answer.
        private static void ReadRect(Transform transform, Canvas canvas, Node node)
        {
            try
            {
                RectTransform rect = transform as RectTransform;
                if (rect == null)
                {
                    return;
                }

                Camera camera =
                    canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay
                        ? null
                        : canvas.worldCamera;
                Vector3[] corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                float left = float.MaxValue;
                float bottom = float.MaxValue;
                float right = float.MinValue;
                float top = float.MinValue;
                for (int i = 0; i < corners.Length; i++)
                {
                    Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corners[i]);
                    left = Mathf.Min(left, point.x);
                    right = Mathf.Max(right, point.x);
                    bottom = Mathf.Min(bottom, point.y);
                    top = Mathf.Max(top, point.y);
                }

                node.HasRect = true;
                node.X = Mathf.RoundToInt(left);
                node.Y = Mathf.RoundToInt(UnityEngine.Screen.height - top);
                node.Width = Mathf.RoundToInt(right - left);
                node.Height = Mathf.RoundToInt(top - bottom);
            }
            catch (Exception) { }
        }

        private static Canvas CanvasAbove(Transform transform)
        {
            try
            {
                return transform == null ? null : transform.GetComponentInParent<Canvas>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string Number(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        // What the player reads: the game's rich-text markup taken off and the line breaks the
        // layout needs collapsed, so one node is one readable string.
        private static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            StringBuilder stripped = new StringBuilder(text.Length);
            int index = 0;
            while (index < text.Length)
            {
                if (text[index] == '<')
                {
                    int close = text.IndexOf('>', index + 1);
                    if (close > index && IsMarkupTag(text, index + 1, close))
                    {
                        index = close + 1;
                        continue;
                    }
                }

                stripped.Append(text[index]);
                index++;
            }

            StringBuilder collapsed = new StringBuilder(stripped.Length);
            bool space = false;
            for (int i = 0; i < stripped.Length; i++)
            {
                char character = stripped[i];
                if (char.IsWhiteSpace(character))
                {
                    space = collapsed.Length > 0;
                    continue;
                }

                if (space)
                {
                    collapsed.Append(' ');
                    space = false;
                }

                collapsed.Append(character);
            }

            string result = collapsed.ToString();
            if (result.Length == 0)
            {
                return null;
            }

            return result.Length > MaxTextLength
                ? result.Substring(0, MaxTextLength) + "..."
                : result;
        }

        // "<b>" and "</color>" are markup; the "<" in "3 < 4" is not.
        private static bool IsMarkupTag(string text, int start, int end)
        {
            if (end - start < 1 || end - start > 96)
            {
                return false;
            }

            char first = text[start];
            if (first == '/')
            {
                start++;
                if (start >= end)
                {
                    return false;
                }

                first = text[start];
            }

            if (!char.IsLetter(first) && first != '#')
            {
                return false;
            }

            for (int i = start; i < end; i++)
            {
                if (text[i] == '<')
                {
                    return false;
                }
            }

            return true;
        }

        private static string WindowNames(Scan scan)
        {
            StringBuilder names = new StringBuilder();
            foreach (Window window in scan.Windows)
            {
                if (names.Length > 0)
                {
                    names.Append(", ");
                }

                names.Append(window.Name);
            }

            return names.Length == 0 ? "(none)" : names.ToString();
        }

        private static void Flatten(StringBuilder text, Node node, int depth, List<string> fields)
        {
            bool wrote = false;
            foreach (string field in fields)
            {
                string token = Token(node, field);
                if (string.IsNullOrEmpty(token))
                {
                    continue;
                }

                if (!wrote)
                {
                    text.Append(' ', depth * 2);
                    wrote = true;
                }
                else
                {
                    text.Append(" | ");
                }

                text.Append(token);
            }

            if (wrote)
            {
                text.Append('\n');
            }

            foreach (Node child in node.Children)
            {
                Flatten(text, child, depth + 1, fields);
            }
        }

        private static string Token(Node node, string field)
        {
            switch (field)
            {
                case "name":
                    return node.Name;
                case "kind":
                    return node.Kind;
                case "text":
                    return node.Text == null ? null : "text=\"" + node.Text + "\"";
                case "tooltip":
                    return node.Tooltip == null ? null : "tooltip=\"" + node.Tooltip + "\"";
                case "value":
                    return node.Value == null ? null : "value=\"" + node.Value + "\"";
                case "interactable":
                    return node.HasControl
                        ? "interactable=" + (node.Interactable ? "true" : "false")
                        : null;
                case "visible":
                    return node.Visible ? null : "visible=false";
                case "rect":
                    return node.HasRect
                        ? "rect=["
                            + node.X
                            + ","
                            + node.Y
                            + ","
                            + node.Width
                            + ","
                            + node.Height
                            + "]"
                        : null;
                case "more":
                    return node.More ? "more" : null;
                default:
                    return null;
            }
        }

        private static int Write(JsonTextWriter json, Node node)
        {
            json.WriteStartObject();
            json.WritePropertyName("name");
            json.WriteValue(node.Name);
            json.WritePropertyName("kind");
            json.WriteValue(node.Kind);
            if (node.Text != null)
            {
                json.WritePropertyName("text");
                json.WriteValue(node.Text);
            }

            if (node.Tooltip != null)
            {
                json.WritePropertyName("tooltip");
                json.WriteValue(node.Tooltip);
            }

            if (node.Value != null)
            {
                json.WritePropertyName("value");
                json.WriteValue(node.Value);
            }

            if (node.HasControl)
            {
                json.WritePropertyName("interactable");
                json.WriteValue(node.Interactable);
            }

            if (!node.Visible)
            {
                json.WritePropertyName("visible");
                json.WriteValue(false);
            }

            if (node.HasRect)
            {
                json.WritePropertyName("rect");
                json.WriteStartArray();
                json.WriteValue(node.X);
                json.WriteValue(node.Y);
                json.WriteValue(node.Width);
                json.WriteValue(node.Height);
                json.WriteEndArray();
            }

            if (node.More)
            {
                json.WritePropertyName("more");
                json.WriteValue(true);
            }

            int written = 1;
            if (node.Children.Count > 0)
            {
                json.WritePropertyName("children");
                json.WriteStartArray();
                foreach (Node child in node.Children)
                {
                    written += Write(json, child);
                }

                json.WriteEndArray();
            }

            json.WriteEndObject();
            return written;
        }
    }
}
