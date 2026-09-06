using System;
using System.Collections.Generic;

namespace SongsOfConquestAccess.UI.Graph
{
    /// <summary>The four navigable directions between graph nodes (explicit edges). Tab-stop cycling and
    /// region jumps are OPERATIONS over node metadata (<see cref="GraphNode.StopKey"/> /
    /// <see cref="GraphNode.RegionKey"/>), not edges — they carry per-stop remembered positions, which a
    /// static edge can't express.</summary>
    public enum GraphDir
    {
        Up,
        Right,
        Down,
        Left,
    }

    /// <summary>The well-known announcement-part kinds. A part's kind is its identity for control-type
    /// ordering, node-over-type overriding, and the user's per-kind announcement settings.</summary>
    public static class AnnouncementKinds
    {
        public const string Label = "label";
        public const string Role = "role";
        public const string Value = "value";
        public const string Selected = "selected";
        public const string Enabled = "enabled";
        public const string Tooltip = "tooltip";
        public const string Position = "position";

        /// <summary>The control's USAGE HINTS (<see cref="NodeHint"/>). Deliberately absent from every
        /// control type's kind order, which is what puts it in the trailing bucket: a hint is the last
        /// thing said about a control, after the position and after the words a control adds about
        /// itself with no kind at all ("draggable"), because it is about the keyboard rather than about
        /// the thing.</summary>
        public const string Hint = "hint";
    }

    /// <summary>
    /// One part of a control's spoken focus readout ("Hold position" / "toggle" / "on"), resolved live at
    /// speak time. A LIVE part is additionally watched while its node is focused: when its resolved text
    /// changes (an async toggle settling, a value the game flips), the navigator speaks just that part
    /// immediately — state feedback without re-reading the whole control, and without per-element watcher
    /// machinery.
    /// </summary>
    public sealed class NodeAnnouncement
    {
        /// <summary>The part's text, resolved live. Null/empty at speak time = the part stays silent.</summary>
        public Func<string> Text;

        /// <summary>Watch this part while the node is focused and speak it when its value changes.</summary>
        public bool Live;

        /// <summary>The part's kind (<see cref="AnnouncementKinds"/>), or null for a custom one-off part.
        /// Kinds drive the control type's speak order, let a node's part override the type's common part
        /// of the same kind, and key the user's per-kind announcement settings.</summary>
        public string Kind;

        public NodeAnnouncement(Func<string> text, bool live = false, string kind = null)
        {
            Text = text;
            Live = live;
            Kind = kind;
        }

        public static NodeAnnouncement Static(string text)
        {
            return new NodeAnnouncement(() => text);
        }
    }

    /// <summary>
    /// A CONTROL TYPE — "button", "toggle", "slider" — as a registry VALUE rather than a C# class. Deriving
    /// type identity from proxy/wrapper classes forces attribute unions and class collapsing whenever two
    /// widgets should share one settings identity; a value lets a node factory just point at the type. A
    /// type owns the speak ORDER of its announcement kinds and the parts COMMON to every control of the
    /// type (the localized role word); nodes contribute their specific parts, overriding a common part of
    /// the same kind. The user's per-type announcement settings key off <see cref="Key"/>.
    /// </summary>
    public sealed class ControlType
    {
        /// <summary>Stable settings/registry key ("button", "toggle", "slider").</summary>
        public string Key;

        /// <summary>The announcement kinds in speak order; parts with unknown/absent kinds append after,
        /// in declaration order.</summary>
        public string[] Order;

        /// <summary>The parts every control of this type shares (the role word), resolved per compose.
        /// Null = none.</summary>
        public Func<IList<NodeAnnouncement>> Common;
    }

    /// <summary>
    /// One block of a control's readable content, declared ONCE and surfaced everywhere it belongs.
    ///
    /// A control's content reaches the player through two channels — the focus readout and the review
    /// buffer — and wiring them separately is how a row comes to announce a tooltip it cannot review, or
    /// to review one it never mentions. It happened three times on the new-game screens before this type
    /// existed. So a section says WHAT the lines are and HOW LOUD they should be, and the engine derives
    /// both surfaces from that one declaration: every section feeds the buffer, in declared order, and
    /// <see cref="Mode"/> alone decides what (if anything) the focus readout says about it.
    ///
    /// Sections are ordered as the screen draws them (a row's heading tooltip before its value's), which
    /// is the order the buffer reads them in.
    /// </summary>
    public sealed class NodeSection
    {
        /// <summary>The block's lines, resolved live at read time — a refusing button's reason has to be
        /// the one it would give now. Null or an empty list = the section contributes nothing.</summary>
        public Func<IList<string>> Lines;

