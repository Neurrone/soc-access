using System;
using System.Collections.Generic;

namespace SongsOfConquestAccess.UI.Graph
{
    /// <summary>
    /// The graph-native table/document emitter — a sheet idiom built on graph primitives, with no special
    /// composer: one Tab-stop of vertically-stacked REGIONS (each a Ctrl+arrow jump target and a context
    /// level, so entering one announces its title once via the path diff), rows navigated Up/Down with the
    /// column preserved, cells Left/Right. The usual table framing rules ride the graph's own mechanisms:
    ///  - column header on column change  = left/right EDGE LABELS (the destination column's header),
    ///    the PRIMARY column included: stepping back left onto a row's name says the caption that
    ///    column is drawn under, the same as every other crossing. (It used to say nothing there, on
    ///    the argument that the primary's full readout identifies it; the owner's ruling is that a
    ///    table is easier to hold on to when every column announces itself the same way, and a caption
    ///    the game DREW must be sayable — the audit counted the unsaid one as painted-but-unsaid.)
    ///  - row name when off in a metadata column = vertical edge labels into non-primary cells, unless
    ///    the rows have no name to give (<see cref="NamedRows"/>) - and a table with no row names says
    ///    the column's caption on ARRIVING in it instead (<see cref="NodeVtable.ColumnHeader"/>), since
    ///    a landing there would otherwise name neither the row nor the column;
    ///  - the whole-row readout = the PRIMARY (column 0) cell's announcement list carrying the row's
    ///    metadata as extra parts — vertical navigation rides column 0, so moving down the table reads
    ///    the whole row, per-part filterable like any control;
    ///  - empty cells read the localized "blank" (<see cref="BlankText"/>).
    /// Emit rows in one region, then start the next; <see cref="Finish"/> closes the last region. Raw mode
    /// underneath (explicit edges), so no auto positions - the one position a table speaks is its ROW's
    /// ("3 of 12", the region's row count), stamped here as a <see cref="TableRow"/> and spoken by the
    /// announcer on row CHANGES only, never as the player walks a row's columns.
    ///
    /// The three text/type hooks are static injection points the host wires once at startup, the same
    /// shape <see cref="GraphAnnouncer"/> uses: a sheet is constructed per rebuild, so per-instance wiring
    /// would be repeated at every call site. <see cref="Reset"/> clears them for teardown and tests.
    /// </summary>
    public sealed class GraphSheet
    {
        /// <summary>Localized word for an empty metadata cell; null = empty cells read as nothing.</summary>
        public static Func<string> BlankText;

        /// <summary>Localized role word for a region that has columns ("table"); null = no role.</summary>
        public static Func<string> TableRoleText;

        /// <summary>The control type stamped on read-only metadata cells; null = untyped.</summary>
        public static ControlType TextCellType;

        /// <summary>Drop every injected hook — mod teardown, and test isolation.</summary>
        public static void Reset()
        {
            BlankText = null;
            TableRoleText = null;
            TextCellType = null;
        }

        /// <summary>
        /// Whether column 0 NAMES its row.
        ///
        /// True for every table whose rows are THINGS - a fleet, a system, a save - where landing in the
        /// Power column two rows down wants to hear which fleet it landed on, which is what the vertical
        /// edge labels say. False for a grid whose rows are only the lines the game wrapped one lattice
        /// onto (the economy screen's luxury families): there column 0 is a cell like any other, so
        /// naming the row would announce a NEIGHBOURING cell's words on every vertical crossing - a
        /// resource name in front of a different resource's figure.
        ///
        /// Two things follow from it, and they are the same fact twice: unnamed rows label no vertical
        /// crossing, and their cells are searched by their own words
        /// (<see cref="NodeVtable.SearchesAsItself"/>) rather than being filtered out as duplicates of a
        /// row name that does not exist.
        /// </summary>
        public bool NamedRows = true;

        private readonly GraphBuilder _b;
        private readonly string _key;
        private int _regionIndex = -1;
        private bool _contextOpen;

        // Current region state. Row cells carry their COLUMN IDENTITY - the logical column plus the
        // piece within it (sparse rows skip empty cells -- they aren't landable -- and a cell read as
        // several pieces puts them all under one logical column; vertical navigation matches by
        // identity, never by position along the row).
        private struct CellRef
        {
            public int Col;
            public int Piece;
            public ControlId Id;
        }

