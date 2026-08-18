using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using UnityEngine;

namespace SongsOfConquestAccess.Scanner
{
    internal enum ScannerResultKind
    {
        Point,
        TerrainPoint,
        TerrainGroup,
        AreaGroup,
        UnexploredGroup,
        CommanderZoneOfControl
    }

    /// <summary>
    /// How the thing stands towards the local player, where that is a fact the
    /// game knows. <see cref="None"/> covers results that have no owner at all,
    /// such as terrain.
    /// </summary>
    internal enum ScannerResultRelationship
    {
        None,
        Neutral,
        Friendly,
        Enemy
    }

    internal sealed class ScannerResult
    {
        private string _itemKey;
        private string _itemLabel;
        private string _instanceLabel;

        public ScannerResult(string key, string label, Vector2Int position)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new System.ArgumentException("Scanner result key is required.", "key");
            }

            Key = key;
            Label = label;
            Position = position;
            Points = new List<Vector2Int>();
        }

        public string Label { get; private set; }

        public string Key { get; private set; }

        /// <summary>
        /// What the thing is called as a group. Defaults to the label, which is
        /// what makes every entity with the same name collapse into one item
        /// without the adapters saying anything.
        /// </summary>
        public string ItemLabel
        {
            get { return string.IsNullOrWhiteSpace(_itemLabel) ? Label : _itemLabel; }
            set { _itemLabel = value; }
        }

        /// <summary>
        /// Grouping identity. Set it where the item is a fixed kind of thing and
        /// a stable string is available, so item identity does not ride on
        /// translated text. It says what the thing is, not whose it is or what
        /// state it is in: the item layer splits those apart on their own.
        /// </summary>
        public string ItemKey
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_itemKey))
                {
                    return _itemKey;
                }

                string label = ItemLabel;
                return string.IsNullOrWhiteSpace(label) ? Key : label;
            }

            set { _itemKey = value; }
        }

        /// <summary>
        /// What tells this instance apart from the others under the same item,
        /// such as a cluster's tile count. Falls back to the item name, so a
        /// result always says what it is even when the item name is all there
        /// is to say.
        /// </summary>
        public string InstanceLabel
        {
            get { return string.IsNullOrWhiteSpace(_instanceLabel) ? ItemLabel : _instanceLabel; }
            set { _instanceLabel = value; }
        }

        public Vector2Int Position { get; private set; }

        public bool NotVisible { get; set; }

        public bool Unvisited { get; set; }

        /// <summary>
        /// Whether the troop whose turn it is can attack this. Set by the
        /// screens that have an acting troop to answer for, and left alone
        /// everywhere else.
        /// </summary>
        public bool Attackable { get; set; }

        public ScannerResultRelationship Relationship { get; set; }

        public ScannerResultKind Kind { get; set; }

        /// <summary>What the adapter classified this entry as when it built the snapshot, so a
        /// result sounds like itself even where its position tile no longer carries the entity.
        /// None for terrain groups, zones of control and every non-adventure snapshot.</summary>
        public AdventureEntityCategory EntityCategory { get; set; }

        public object StableReference { get; set; }

        public List<Vector2Int> Points { get; private set; }

        /// <summary>
        /// Takes the position the adapter reported on the last re-query, which
        /// is how a result covering many tiles ends up announced through the
        /// tile nearest the cursor rather than the one nearest the scan origin.
        /// </summary>
        public void ApplyRefresh(ScannerResultRefresh refresh)
        {
            if (refresh.IsValid)
            {
                Position = refresh.Position;
            }
        }
    }

    internal static class ScannerResultLabels
    {
        public static string ZoneOfControl(int tileCount, string name)
        {
            string tileText = ModText.Plural(ModStrings.Common.TileCount, tileCount, tileCount);
            return ModText.Get(ModStrings.Spatial.ZoneOfControlTiles, tileText, FormatPossessive(name));
        }

        /// <summary>
        /// The shorter form used once the item name has already said "zone of
        /// control", leaving the instance to say whose it is and how big.
        /// </summary>
        public static string ZoneOfControlInstance(int tileCount, string name)
        {
            string tileText = ModText.Plural(ModStrings.Common.TileCount, tileCount, tileCount);
            return ModText.Get(ModStrings.Spatial.ZoneOfControlInstance, tileText, FormatPossessive(name));
        }

        private static string FormatPossessive(string name)
        {
            return ModText.FormatPossessiveName(name, ModStrings.Spatial.CommanderPossessive);
        }
    }
}