        /// <summary>How the section reaches the focus readout. Only
        /// <see cref="TooltipMode.Announce"/> puts anything there; <see cref="TooltipMode.None"/>
        /// (content the control DRAWS - a planet card's output rows, a chart's series) and
        /// <see cref="TooltipMode.Indicate"/> (a dossier the game assembles on hover) are both
        /// buffer-only, because the readout already named the control and the substance is there to
        /// be walked.
        ///
        /// Read-only from outside, and set from nowhere a screen can reach: for a section read off a
        /// game tooltip the mode is the TOOLTIP'S OWN KIND (<c>GraphNodes.ModeFor</c>) and a screen
        /// that could say otherwise is a screen that can get it wrong.</summary>
        public readonly TooltipMode Mode;

        /// <summary>Whether there is really a tooltip here, asked EVERY frame. Only consulted for
        /// <see cref="TooltipMode.Indicate"/>, and null - the default - means "always", which is the
        /// right answer for a section the mod itself invented and therefore knows is real.
        ///
        /// It exists for the sections read off a game widget, where "the control has a tooltip" and
        /// "the game would draw one" are different questions: a prefab hangs an empty tooltip on a
        /// widget it has nothing to say about, and aiming the focus pointer at one parks the game's
        /// own tooltip countdown for good. The question is asked per frame rather than when the node
        /// is declared because a tooltip the game fills in later has to start counting the moment it
        /// becomes real. Nothing SPEAKS off this any more - the pointer and the tooltip-parity audit
        /// are its two readers.
        ///
        /// Note what this is NOT: a check that the section's LINES resolve to something. A tooltip the
        /// renderer assembles has no words until it is drawn, which is well after the readout that
        /// mentions it is composed, so asking for lines answers "empty" on exactly the controls that
        /// most need indicating.</summary>
        public Func<bool> Indicates;

        /// <summary>Whether these lines came off a TOOLTIP - the thing a hover would raise - rather
        /// than being words the mod composed about the control itself.
        ///
        /// It exists because the two are counted differently when the readout is composed
        /// (<see cref="TooltipParts"/>): a control points at ONE tooltip, so at most one tooltip's
        /// lines are spoken, while composed words are the control's own and every one of them is.
        /// Internal, because it is a fact about which door built the section rather than anything a
        /// screen decides.</summary>
        public readonly bool FromTooltip;

        /// <summary>WHICH tooltip these lines came off - the hover surface itself, held as an opaque
        /// reference because this layer has no idea what a tooltip is.
        ///
        /// <see cref="FromTooltip"/> answers "did a tooltip write this", which is what the readout
        /// needs; this answers "which one", which is what a node's ONE-tooltip rule needs
        /// (<see cref="OneTooltipRule"/>). The two are different questions because one tooltip can
        /// legitimately produce SEVERAL sections, and counting sections would refuse that while
        /// letting two genuinely different hover surfaces through.
        ///
        /// Null for a section the mod composed, and for a caller that read a tooltip's words without
        /// naming the tooltip; an unnamed source is never counted, so the rule can only ever
        /// under-report.</summary>
        public readonly object Source;

        /// <summary>
        /// THE WORDS THIS SECTION SPEAKS WHEN ITS OWN ARE NOT WRITTEN YET, and null - the ordinary
        /// case - when what it speaks is settled by <see cref="Mode"/> alone.
        ///
        /// A tooltip the game ASSEMBLES on hover has no words until the tooltip window draws it,
        /// several frames after the readout that would have said them. So a player who has asked for
        /// those tooltips to be read (the mod's own "announce long tooltips" setting) cannot be
        /// served by the readout at all: the door hands the section a reader that answers nothing
        /// until the game has drawn, and the FIRST drawn words from then on
        /// (<c>ES2Access.UI.LongTooltips</c>). The part carrying it is therefore LIVE - the
        /// navigator's live watch is what notices the words arriving and says them, after the
        /// readout, once.
        ///
        /// A second reader rather than a swap of <see cref="Lines"/>, because the two surfaces want
        /// different answers: the review buffer wants what the window is drawing NOW, every later
        /// detail included, while the readout wants what was there when it first drew - the window
        /// re-assembles itself a few seconds in, and hearing the whole block again is not what
        /// anybody asked for.
        ///
        /// Not a loudness a screen can name, for the same reason <see cref="Mode"/> is not: it is
        /// set at the one door, off the tooltip's own kind and the player's setting.</summary>
        public readonly Func<IList<string>> Late;

        /// <summary>WHAT THIS CONTROL COSTS, in the words the tooltip's own cost panel would draw,
        /// resolved live - a price moves with every bonus the empire picks up, and a turn count with
        /// every point of industry the system gains.
        ///
        /// Held on the section rather than on the node because the answer is the TOOLTIP'S: the game
        /// draws its cost line for a tooltip CLASS, off the target and context that tooltip carries,
        /// so the control that has a price to say is exactly the control pointing at such a tooltip,
        /// and the price is the one a hover would show. The readout takes it from the section the
        /// node points at (<see cref="TooltipParts.CostPart"/>) and speaks it right after the name.
        ///
        /// Null - the ordinary case - is a tooltip whose class draws no cost panel, and every section
        /// the mod composed itself. Cleared by the door for the few rows that DRAW their own turn
        /// count, where saying it again is saying it twice.</summary>
        public Func<string> Cost;