        /// <summary>
        /// One cell of a row at an explicit COLUMN IDENTITY: the logical column (1-based; 0 is the
        /// primary) and, for a cell the caller reads as several pieces, which piece this is
        /// (0-based, in drawn order).
        ///
        /// Identity rather than position is what lets rows be RAGGED without lying: a cell drawn as
        /// three badges on one row and none on the next puts three pieces under one column, so the
        /// caption is the column's whatever the piece, the next column's caption follows it, and
        /// Up/Down from a piece land on the same column of the neighbouring row - the same piece where
        /// it has one, the nearest where it has fewer, and the row's name where it has none.
        /// </summary>
        public struct SheetCell
        {
            public int Col;
            public int Piece;
            public NodeVtable Vtable;

            public SheetCell(int col, int piece, NodeVtable vtable)
            {
                Col = col;
                Piece = piece;
                Vtable = vtable;
            }
        }

        private string[] _columns; // headers for cells 0..N, primary first (null = a plain list region)
        private int _row = -1;
        private List<CellRef> _prevRowIds;
        private List<CellRef> _rowIds;
        private Func<string> _rowName; // the current row's primary label (for vertical edge labels)
        private Func<string> _prevRowName;
        private object _rowRef;        // the current row's domain object (identity keys), or null
        private object _rowWidget;     // the drawn row: evidence AND scroll anchor, or null
        private ControlId _first;      // the first PRIMARY this sheet emitted

        // Where each row of the CURRENT region sits in it. Stamped on every cell of the row as it is
        // emitted and completed with the count when the region closes -- how many rows a table has is
        // not known until its last one has been declared.
        private readonly List<TableRow> _regionRows = new List<TableRow>();
        private TableRow _rowPos;

        public GraphSheet(GraphBuilder b, string keyPrefix)
        {
            _b = b;
            _key = keyPrefix;
        }

        /// <summary>
        /// The primary cell of the first row this sheet emitted — where a screen whose content IS the
        /// table sends focus (<see cref="GraphBuilder.SetStart"/>). Null until a row has been emitted.
        ///
        /// The one id a caller is given, deliberately. Cell keys are the sheet's own business, and a
        /// screen that rebuilds one by hand is coupled to a private format that will silently stop
        /// matching: the id it hands the builder simply names no node, and the graph falls back to
        /// whatever was declared first with nothing said about it.
        /// </summary>
        public ControlId FirstRow
        {
            get { return _first; }
        }

        /// <summary>
        /// The structural key this sheet mints for one cell of the row belonging to
        /// <paramref name="rowRef"/> — the same identity-keyed rows <see cref="Row"/> takes a domain
        /// object for, and independent of the order they were emitted in.
        ///
        /// Nothing in the mod names a cell this way: a screen sends focus with
        /// <see cref="FirstRow"/> and everything else resolves a cell through the public
        /// <c>NodeVtable.Row</c> and <c>Column</c> stamps. It is here so that the tests that pin the
        /// WIRING between cells ask the sheet which id it minted, instead of re-spelling a private
        /// format that would go on matching nothing the day it changes.
        /// </summary>
        public string CellKey(object rowRef, int col)
        {
            return RowKeyFor(rowRef) + "c" + col;
        }

        /// <summary>
        /// Continue the table below a node that is NOT part of it — a paragraph the screen drew above
        /// the first row, a heading it declared itself. The first row then meets that node exactly as
        /// it would meet a row above it: every cell reaches it going up (a lone node is a row of one
        /// column, so the ragged-row fallback sends the other columns there too) and it reaches the
        /// first row's primary going down.
        ///
        /// Between <see cref="Region"/> and the first row. This is what keeps a screen from wiring its
        /// own seam: hand-written seam edges are the same shape every time and get the direction or the
        /// column coverage wrong.
        /// </summary>
        public GraphSheet Follows(ControlId node)
        {
            // Seeded as the row just finished, because that is what the next row wires itself against.
            _rowIds = node == null ? null : new List<CellRef> { new CellRef { Col = 0, Id = node } };
            _rowName = null;
            return this;
        }

        /// <summary>Start a region: a Ctrl+arrow jump target and a context level ("Fleets, table").
        /// <paramref name="columns"/> are the headers for EVERY column the rows will have, the primary's
        /// first — hand over the game's own header list as it is drawn, with no entry dropped, because
        /// reindexing at the call site is the mistake this shape exists to prevent. A column the game
        /// draws no caption over is a null entry (the crossing into it stays label-free); null/empty for
        /// the whole array = a plain list region, which labels nothing.</summary>
        public GraphSheet Region(string label, string[] columns = null, string role = null)
        {
            CloseRegion();
            _regionIndex++;
            _b.SetRegion(_key + "reg:" + _regionIndex);
            if (!string.IsNullOrEmpty(label))
            {
                _b.PushContext(label, role ?? DefaultRole(columns), false);
                _contextOpen = true;
            }
            _columns = columns;
            return this;
        }

