using System.Collections.Generic;
using System.Text;
using System;
using SongsOfConquest.Client;
using SongsOfConquest.Client.InputManagement;
using SongsOfConquest.Client.Menu.Tooltip;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Details;
using SongsOfConquest.Common.Economy;
using SongsOfConquest.Common.Localization;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace SongsOfConquestAccess.Adapters
{
    // Native tooltip/details objects do not expose their contents as plain text.
    // They reveal content by drawing into an IDetailsDrawer via IDetails.Draw(...).
    // This class is a text-capturing fake drawer: text-producing draw calls become
    // speech/buffer lines, while visual-only draw calls are implemented as safe
    // no-ops so native details builders can run without needing per-details parsers.
    public sealed class DetailsTextUtility : IDetailsDrawer
    {
        // Native details builders often mutate the element returned from AddText/AddImage,
        // for example drawer.AddText(...).FontColor = .... Return a no-op element so
        // those builders can run while this drawer captures only text.
        private static readonly NullDetailsElement NullElement = new NullDetailsElement();
        private readonly List<string> _parts = new List<string>();
        private readonly List<TooltipInstructionRow> _instructionRows = new List<TooltipInstructionRow>();
        private ILocalizationHandler _localization;
        private ResourceType? _pendingResourceIconType;

        public IEnumerable<DetailsSidePanelDescription> AllSidePanels
        {
            get { return new DetailsSidePanelDescription[0]; }
        }

        public static string ToText(IDetails details, ILocalizationHandler localization)
        {
            IReadOnlyList<string> parts = ToLines(details, localization);
            return JoinSentences(parts);
        }

        public static IReadOnlyList<string> ToLines(IDetails details, ILocalizationHandler localization)
        {
            return Capture(details, localization).TextLines;
        }

        public static DetailsTextUtility Capture(IDetails details, ILocalizationHandler localization)
        {
            if (details == null || localization == null)
            {
                return new DetailsTextUtility();
            }

            // Native details may include instruction rows from AddLabelWithImage
            // or AddLabelsWithInputTypes. Generic extraction preserves those
            // rows because they are ambiguous: some are status messages, while
            // others describe real actions. Adapters that understand a specific
            // tooltip source may replace known action instruction rows with
            // structured TooltipAction entries. Unknown rows remain in TextLines
            // so unsupported actions are still spoken.
            DetailsTextUtility drawer = new DetailsTextUtility();
            drawer._localization = localization;
            try
            {
                details.Draw(drawer, localization);
            }
            catch (System.Exception ex)
            {
                SocAccessMod.Instance?.LogWarning("DetailsTextUtility failed to draw details: " + ex.Message);
            }

            return drawer;
        }

        public IReadOnlyList<string> TextLines
        {
            get { return _parts.ToArray(); }
        }

        // Native tooltip instruction rows are draw-time metadata. They are not
        // accessibility actions by themselves; an adapter that understands the
        // native source must decide whether an instruction can be invoked safely.
        public IReadOnlyList<TooltipInstructionRow> InstructionRows
        {
            get { return _instructionRows.ToArray(); }
        }

        public void RegisterSidePanelDescription(DetailsSidePanelDescription description)
        {
            Add(description.Title);
            Add(description.Description);
        }

        public IUITextMesh AddText(string text, FontType type = FontType.LabelSmall, HorizontalAlignment horizontalAlignment = HorizontalAlignment.Center, VerticalAlignment verticalAlignment = VerticalAlignment.Middle, FontColor fontColor = FontColor.White)
        {
            Add(text);
            return NullElement;
        }

        public IUIImage AddImage(Sprite icon)
        {
            return NullElement;
        }

        public IUIImage AddImage(Sprite icon, int maxSize)
        {
            return NullElement;
        }

        public Sprite GetResourceIcon(int resourceType, bool largeIcon = true)
        {
            if (Enum.IsDefined(typeof(ResourceType), resourceType))
            {
                _pendingResourceIconType = (ResourceType)resourceType;
            }
            else
            {
                _pendingResourceIconType = null;
            }

            return null;
        }

        public void AddHeaderDivider(string text, string subHeaderText, Color backgroundColor, FontType type = FontType.TitleSmall, FontType subHeaderType = FontType.LabelSmall, HorizontalAlignment horizontalAlignment = HorizontalAlignment.Center, VerticalAlignment verticalAlignment = VerticalAlignment.Middle)
        {
            Add(text);
            Add(subHeaderText);
        }

        public void AddUpgradeLevel(string label, int current, int max)
        {
            Add(label + " " + current + " of " + max);
        }

        public void AddDualHeaderWithBackground(string leftText, string rightText)
        {
            AddRow(leftText, rightText);
        }

        public void AddHeader(string header)
        {
            Add(header);
        }

        public void AddTextWithBackground(string leftText, string rightText)
        {
            AddRow(leftText, rightText);
        }

        public void AddImageDivider()
        {
        }

        public void AddSpace(DetailsEmptySpace.DetailsSpaceSize spaceSize)
        {
        }

        public void AddTextWithVerticalDivider(string leftTopText, string leftBottomText, string rightTopText, string rightBottomText)
        {
            Add(leftTopText);
            Add(leftBottomText);
            Add(rightTopText);
            Add(rightBottomText);
        }

        public void AddTextWithDivider(string symbol, string text)
        {
            string left = symbol ?? string.Empty;
            string right = text ?? string.Empty;
            // Research stack tooltips use this for tier headings, passing
            // "TIER" and "1" separately. Speech should expose that visual row
            // as one review line, e.g. "TIER 1", not two unrelated lines.
            if (!string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right))
            {
                Add(left.Trim() + " " + right.Trim());
                return;
            }

            Add(left);
            Add(right);
        }

        public (IUITextMesh headerText, IUITextMesh descriptionText) AddTextWithHeader(string header, string details)
        {
            AddRow(header, details);
            return (NullElement, NullElement);
        }

        public void AddLabelWithImage(string text, InputType type)
        {
            AddInstruction(text, type);
        }

        public void AddFrameTopHex(Sprite icon)
        {
        }

        public void AddFrameTopCircle(Sprite icon)
        {
        }

        public void AddSpellHeader(string header, string subHeader)
        {
            Add(header);
            Add(subHeader);
        }

        public void AddTextLeftRight(string leftText, string rightText)
        {
            AddRow(leftText, rightText);
        }

        public IUITextMesh AddSingleTextWithBackground(string text)
        {
            Add(text);
            return NullElement;
        }

        public void AddEntry(Sprite icon, string text, string value, bool showBackground)
        {
            ResourceType? resourceType = _pendingResourceIconType;
            _pendingResourceIconType = null;
            if (resourceType.HasValue)
            {
                AddResourceRow(resourceType.Value, text, value);
                return;
            }

            AddRow(text, value);
        }

        public void AddEntryWithReference(AssetReferenceT<Sprite> iconReference, string text, string value, bool showBackground)
        {
            AddRow(text, value);
        }

        public void AddBottomFade(Color color)
        {
        }

        public void AddLabelsWithInputTypes(string inputLabel, InputType inputType, string secondaryInputLabel = null, InputType secondaryInputType = default(InputType), string thirdInputLabel = null, InputType thirdInputType = default(InputType), bool addSpaceIfDrawn = true, int? separatorSpaces = null)
        {
            AddInstruction(inputLabel, inputType);
            AddInstruction(secondaryInputLabel, secondaryInputType);
            AddInstruction(thirdInputLabel, thirdInputType);
        }

        public void BeginHorizontal()
        {
        }

        public void EndHorizontal()
        {
        }

        public void StartBuild(IUITransform transform, IUITransform iconParent)
        {
        }

        public void Clear()
        {
            _parts.Clear();
        }

        public float CalculateTextContentWidth()
        {
            return 0f;
        }

        public Sprite GetUpgradeIcon()
        {
            return null;
        }

        public Sprite GetDefenceIcon()
        {
            return null;
        }

        public Sprite GetBuildingInProgressIcon()
        {
            return null;
        }

        public Sprite GetTroopIcon()
        {
            return null;
        }

        public Sprite GetPillageIcon()
        {
            return null;
        }

        private void Add(string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                _parts.Add(value);
            }
        }

        private void AddRow(string leftText, string rightText)
        {
            string left = leftText ?? string.Empty;
            string right = rightText ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right))
            {
                Add(left.TrimEnd(':') + ": " + right);
            }
            else
            {
                Add(left);
                Add(right);
            }
        }

        private void AddResourceRow(ResourceType resourceType, string text, string value)
        {
            string amount = SongsOfConquestAccess.Speech.SpeechTextSanitizer.Normalize(text);
            string right = SongsOfConquestAccess.Speech.SpeechTextSanitizer.Normalize(value);
            string resourceName = GetResourceName(resourceType, amount);
            string row = amount;
            if (!string.IsNullOrWhiteSpace(resourceName) && !ContainsResourceName(amount, resourceName))
            {
                row = string.IsNullOrWhiteSpace(amount) ? resourceName : amount + " " + resourceName;
            }

            if (!string.IsNullOrWhiteSpace(right))
            {
                row = string.IsNullOrWhiteSpace(row) ? right : row + ": " + right;
            }

            if (string.IsNullOrWhiteSpace(row))
            {
                return;
            }

            int previousIndex = _parts.Count - 1;
            if (previousIndex >= 0 && IsCostHeader(_parts[previousIndex]))
            {
                _parts[previousIndex] = _parts[previousIndex].TrimEnd(':') + ": " + row;
                return;
            }

            if (previousIndex >= 0 && IsCostLine(_parts[previousIndex]))
            {
                _parts[previousIndex] = _parts[previousIndex] + ", " + row;
                return;
            }

            Add(row);
        }

        private string GetResourceName(ResourceType resourceType, string amountText)
        {
            if (_localization == null)
            {
                return string.Empty;
            }

            int amount;
            string key = "Common/Resource/" + resourceType;
            if (int.TryParse(SongsOfConquestAccess.Speech.SpeechTextSanitizer.Normalize(amountText), out amount))
            {
                return SongsOfConquestAccess.Speech.SpeechTextSanitizer.Normalize(_localization.GetPluralText(key, amount));
            }

            return SongsOfConquestAccess.Speech.SpeechTextSanitizer.Normalize(_localization.GetPluralTextGeneric(key));
        }

        private static bool ContainsResourceName(string text, string resourceName)
        {
            return !string.IsNullOrWhiteSpace(text)
                && !string.IsNullOrWhiteSpace(resourceName)
                && text.IndexOf(resourceName, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool IsCostHeader(string text)
        {
            string normalized = SongsOfConquestAccess.Speech.SpeechTextSanitizer.Normalize(text).Trim().TrimEnd(':');
            string costHeader = SongsOfConquestAccess.Speech.SpeechTextSanitizer.Normalize(GetCostHeader()).Trim().TrimEnd(':');
            return normalized.Equals(costHeader, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsCostLine(string text)
        {
            string normalized = SongsOfConquestAccess.Speech.SpeechTextSanitizer.Normalize(text).Trim();
            string costHeader = SongsOfConquestAccess.Speech.SpeechTextSanitizer.Normalize(GetCostHeader()).Trim().TrimEnd(':');
            return normalized.StartsWith(costHeader + ": ", StringComparison.OrdinalIgnoreCase);
        }

        private string GetCostHeader()
        {
            return _localization != null ? _localization.GetText("Adventure/BuildMenu/Cost") : "Cost";
        }

        private void AddInstruction(string text, InputType inputType)
        {
            Add(text);
            if (!string.IsNullOrWhiteSpace(text))
            {
                _instructionRows.Add(new TooltipInstructionRow(text, inputType));
            }
        }

        private static string JoinSentences(IReadOnlyList<string> parts)
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < parts.Count; i++)
            {
                string part = SongsOfConquestAccess.Speech.SpeechTextSanitizer.Normalize(parts[i]);
                if (string.IsNullOrWhiteSpace(part))
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    char previous = builder[builder.Length - 1];
                    builder.Append(IsTerminalPunctuation(previous) ? " " : ". ");
                }

                builder.Append(part);
            }

            return builder.ToString();
        }

        private static bool IsTerminalPunctuation(char value)
        {
            return value == '.'
                || value == '!'
                || value == '?'
                || value == ':'
                || value == ';';
        }

        private sealed class NullDetailsElement : IUITextMesh, IUIImage
        {
            public bool Active { get; set; }
            public Action<string> OnLinkClicked { get; set; }
            public string Text { get; set; }
            public FontColor FontColor { get; set; }
            public FontStyle FontStyle { get; set; }
            public FontType FontType { get; set; }
            public HorizontalAlignment HorizontalAlignment { get; set; }
            public VerticalAlignment VerticalAlignment { get; set; }
            public bool WordWrapping { get; set; }
            public Overflow Overflow { get; set; }
            public bool Autosize { get; set; }
            public bool RaycastTarget { get; set; }
            public Bounds TextBounds { get { return default(Bounds); } }
            public Sprite Sprite { get; set; }
            public Material Material { get; set; }
            public Color SpriteColor { get; set; }
            public float FillAmount { get; set; }
            public bool PreserveAspect { get; set; }
            public bool DraggingEnabled { get; set; }
            public Action<IUITransform> OnDragged { get; set; }
            public Action<IUITransform> OnDropped { get; set; }
            public Action OnDraggedOutside { get; set; }
            public Action<Vector2> OnDraggedInside { get; set; }
            public Action<Vector2> OnClickedInside { get; set; }
            public Action OnClickedOutside { get; set; }
            public float Alpha { get; set; }
            public Vector2 AnchorMin { get; set; }
            public Vector2 AnchorMax { get; set; }
            public Vector2 OffsetMin { get; set; }
            public Vector2 OffsetMax { get; set; }
            public Vector2 SizeDelta { get; set; }
            public Vector2 Pivot { get; set; }
            public Vector2 AnchoredPosition { get; set; }
            public Rect Rectangle { get { return default(Rect); } }
            public IUITransform Parent { get; set; }
            public TooltipDescription Tooltip { get; set; }
            public Action<IUITransform> OnTooltipShownEvent { get; set; }
            public Action<IUITransform> OnTooltipHiddenEvent { get; set; }
            public Action OnWasDestroyed { get; set; }
            public string Name { get; set; }
            public Transform MonoTransform { get { return null; } }
            public Vector3 LocalPosition { get; set; }
            public Vector3 Position { get; set; }
            public Vector3 Size { get; set; }
            public Vector3 Scale { get; set; }
            public Quaternion Rotation { get; set; }
            public Quaternion LocalRotation { get; set; }
            public Vector3 Forward { get { return Vector3.forward; } }
            public int ChildCount { get { return 0; } }

            ITransform ITransform.Parent
            {
                get { return Parent; }
                set { Parent = value as IUITransform; }
            }

            public void SetText(StringBuilder stringBuilder)
            {
                Text = stringBuilder != null ? stringBuilder.ToString() : string.Empty;
            }

            public void ClearText()
            {
                Text = string.Empty;
            }

            public void AppendText(string text)
            {
                Text = (Text ?? string.Empty) + (text ?? string.Empty);
            }

            public void AppendTextWithLink(string text, string linkId)
            {
                AppendText(text);
            }

            public void AppendTextWithTooltip(string text, string linkId, TooltipDescription tooltip)
            {
                AppendText(text);
            }

            public void AppendNewLine()
            {
                AppendText("\n");
            }

            public void AppendStats(string text, object stats)
            {
                AppendText(text);
            }

            public void AppendVerticalSpacing()
            {
            }

            public void SetTooltip(string linkId, TooltipDescription tooltip)
            {
                Tooltip = tooltip;
            }

            public void ForceMeshUpdate(bool ignoreActiveState = false, bool forceTextReparsing = false)
            {
            }

            public void SetNativeSize()
            {
            }

            public void SetInputIcon(ActionReference? inputActionId)
            {
            }

            public void RebuildLayout()
            {
            }

            public void ClampToScreen()
            {
            }

            public void SetTooltip(string header, string text)
            {
            }

            public void SetTooltip(string text)
            {
            }

            public void SetDetails(IDetails details)
            {
            }

            public Canvas GetCanvas()
            {
                return null;
            }

            public bool IsPointerInsideRect()
            {
                return false;
            }

            public void MakeLastSibling()
            {
            }

            public void MakeFirstSibling()
            {
            }

            public void SetSiblingIndex(int index)
            {
            }

            public void Recolor(ColorCollection collection, bool includeChildren = true)
            {
            }

            public void Recolor(Color primary, Color secondary, Color tertiary, bool includeChildren = true)
            {
            }

            public void Destroy()
            {
            }

            public void DestroyAllChildren(bool onlyDestroyActive = false)
            {
            }

            public ITransform Instantiate(ITransform parent = null)
            {
                return this;
            }

            public T GetComponent<T>()
            {
                return default(T);
            }

            public T GetComponentInChildren<T>()
            {
                return default(T);
            }
        }
    }

    public sealed class TooltipInstructionRow
    {
        public TooltipInstructionRow(string text, InputType inputType)
        {
            Text = text ?? string.Empty;
            InputType = inputType;
        }

        public string Text { get; private set; }

        public InputType InputType { get; private set; }
    }
}