        private NodeSection(
            Func<IList<string>> lines,
            TooltipMode mode,
            Func<bool> indicates,
            bool fromTooltip,
            object source,
            Func<string> cost,
            Func<IList<string>> late
        )
        {
            Lines = lines;
            Mode = mode;
            Indicates = indicates;
            FromTooltip = fromTooltip;
            Source = source;
            Cost = cost;
            Late = late;
        }

        /// <summary>Content the control draws: reviewable, never spoken on focus.</summary>
        public static NodeSection Buffer(Func<IList<string>> lines)
        {
            return lines == null
                ? null
                : new NodeSection(lines, TooltipMode.None, null, false, null, null, null);
        }

        /// <summary>
        /// Words the MOD composed for a control the game explains nowhere on the screen - a report's
        /// outcome sentence read out of the model, a card the prefab draws as a picture - spoken as
        /// the readout is read.
        ///
        /// The one place a section's loudness is a caller's to decide, and it is decidable precisely
        /// because there is no tooltip behind it to have a kind. Never hand it a tooltip's lines: a
        /// tooltip's announcement is settled by its own class, at <c>GraphNodes.TooltipSection</c>.
        /// </summary>
        public static NodeSection Composed(Func<IList<string>> lines)
        {
            return lines == null
                ? null
                : new NodeSection(lines, TooltipMode.Announce, null, false, null, null, null);
        }

        /// <summary>
        /// A section whose loudness was DERIVED from a tooltip's own class - the door's own
        /// constructor, and the only way a section that is not <see cref="Buffer"/> or
        /// <see cref="Composed"/> comes to exist.
        ///
        /// Kept out of the public surface so that "which tooltips announce" is answered in one place
        /// (<c>GraphNodes.ModeFor</c>) rather than per call site: a screen has no way to name a mode
        /// for a tooltip, which is what makes the wrong reading unrepresentable rather than merely
        /// discouraged.
        ///
        /// <paramref name="source"/> is the tooltip itself (<see cref="Source"/>), so a node can be
        /// asked how many DIFFERENT hover surfaces it declared rather than how many sections it has.
        /// </summary>
        /// <paramref name="cost"/> is what this tooltip's own cost panel would draw
        /// (<see cref="Cost"/>), which is the door's answer too - a cost line is a fact about the
        /// tooltip class, and a screen naming one for itself is a screen that can name the wrong one.
        /// <paramref name="late"/> is what it speaks while its own words are not written yet
        /// (<see cref="Late"/>).
        public static NodeSection Derived(
            Func<IList<string>> lines,
            TooltipMode mode,
            Func<bool> indicates,
            object source = null,
            Func<string> cost = null,
            Func<IList<string>> late = null
        )
        {
            return new NodeSection(lines, mode, indicates, true, source, cost, late);
        }
    }

    /// <summary>
    /// The behaviors of a control, as data. <see cref="Announcements"/> is required (its parts compose the
    /// spoken focus readout; the first part is the control's label for search/dedupe purposes); the rest
    /// are optional — a null slot means the control doesn't have that behavior and the navigator speaks
    /// its "nothing there" feedback instead.
    /// </summary>
    public sealed class NodeVtable
    {
        /// <summary>Required, at least one part. The control's spoken focus readout. Parts marked
        /// <see cref="NodeAnnouncement.Live"/> re-speak on change while focused. When
        /// <see cref="ControlType"/> is set, the type's common parts merge in and the type's kind order
        /// applies; otherwise parts speak in declaration order.
        ///
        /// A node's announcement-part list must keep its SHAPE across rebuilds: the live-part watch
        /// re-baselines when the list changes shape and swallows exactly the change it should have
        /// spoken - represent absent state as an empty part, never a missing one.</summary>
        public IList<NodeAnnouncement> Announcements;

        /// <summary>The control's type (registry value) — supplies the role word, the speak order, and the
        /// per-type announcement settings identity. Null = an untyped one-off.</summary>
        public ControlType ControlType;

        /// <summary>Optional. Primary activation — the left-click equivalent (Enter).</summary>
        public Action OnActivate;

        /// <summary>Optional. Secondary activation — the right-click equivalent.</summary>
        public Action OnSecondary;