        // A region is a TABLE once it has a column beside the primary; one captioned column on its own
        // is still the list it looks like (the array counts the primary, so the threshold is 1, not 0).
        private static string DefaultRole(string[] columns)
        {
            return columns != null && columns.Length > 1 && TableRoleText != null ? TableRoleText() : null;
        }

        /// <summary>One row: the interactive/primary cell's vtable plus the metadata cell values (which
        /// are the region's columns from the SECOND on, the primary's own caption being the first).
        /// Metadata cells are read-only text.
        /// <paramref name="rowRef"/> is the row's DOMAIN OBJECT (the fleet, the system) and should be
        /// passed whenever rows can appear/vanish/reorder: keys derive from it, so a removed row's focus
        /// slides to a genuinely different identity and the differ announces the landing — index keys
        /// would silently rebadge the next row as "the same control". The primary additionally carries it
        /// as its reference (tier-1 follow when the row moves).
        /// <paramref name="rowWidget"/> is the thing the game DRAWS the row as, and it answers two
        /// questions at once: it is what every cell of the row is scrolled into view by
        /// (<see cref="NodeVtable.ScrollAnchor"/>), and it is the EVIDENCE the row's cells exist by -
        /// pass it and they are <see cref="DrawnNode"/>s the host's gate can withdraw, omit it and they
        /// are <see cref="SyntheticNode"/>s nothing can check. Pass it wherever the game draws a row
        /// widget at all, and always where the row is keyed by a MODEL: the row object is then a save,
        /// a term, a trait - something with no rectangle at all - so the viewport had nothing to follow
        /// and the cursor walked off the bottom of the list with the list standing still.</summary>
        public GraphSheet Row(
            NodeVtable primary,
            object rowRef,
            object rowWidget,
            params Func<string>[] cells
        )
        {
            BeginRow(primary, rowRef, rowWidget);
            if (cells != null)
                for (int i = 0; i < cells.Length; i++)
                {
                    Func<string> v = cells[i];
                    if (v == null) continue; // sparse: an empty logical column isn't landable
                    EmitCell(new NodeVtable
                    {
                        ControlType = TextCellType,
                        // Mutable for the same reason every other vtable factory's list is: a caller
                        // extending the parts must not meet an IList that refuses Add at run time.
                        Announcements = new List<NodeAnnouncement> { new NodeAnnouncement(() => Blank(v())) },
                        // Type-ahead matches the row's name from any cell - unless the row has no name,
                        // where the cell's own words are the only thing there is to type at.
                        SearchText = NamedRows ? _rowName : null,
                    }, i + 1, 0);
                }
            WireVertical();
            return this;
        }

        /// <summary>A row whose cells are pre-built vtables at explicit LOGICAL columns (sparse grids).
        /// Column numbers are 1-based (0 is the primary).</summary>
        public GraphSheet RowAt(NodeVtable primary, object rowRef,
            IEnumerable<KeyValuePair<int, NodeVtable>> cells, object rowWidget = null)
        {
            BeginRow(primary, rowRef, rowWidget);
            if (cells != null)
                foreach (KeyValuePair<int, NodeVtable> kv in cells)
                    if (kv.Value != null) EmitCell(kv.Value, kv.Key, 0);
            WireVertical();
            return this;
        }

        /// <summary>The same for cells at explicit COLUMN IDENTITIES (<see cref="SheetCell"/>): a cell
        /// the caller reads as several pieces emits them all under its one logical column, in the order
        /// given, which is the order they are walked. Emit a row's cells in drawn order.</summary>
        public GraphSheet RowAt(NodeVtable primary, object rowRef,
            IEnumerable<SheetCell> cells, object rowWidget = null)
        {
            BeginRow(primary, rowRef, rowWidget);
            if (cells != null)
                foreach (SheetCell cell in cells)
                    if (cell.Vtable != null) EmitCell(cell.Vtable, cell.Col, cell.Piece);
            WireVertical();
            return this;
        }

        private void BeginRow(NodeVtable primary, object rowRef, object rowWidget)
        {
            _rowRef = rowRef;
            _rowWidget = rowWidget;
            _row++;
            _prevRowIds = _rowIds;
            _prevRowName = _rowName;
            _rowIds = new List<CellRef>();
            _rowPos = new TableRow { Key = RowKey(), Index = _regionRows.Count + 1 };
            _regionRows.Add(_rowPos);

            // The row's name for vertical edge labels = the primary's label (first announcement part).
            _rowName = primary.Announcements != null && primary.Announcements.Count > 0
                ? primary.Announcements[0].Text : null;

            EmitCell(primary, 0, 0);
        }