        /// <summary>
        /// Optional. This control NAMES A PLACE that already exists elsewhere in the graph, and Right
        /// goes there — a reference-shaped leaf: a lane naming the system at its far end, a link
        /// naming the row it points at, a search result naming the thing it found.
        ///
        /// It exists because the alternative is worse. Such a node could be made a GROUP that
        /// re-declares the named place underneath it, and then every object has two nodes - which
        /// breaks the one-object-one-node rule (reference identity is followed before the structural
        /// key, so the copy either teleports the cursor or has to be keyed structurally and lose its
        /// identity), and the tree has no bottom unless the copy is deliberately made poorer than the
        /// original. Following the reference instead keeps ONE node per place: Right REBASES the
        /// cursor onto it.
        ///
        /// The contract, which the engine holds the caller to:
        /// - Only asked where the node is NOT expandable. A group's own expansion wins - a control
        ///   that has children of its own is not standing in for somewhere else.
        /// - It is NOT a click. Right opens branches all over a tree and a player presses it
        ///   speculatively; a handler that posts an order or confirms a mode the game has armed would
        ///   fire on a keystroke nobody meant as a command.
        /// - The handler MOVES FOCUS itself (through the host's own put-the-cursor-here route) and the
        ///   engine says nothing: <see cref="KeyGraph.TreeMove.Followed"/> is consumed silently, and
        ///   the landing announces itself exactly once, by the one code path every focus change goes
        ///   through. A handler that speaks as well is that landing said twice.
        /// </summary>
        public Action OnFollow;

        /// <summary>Optional. The control's OTHER activation — what the game's own modified click does
        /// (queue this at the head of the queue rather than the end). Distinct from
        /// <see cref="OnSecondary"/>, which is the right-click.</summary>
        public Action OnAlternate;

        /// <summary>Optional. The command the game puts on a RIGHT-CLICK here - the one thing the
        /// control does when the player asks it to do its obvious thing without opening anything.
        /// Distinct from <see cref="OnActivate"/> (the left click) and from <see cref="OnAlternate"/>
        /// (the modified left click); a control without one answers the key with a spoken cue rather
        /// than with silence.</summary>
        public Action OnContextual;

        /// <summary>Optional. GO TO WHERE THIS HAPPENED - the game's own show-location for this row,
        /// exactly as clicking the button its popup would draw. Distinct from every click above: it
        /// moves the VIEW rather than doing anything to the control, and it is only ever wired where
        /// the game itself offers the affordance for this particular thing, so its presence is also
        /// the key's availability (the claim asks for it before the press, and the handler asks
        /// again). A control without one leaves the key alone.</summary>
        public Action OnGoTo;

        /// <summary>Optional. EMPTY THIS CONTROL - take away what it holds without replacing it, on
        /// the controls where the game itself has no such affordance at all (a key-binding field: the
        /// mouse can only clear one by focusing it and clicking away). Like <see cref="OnGoTo"/> its
        /// presence IS the key's availability, asked before the press and again in the handler, so a
        /// control without one leaves the key to the game.</summary>
        public Action OnClear;

        /// <summary>Optional. The command the game puts on a DOUBLE click here - the second click
        /// inside its own double-click window, which several of this game's controls answer with a
        /// command of their own (a fleet row shows that fleet on the map, a picked choice is
        /// confirmed, a module tile fits itself). Distinct from <see cref="OnActivate"/> (the single
        /// click, which such a control may answer with nothing at all), from
        /// <see cref="OnAlternate"/> (the click with a modifier held) and from
        /// <see cref="OnContextual"/> (the right click).</summary>
        public Action OnDoubleClick;

        /// <summary>Optional. Add this control's item to the game's own selection, or take it out
        /// again, leaving the rest of the selection alone - what the game's Ctrl+click does.</summary>
        public Action OnSelectToggle;

        /// <summary>Optional. Extend the game's own selection from wherever it last was to here -
        /// what the game's Shift+click does.</summary>
        public Action OnSelectRange;

        /// <summary>Optional. What this control offers to PICK UP and carry (a ship out of a fleet,
        /// a population unit off a planet). Returning null means it has nothing to give right now.
        /// The carried thing's name is captured at that moment and never re-derived - see
        /// <see cref="CarryItem"/>. A PURE QUERY: the readout asks it speculatively to know whether to
        /// say "draggable" (<c>CarryState.DraggablePart</c>), so it must decide, not act.</summary>
        public Func<CarryItem> OnPickUp;

        /// <summary>Optional. Which kind of cargo this control will TAKE (<see cref="CarryItem.Kind"/>).
        /// Null takes nothing; the kind is what keeps a ship from being dropped into a population
        /// list.</summary>
        public string DropKind;

        /// <summary>Optional. Take the carried thing, through the GAME's own can-do check - never a
        /// rule the mod invented. A refusal carries the game's own words and leaves the player still
        /// holding it.</summary>
        public Func<CarryItem, DropResult> OnDrop;

        /// <summary>Optional. Whether this control would take THIS cargo right now - the screen's own
        /// test for the ones among a family of targets that will refuse (a locked deck slot beside three
        /// live ones, a hull slot the module does not fit). Asked for the spoken drop-target INDICATION
        /// only, so the word and the outcome cannot disagree; the drop itself still goes through
        /// <see cref="OnDrop"/>, whose refusal carries the game's own reason for a player who presses
        /// anyway. Null = <see cref="DropKind"/> alone answers.</summary>
        public Func<CarryItem, bool> DropAccepts;

        /// <summary>Optional. Everything the control has to say beyond its readout, as ordered
        /// <see cref="NodeSection"/>s — its tooltips (the heading's explanation, then the value's
        /// dossier) and whatever else it draws. ONE declaration: the review buffer reads them all in
        /// order under the control's name and state, and the focus readout's tooltip part is derived
        /// from their modes (<see cref="TooltipParts.Part(IList{NodeSection})"/>). Null = the control
        /// has nothing beyond its readout, which is a complete buffer in itself.</summary>
        public IList<NodeSection> Sections;

        /// <summary>
        /// Optional. What the review buffer OPENS with, for a control whose readout deliberately
        /// leaves out a word the buffer needs.
        ///
        /// The buffer's head is normally the readout itself (<see cref="NodeBuffer"/>), and that is
        /// right wherever the readout is the whole of what the control says. A table CELL is the
        /// exception the type exists for: its column's caption is spoken as the EDGE the player
        /// crossed to reach it rather than by the cell, so the cell's readout is the bare value while
        /// the buffer - which nobody arrives at across an edge - needs the caption with it. Declaring
        /// the head is how the caption gets there once: the control's own first content line then
        /// matches it and the head dedupe drops the copy.
        ///
        /// Null - the ordinary case - is the readout.
        /// </summary>
        public Func<string> BufferHead;

        /// <summary>Optional. The USAGE HINTS this control ends its readout and its review buffer with
        /// - what the mod's gesture chords do here, one sentence per hint, in declared order
        /// (<see cref="NodeHint"/>). Declared where the screen wires the gesture, so the two cannot
        /// drift apart; null - the ordinary case - is a control whose gestures are the uniform ones
        /// every control has, or whose own game tooltip already states them.</summary>
        public IList<NodeHint> Hints;

        /// <summary>Optional. Horizontal value adjust (a slider): sign is -1 (decrease) / +1 (increase),
        /// large requests a coarse step. When set, left/right do NOT navigate.</summary>
        public Action<int, bool> OnAdjust;

        /// <summary>Optional. The control's state line, spoken IMMEDIATELY (interrupting) after an
        /// activation/adjust that changes state — the synchronous feedback path for rapid key repeats.
        /// Asynchronous/game-driven changes ride the Live announcement watch instead.</summary>
        public Func<string> StateText;

        /// <summary>Optional. The text type-ahead matches against; null = the first announcement part
        /// (the label). (A cell whose label is a bare number can search as its row's name, etc.)</summary>
        public Func<string> SearchText;

        /// <summary>If true, type-ahead never matches this control.</summary>
        public bool ExcludeFromSearch;

        /// <summary>Which column of a tabular row this control is - 0 (the default) for the row's
        /// primary cell and for everything that is not in a table. Stamped by
        /// <see cref="GraphSheet"/>, and read by type-ahead: a row contributes ONE result, its
        /// primary, because every cell of it searches as the row's name.</summary>
        public int Column;

        /// <summary>Type-ahead matches this cell BY ITS OWN words rather than by its row's - which is
        /// what a table whose rows have no name is made of (<see cref="GraphSheet.NamedRows"/>). The
        /// one-result-per-row filter exists because every cell of a named row searches as that row;
        /// where the row has no name, dropping the non-primary cells would make the columns
        /// unsearchable instead of un-duplicated. Stamped by <see cref="GraphSheet"/> on EVERY cell of
        /// an unnamed row, column 0 included.
        ///
        /// It is also what stops a landing being walked off: a search made from column 3 steps back into
        /// that column after landing on the row's primary, because the primary matched by the row's name
        /// and the player was reading column 3 - but a cell that matched by its OWN words IS the thing
        /// asked for, and following the column off it reads a neighbour. So anything declared at a
        /// non-zero column that is not a row cell (a sort-header band) must set this too, or the
        /// one-result-per-row filter hides all but its first column.</summary>
        public bool SearchesAsItself;

        /// <summary>
        /// The caption of the column this cell sits in, said on ARRIVING in the column by any means
        /// other than the sideways step that already labels its own edge - a Tab into the table, a
        /// search landing, a re-read, a vertical crossing that fell to another column.
        ///
        /// Only a table whose rows have no name stamps it (<see cref="GraphSheet.NamedRows"/>). Where
        /// the rows ARE named, column 0 says which row it is and that is the orientation a landing
        /// needs; where they are not, a cell landed on out of the blue says neither its row nor its
        /// column, and the column heading is the only place left for it to sit. Suppressed whenever the
        /// player came from a cell under the same heading, so walking a column says it once.
        /// </summary>
        public string ColumnHeader;