        /// <summary>A single full-width line (a lead row like "Your dust", a section note).</summary>
        public GraphSheet Line(NodeVtable vt, object rowWidget = null)
        {
            BeginRow(vt, null, rowWidget);
            WireVertical();
            return this;
        }

        /// <summary>Close the final region. Call once after the last row.</summary>
        public void Finish()
        {
            CloseRegion();
        }

        private void CloseRegion()
        {
            if (_contextOpen)
            {
                _b.PopContext();
                _contextOpen = false;
            }
            // Rows DO chain across region boundaries: the last row of a region wires to the first of the
            // next as rows are emitted (prev-row linkage carries across Region()).
            _columns = null;

            // "3 of 12" counts the rows of the TABLE the player is in, and a region is one table.
            for (int i = 0; i < _regionRows.Count; i++) _regionRows[i].Count = _regionRows.Count;
            _regionRows.Clear();
            _rowPos = null;
        }

        private void EmitCell(NodeVtable vt, int col, int piece)
        {
            // Which column a cell is in is the sheet's own knowledge, and type-ahead needs it: a row
            // whose every cell searches as the row's name must offer the player one result, not one
            // per column. Stamped here so no caller can forget it.
            vt.Column = col;
            // Column 0 included: where the rows have no name, the primary is a cell like any other and
            // its words are its own. Stamping it too is what tells a search that landed there that it
            // has arrived (type-ahead follows the column the player was reading only off a cell that
            // matched by its ROW's name) - without it a search from column 3 walks one cell past the
            // Transvine it found.
            if (!NamedRows) vt.SearchesAsItself = true;

            // A table whose rows have no name is the one where a landing identifies nothing: the row
            // cannot say where it is and the cell is another cell like it. So its cells carry the
            // column's caption for the announcer to say on arrival (NodeVtable.ColumnHeader) - the same
            // words the sideways edge already crosses with, in the case where no edge was crossed.
            if (!NamedRows) vt.ColumnHeader = Header(col);

            // Which row it is in, for the position the announcer speaks on row CHANGES only.
            vt.Row = _rowPos;

            // What a landing here is scrolled into view by: the widget the game drew the row as where
            // the caller named one - the only answer a MODEL-keyed row has, since its key has no
            // rectangle - and otherwise the row's own object, which a cell in another column carries
            // nothing of its own to be found by (identity is per cell).
            if (vt.ScrollAnchor == null)
            {
                vt.ScrollAnchor = _rowWidget ?? (col != 0 ? _rowRef : null);
            }

            // Identity keys when the row has a domain object: stable across reorders/removals (the
            // primary also carries the reference for tier-1 follow); positional only for static lines.
            // A piece past the first carries its index in the key; the first keeps the key a
            // one-piece cell always had, so nothing that names a cell by it changes.
            string skey = RowKey() + "c" + col + (piece > 0 ? "p" + piece : string.Empty);
            ControlId id = _rowRef != null && col == 0
                ? ControlId.For(_rowRef, skey)
                : ControlId.Structural(skey);
            // The row's NATURE is whether the caller handed over the widget the game draws it as. With
            // one, every cell of the row is DRAWN and stands on that widget: the sheet knows nothing
            // about widgets and cannot ask it anything, but it does not have to - it passes the thing
            // along and the host's gate asks. That is the answer for a table read off a drawn one,
            // where each row is a line the game is holding: the walk that enumerated the rows filtered
            // them once (TableSheet.Lines keeps the visible, non-transparent ones) and this makes the
            // same fact checkable every frame instead of once at enumeration.
            //
            // Without one it is SYNTHETIC, which is honest rather than lax: a sheet fed rows composed
            // out of the game's data - a grid the mod laid out, a footer band cut from several labels -
            // has no single widget any cell of it stands on, and there is nothing for a gate to ask
            // about. Its honesty lives at the walk that enumerated the rows, as every synthetic node's
            // does. Types are constructed directly because this assembly has no engine-side door to
            // come through (<c>UI.Nodes</c>): they are its own.
            _b.AddNode(
                _rowWidget != null
                    ? (NodeDeclaration)new DrawnNode(id, vt, _rowWidget)
                    : new SyntheticNode(id, vt)
            );
            if (col == 0 && _first == null) _first = id;

            // Left/right to the nearest EMITTED cell (sparse rows skip empty columns), labeled with the
            // destination column's header -- the primary's included, so every crossing in a table names
            // where it landed and no drawn caption goes unsaid.
            if (_rowIds.Count > 0)
            {
                CellRef left = _rowIds[_rowIds.Count - 1];
                // A step between two pieces of ONE column crosses no column, so it carries no
                // caption: the caption was said on the way into the column and is said again on
                // the way out of it.
                bool crossing = left.Col != col;
                _b.Connect(id, GraphDir.Left, left.Id, crossing ? Header(left.Col) : null);
                _b.Connect(left.Id, GraphDir.Right, id, crossing ? Header(col) : null);
            }
            _rowIds.Add(new CellRef { Col = col, Piece = piece, Id = id });
        }