        /// <summary>Which ROW of a table this control sits in - the same <see cref="TableRow"/> object
        /// on every cell of the row, null outside a table. See <see cref="TableRow"/> for what the
        /// announcer does with it.</summary>
        public TableRow Row;

        /// <summary>
        /// What to bring into view when focus lands here, for a control whose own identity carries no
        /// backing object.
        ///
        /// Scrolling follows the node's <c>ControlId.Subject</c>, and a table gives that reference
        /// to the row's PRIMARY cell alone - identity is per cell, and every cell answering to the
        /// same object would make a moved row resolve to whichever cell was reached first. So a cell
        /// in another column names the row here instead: same scrolling, untouched identity. Null
        /// everywhere else, where the reference is the answer.
        /// </summary>
        public object ScrollAnchor;

        /// <summary>Optional (Expandable groups): override HOW expansion state changes. When null the
        /// engine mutates the persistent expansion set (<see cref="GraphState.Expanded"/>); an adapter
        /// wires these to a retained game-side container's Expand/Collapse instead.</summary>
        public Action OnExpand;

        public Action OnCollapse;

        /// <summary>Set when this group's own announcements already include its expanded/collapsed state,
        /// so the announcer doesn't append it again.</summary>
        public bool SpeaksOwnExpansion;

        /// <summary>Set when this node's announcements already include its list position, so the announcer
        /// doesn't append the auto-stamped one.</summary>
        public bool SpeaksOwnPosition;

        /// <summary>
        /// Optional. Make the GAME look the way it would if the pointer were resting on this control —
        /// its hover highlight, a menu opening under it, the game's own tooltip. Nothing here is spoken
        /// or navigable; it exists so that someone watching the screen sees where the keyboard is, which
        /// is what makes a screen-reader player's turn followable by the people sitting next to them.
        ///
        /// Called by the navigator at the one place focus is committed, whatever moved it, and only when
        /// focus actually changes control. An adapter should treat it as a REQUEST recorded now and
        /// applied once per frame rather than as game calls made inline: focus is re-committed after a
        /// rebuild, and animations restarted mid-flight flicker.
        /// </summary>
        public Action OnFocusVisual;

        /// <summary>Optional. The other half of <see cref="OnFocusVisual"/>: focus has left this control.
        /// Called before the new control's OnFocusVisual, and also when the screen closes or the mod
        /// stops, so nothing is left looking hovered.</summary>
        public Action OnBlurVisual;

        /// <summary>
        /// Optional. WHAT <see cref="OnFocusVisual"/> aims the pointer at, as the game's own tooltip
        /// object - the aim written down beside the content instead of hidden inside a closure.
        ///
        /// A control carrying two tooltips shows only the one the pointer is sent to, so "which
        /// tooltip does this node point at" is a question about the node, and anything that has to
        /// ANSWER it - a parity audit, a probe, a screen inheriting another's declaration - used to
        /// re-derive it by walking the widget tree. That answer is wrong wherever the deepest tooltip
        /// in a card is decoration, and it reported defects on screens whose pointing was right.
        ///
        /// Set by every pointing helper, from the same argument it aims, so the two cannot drift; the
        /// LAST pointing call on a vtable wins, exactly as it does for the visual. Resolved when
        /// asked rather than when declared, because a widget the game fills in later gets its tooltip
        /// after the node is built. Null = this node aims at nothing.
        ///
        /// Typed as <see cref="object"/> because the core knows nothing of the game's toolkit; every
        /// reader casts it back to the engine's tooltip type.
        /// </summary>
        public Func<object> PointsAt;

    }

    /// <summary>
    /// A control offered to the graph: its identity, its behaviors — and, by which SUBCLASS it is,
    /// whether there is anything on the screen whose paint state can vouch for it.
    ///
    /// Identity and evidence are two different questions and this type is where they stop competing.
    /// <see cref="ControlId.Subject"/> answers WHICH THING a node is, so that focus follows it across
    /// rebuilds. Whether the game is still drawing the thing is a separate question with only two
    /// honest answers, and a nullable slot could not tell them apart: "here is the widget, ask it"
    /// and "nothing here can be asked" both read as a field that may be empty, so a walk that simply
    /// forgot to say looked exactly like a walk that had nothing to say. So the two answers are two
    /// TYPES — <see cref="DrawnNode"/> carries the widget and cannot be built without one,
    /// <see cref="SyntheticNode"/> has no evidence member at all — and every declaration site says
    /// which it is because the compiler will not let it stay silent.
    ///
    /// The base knows nothing about evidence: it is what the graph machinery needs to make a node,
    /// and no more.
    /// </summary>
    public abstract class NodeDeclaration
    {
        /// <summary>The control's identity. Never null.</summary>
        public ControlId Id { get; private set; }

        /// <summary>The control's behaviors. Never null; must carry at least one announcement.</summary>
        public NodeVtable Vtable { get; private set; }

        public NodeDeclaration(ControlId id, NodeVtable vtable)
        {
            if (id == null) throw new ArgumentNullException("id");
            if (vtable == null) throw new ArgumentNullException("vtable");
            Id = id;
            Vtable = vtable;
        }
    }

    /// <summary>
    /// A control the GAME is drawing, declared with the widget that proves it.
    ///
    /// <see cref="DrawnBy"/> is the thing the host asks "are you still on the screen" before the node
    /// is allowed to exist at all. It is required, not optional: content the game POOLS is keyed
    /// structurally on purpose (a row keyed by its recycled widget would carry the cursor to whatever
    /// the pool binds there next), so those nodes' ids name no object — and the recycled widget is
    /// nonetheless exactly the right thing to ask about paint state. Before this was a type, such a
    /// node could be declared with neither answer and was silently ungated; four walks announced
    /// retired rows that way, and each was found by a bug report.
    ///
    /// Typed as <see cref="object"/> because this assembly knows nothing of the game's toolkit; the
    /// core stores it and never interprets it.
    /// </summary>
    public sealed class DrawnNode : NodeDeclaration
    {
        /// <summary>The widget whose paint state vouches for this content. Never null.</summary>
        public object DrawnBy { get; private set; }

        public DrawnNode(ControlId id, NodeVtable vtable, object drawnBy)
            : base(id, vtable)
        {
            if (drawnBy == null) throw new ArgumentNullException("drawnBy");
            DrawnBy = drawnBy;
        }
    }

    /// <summary>
    /// A control with nothing on the screen to ask about: untestable by construction, and therefore
    /// never dropped.
    ///
    /// Two origins are legitimate, and only two:
    ///
    /// <para><b>Synthesized from game facts.</b> A place on the map, a fleet in a repository, a row
    /// read out of a model — content the mod assembled from the game's own data rather than off a
    /// drawn widget. Nothing paints it as a unit, so honesty about whether it still exists lives at
    /// the ENUMERATION site: the walk that lists these must list only what is really there.</para>
    ///
    /// <para><b>Mod-authored UI.</b> The settings screens, the turn log, a window's own structural
    /// extras — controls this mod invented, which the game draws nothing for.</para>
    ///
    /// Anything else — a node read off a widget the walk was holding — is a
    /// <see cref="DrawnNode"/>, and declaring it here instead is a misdeclaration the engine's own
    /// door reports.
    /// </summary>
    public sealed class SyntheticNode : NodeDeclaration
    {
        public SyntheticNode(ControlId id, NodeVtable vtable)
            : base(id, vtable) { }
    }

    /// <summary>
    /// Where a row sits in its table, shared by every cell of that row.
    ///
    /// A table's position phrase is about the ROW, not the cell: it is spoken when the player lands on
    /// a row and when they move to a DIFFERENT one, and stays quiet while they walk that row's columns
    /// - including the step back onto column 0, which no per-node position part could tell from an
    /// arrival. That is why this is one shared object with a <see cref="Key"/> rather than an index on
    /// each cell: the announcer compares the row the player came FROM with the row they landed in
    /// (<see cref="GraphAnnouncer.Compose"/>), and the key is what survives the rebuild between the two.
    ///
    /// <see cref="Count"/> is filled in once the table knows how many rows it has, so a sheet may stamp
    /// the object on a cell before the last row is emitted.
    /// </summary>
    public sealed class TableRow
    {
        /// <summary>Identifies the row across rebuilds - the sheet's own structural key for it.</summary>
        public string Key;

        /// <summary>1-based position of the row within its table.</summary>
        public int Index;

        /// <summary>How many rows the table has; 0 until the table is finished (nothing is spoken).</summary>
        public int Count;
    }

    /// <summary>A directed edge to another node, with an optional spoken transition line (a "lane
    /// change" — e.g. crossing into a new column band). Kept as plain data; contextual announcements are
    /// composed from node metadata by the announcer, not per-edge closures (GC discipline).</summary>
    public sealed class Transition
    {
        public ControlId Destination;
        public string Label; // spoken only while crossing this edge; null = silent edge

        public Transition(ControlId destination, string label = null)
        {
            Destination = destination;
            Label = label;
        }
    }

    /// <summary>A control: identity, behaviors, directional transitions, and structural metadata (its
    /// parent chain, tab-stop and region membership, expandability).</summary>
    public sealed class GraphNode
    {
        /// <summary>What the screen offered, with its nature intact - the one place a built node's
        /// evidence (or its declared absence) can still be asked for, by the gate that acted on it and
        /// by the audits that check the gate.</summary>
        public NodeDeclaration Declared;

        public ControlId Id
        {
            get { return Declared == null ? null : Declared.Id; }
        }