        // Vertical edges between the completed row and the previous one: the same LOGICAL column where
        // both rows have it, else the other row's primary (sparse/ragged rows never dead-end). Labels
        // name the destination ROW when landing off-primary (so you know which row you're in without
        // the full readout); landings on column 0 stay unlabeled -- the primary's parts read the row.
        private void WireVertical()
        {
            if (_prevRowIds == null || _prevRowIds.Count == 0) return;

            foreach (CellRef cell in _rowIds)
            {
                bool matched = HasCol(_prevRowIds, cell.Col);
                _b.Connect(cell.Id, GraphDir.Up, FindAt(_prevRowIds, cell.Col, cell.Piece),
                    matched && cell.Col > 0 && NamedRows ? Text(_prevRowName) : null);
            }
            foreach (CellRef cell in _prevRowIds)
            {
                bool matched = HasCol(_rowIds, cell.Col);
                _b.Connect(cell.Id, GraphDir.Down, FindAt(_rowIds, cell.Col, cell.Piece),
                    matched && cell.Col > 0 && NamedRows ? Text(_rowName) : null);
            }
        }

        // The cell of <paramref name="row"/> at a column identity: the same piece of the same column
        // where the row has it, else the NEAREST piece of that column (a row drawing fewer pieces
        // lands on its last one, never on a neighbouring column), else the row's primary.
        private static ControlId FindAt(List<CellRef> row, int col, int piece)
        {
            ControlId nearest = null;
            int distance = int.MaxValue;
            foreach (CellRef c in row)
            {
                if (c.Col != col) continue;
                if (c.Piece == piece) return c.Id;
                int apart = Math.Abs(c.Piece - piece);
                if (apart < distance)
                {
                    distance = apart;
                    nearest = c.Id;
                }
            }
            return nearest ?? row[0].Id; // fall to the row's primary
        }

        private static bool HasCol(List<CellRef> row, int col)
        {
            foreach (CellRef c in row)
                if (c.Col == col) return true;
            return false;
        }

        // The prefix every cell of the current row keys itself with — and the row's own identity across
        // rebuilds, which is what tells a step along the row from a move into a different one.
        // External tooling never parses this format: a cell is resolved to its row through the public
        // NodeVtable.Row and Column stamps, which is how the parity probe finds a cell's widget.
        private string RowKey()
        {
            return _rowRef != null ? RowKeyFor(_rowRef) : _key + "r" + _row;
        }

        // A row's identity is the caller's, and there are exactly two ways to hand one over.
        //
        // A STRING rowRef is a stable id the caller wrote, and it is used whole rather than hashed: a
        // hash of it is a lossy projection, and two ids that hashed alike would mint the same
        // ControlId - which GraphBuilder refuses as a "Duplicate control id" and which blanks the
        // whole render rather than losing one row. Two rows given the SAME id are the caller's own
        // mistake to fix, and a caller with repeats disambiguates them before handing them over.
        //
        // ANYTHING ELSE is identified by the OBJECT, so the hash asked for is the identity one rather
        // than whatever Equals/GetHashCode the type overrode. A type with value equality would
        // otherwise collapse two distinct rows onto one key for the same reason, and every other use
        // the sheet makes of rowRef - the primary's ControlId subject, the scroll anchor - is already
        // by reference.
        private string RowKeyFor(object rowRef)
        {
            string id = rowRef as string;
            return _key
                + "row"
                + (id ?? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(rowRef).ToString(
                    System.Globalization.CultureInfo.InvariantCulture
                ));
        }

        private string Header(int col)
        {
            return _columns != null && col >= 0 && col < _columns.Length ? _columns[col] : null;
        }

        private static string Text(Func<string> f)
        {
            return f != null ? f() : null;
        }

        private static string Blank(string v)
        {
            if (!TextUtil.IsBlank(v)) return v;
            return BlankText != null ? BlankText() : null;
        }
    }
}