        public NodeVtable Vtable
        {
            get { return Declared == null ? null : Declared.Vtable; }
        }

        public readonly Dictionary<GraphDir, Transition> Transitions = new Dictionary<GraphDir, Transition>();

        /// <summary>The node's structural parent within THIS render, or null at screen level. The parent
        /// chain IS the presentation hierarchy: the announcer prefix-diffs old/new chains by identity, so
        /// entering a group reads its levels outermost-first and descending from a group onto its own
        /// child re-announces nothing (the group is on the chain and is the from-node). A parent may be
        /// non-focusable pure structure (a labeled panel — <see cref="Focusable"/> false, never in
        /// Nodes/Order) or a real control (a tree group header).</summary>
        public GraphNode Parent;

        /// <summary>False for a pure-structure parent node (a labeled panel): it exists only on
        /// <see cref="Parent"/> chains for announcements — never navigable, never in Nodes/Order.</summary>
        public bool Focusable = true;

        /// <summary>This node is a group that can expand/collapse (a tree section header). The engine's
        /// tree operations (expand/collapse/descend/ascend) key off this.</summary>
        public bool Expandable;

        /// <summary>An <see cref="Expandable"/> group's state AT THIS RENDER (stamped by the builder from
        /// the persistent expansion set, or the explicit value the declarer passed).</summary>
        public bool Expanded;

        /// <summary>The Tab-stop this node belongs to. Nodes sharing a StopKey form one stop; Tab cycles
        /// stops in first-appearance order, landing on the stop's remembered position.</summary>
        public object StopKey;

        /// <summary>The region (within a stop) this node belongs to, or null. The host's region-jump chord
        /// (ES2 Access: Alt+Up/Down) jumps between regions in first-appearance order.</summary>
        public object RegionKey;

        /// <summary>Auto-stamped sibling position (1-based) and count, from the builder: menu-mode nodes
        /// grouped by (parent, stop) — "3 of 10" among the siblings arrows actually reach. 0 = none
        /// (raw/grid nodes, or a lone sibling outside an expandable group, which reads no position).</summary>
        public int PositionIndex;

        public int PositionCount;

        /// <summary>On a parent (context/group) node: its direct children get NO auto position — for
        /// log-like streams where "37 of 200" is noise.</summary>
        public bool SuppressChildPositions;
    }

    /// <summary>
    /// One built snapshot of a graph: the nodes (keyed by structural identity), their order of
    /// declaration, and where focus starts when there is no prior position. Rebuilt per operation and
    /// thrown away — live state belongs in the node callbacks, not here.
    /// </summary>
    public sealed class GraphRender
    {
        public ControlId StartKey;
        public readonly Dictionary<ControlId, GraphNode> Nodes = new Dictionary<ControlId, GraphNode>();

        /// <summary>Declaration order — drives stop/region cycling and type-ahead scan order.</summary>
        public readonly List<GraphNode> Order = new List<GraphNode>();

        /// <summary>Where Tab lands in a stop that does not want its FIRST node - see
        /// <see cref="GraphBuilder.LandStopOn"/>. Keyed by stop key; a stop with no entry lands on its
        /// first node as before.</summary>
        public readonly Dictionary<object, ControlId> StopLandings = new Dictionary<object, ControlId>();

        /// <summary>This build offers FEWER KINDS of thing than the one before it - see
        /// <see cref="GraphBuilder.SeatOnContainer"/>.</summary>
        public bool SeatOnContainer;

        public GraphNode NodeAt(ControlId key)
        {
            if (key == null) return null;
            GraphNode n;
            return Nodes.TryGetValue(key, out n) ? n : null;
        }
    }

    /// <summary>
    /// The persistent cursor for a graph — the only thing that survives between renders. Holds where
    /// focus is, the last computed traversal order (for closest-survivor recovery), per-stop remembered
    /// positions (so Tab returns to where you were in a stop), and a one-shot move request.
    /// </summary>
    public sealed class GraphState
    {
        /// <summary>The focused control's id (carries its Reference for tier-1 recovery). Null until first render.</summary>
        public ControlId CurKey;

        /// <summary>The down-right total order from the previous render. Null on first render.</summary>
        public List<ControlId> KeyOrder;

        /// <summary>If set, focus jumps here on the next render when present (consumed either way).</summary>
        public ControlId NextSuggestedMove;

        /// <summary>Remembered position per Tab-stop: where Tab lands when cycling back into a stop.</summary>
        public readonly Dictionary<object, ControlId> StopMemory = new Dictionary<object, ControlId>();

        /// <summary>The expanded groups (by id). The builder consults this for groups declared without an
        /// explicit state; the engine's expand/collapse operations mutate it. Screens hold NO expansion
        /// state of their own.</summary>
        public readonly HashSet<ControlId> Expanded = new HashSet<ControlId>();
    }
}
