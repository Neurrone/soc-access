using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Battlefields;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech.Spatial;
using UnityEngine;

namespace SongsOfConquestAccess.Tests
{
    [TestClass]
    public sealed class CombatTileSpeechFormatterTests : ModSettingsFixture
    {
        [TestMethod]
        public void InspectRangeIndicatorsFormatsAttackRangeWithoutTroopName()
        {
            string text = CombatInspectContext.FormatRangeIndicators(new HashSet<CombatRangeIndicator>
            {
                CombatRangeIndicator.Attack
            });

            Assert.AreEqual("Attack range", text);
        }

        [TestMethod]
        public void InspectRangeIndicatorsFormatsDeadlyRangeWithoutTroopName()
        {
            string text = CombatInspectContext.FormatRangeIndicators(new HashSet<CombatRangeIndicator>
            {
                CombatRangeIndicator.Attack,
                CombatRangeIndicator.Deadly
            });

            Assert.AreEqual("Deadly range", text);
        }

        [TestMethod]
        public void InspectRangeIndicatorsFormatsMovementRangeWithoutTroopName()
        {
            string text = CombatInspectContext.FormatRangeIndicators(new HashSet<CombatRangeIndicator>
            {
                CombatRangeIndicator.Movement
            });

            Assert.AreEqual("Movement range", text);
        }

        [TestMethod]
        public void InspectRangeIndicatorsFormatsAttackAndMovementWithoutTroopName()
        {
            string text = CombatInspectContext.FormatRangeIndicators(new HashSet<CombatRangeIndicator>
            {
                CombatRangeIndicator.Attack,
                CombatRangeIndicator.Movement
            });

            Assert.AreEqual("attack and movement range", text);
        }

        [TestMethod]
        public void InspectRangeIndicatorsFormatsZoneOfControlWithoutTroopName()
        {
            string text = CombatInspectContext.FormatRangeIndicators(new HashSet<CombatRangeIndicator>
            {
                CombatRangeIndicator.ZoneOfControl,
                CombatRangeIndicator.Movement
            });

            Assert.AreEqual("Zone of control and movement range", text);
        }

        [TestMethod]
        public void InspectTileSpeechUsesContextIndicators()
        {
            Vector2Int point = new Vector2Int(1, 0);
            CombatInspectContext context = CombatInspectContext.ForStack(new Vector2Int(0, 0));
            context.Add(point, CombatRangeIndicator.Attack);
            CombatTile tile = new CombatTile(point);

            string text = new CombatTileSpeechFormatter(null, context).DescribeInfluence(tile);

            Assert.AreEqual("Attack range", text);
        }

        /// <summary>What the stack cannot do and what has been done to it comes between its name and
        /// its health: the mod's short word for reloading, then the game's own restriction strings,
        /// then the names of the game's own buff and nerf indicators.</summary>
        [TestMethod]
        public void TroopReadsRestrictionsAndEffectsBetweenTheNameAndTheHealth()
        {
            CombatTroopFacts troop = new CombatTroopFacts(
                "Pulses",
                11,
                50,
                50,
                isEnemy: true,
                isActing: false,
                isReloading: true,
                restrictionNames: new[] { "Invulnerable" },
                effectNames: new[] { "Momentum x2" });

            Assert.AreEqual(
                "11 enemy Pulses, reloading, Invulnerable, Momentum x2, 50 / 50 health",
                CombatTileSpeechFormatter.ComposeTroop(troop, attackable: false, facing: null));
        }

        /// <summary>A blocked tile is every tile a troop or an attackable thing stands on, so saying
        /// so said nothing the readout had not already said. Statically unwalkable ground still says
        /// it, because nothing else does.</summary>
        [TestMethod]
        public void TileSaysImpassableAndNoLongerSaysBlocked()
        {
            Assert.AreEqual("4, 2", Describe(new CombatTile(new Vector2Int(4, 2)) { IsBlocked = true }));
            Assert.AreEqual("impassable, 4, 2", Describe(new CombatTile(new Vector2Int(4, 2)) { IsImpassable = true }));
        }

        /// <summary>A cell nothing can enter says no height: how high the gateposts framing a siege
        /// gate stand is no use to a player who can never put a troop on them.</summary>
        [TestMethod]
        public void TileSaysNoHeightForACellNothingCanEnter()
        {
            Assert.AreEqual("impassable, 4, 2", Describe(Blocked(null)));
            Assert.AreEqual(
                "gatepost, impassable, 4, 2",
                Describe(Blocked(new BattlefieldObstacle(BattlefieldObstacleKind.Gatepost, 0, 1))));
        }

        /// <summary>Raised ground is named by what it is where the ground itself has a name: a
        /// cliff nothing can climb, and a siege layout's wall, tower and stairs.</summary>
        [TestMethod]
        public void TileNamesACliffAndTheSiegeStructuresInsteadOfElevatedGround()
        {
            Assert.AreEqual("elevated ground, height 2, 4, 2", Describe(Ground(BattlefieldCellKind.Elevated, 2)));
            Assert.AreEqual("cliff, height 2, 4, 2", Describe(Ground(BattlefieldCellKind.Cliff, 2)));
            Assert.AreEqual("wall, height 2, 4, 2", Describe(Ground(BattlefieldCellKind.Wall, 2)));
            Assert.AreEqual("tower, height 3, 4, 2", Describe(Ground(BattlefieldCellKind.Tower, 3)));
            Assert.AreEqual("stairs, height 1, 4, 2", Describe(Ground(BattlefieldCellKind.Stairs, 1)));
        }

        /// <summary>A cell everything has to pass through says so, and it says it with the ground
        /// it stands on rather than instead of it: a choke cell is still flat or raised ground.
        /// </summary>
        [TestMethod]
        public void TileSaysChokePointAfterTheGroundAndOnlyOnAChokeCell()
        {
            Assert.AreEqual("choke point, 4, 2", Describe(Choke(BattlefieldCellKind.Flat, 0)));
            Assert.AreEqual(
                "elevated ground, height 1, choke point, 4, 2",
                Describe(Choke(BattlefieldCellKind.Elevated, 1)));
            Assert.AreEqual("4, 2", Describe(Ground(BattlefieldCellKind.Flat, 0)));
        }

        /// <summary>Switched off in the settings, the part is silent and the rest of the tile reads
        /// as it did.</summary>
        [TestMethod]
        public void TileSaysNothingOfAChokePointWhenThePartIsSwitchedOff()
        {
            BindTemporaryConfig();
            ModSettings.SetAnnouncementElementEnabled(
                CombatAnnouncementDefinitions.Tile,
                CombatAnnouncementDefinitions.Tile.GetElement(CombatAnnouncementDefinitions.TileKeys.ChokePoint),
                false);

            Assert.AreEqual("4, 2", Describe(Choke(BattlefieldCellKind.Flat, 0)));
        }

        private static CombatTile Choke(BattlefieldCellKind kind, byte elevation)
        {
            CombatTile tile = Ground(kind, elevation);
            tile.IsChokePoint = true;
            return tile;
        }

        /// <summary>A blocked cell raised above the board, which every gatepost of a walled town
        /// siege is: height 3, and nothing can stand there.</summary>
        private static CombatTile Blocked(BattlefieldObstacle obstacle)
        {
            return new CombatTile(new Vector2Int(4, 2))
            {
                IsImpassable = true,
                Kind = BattlefieldCellKind.Impassable,
                Elevation = 3,
                Obstacle = obstacle
            };
        }

        private static CombatTile Ground(BattlefieldCellKind kind, byte elevation)
        {
            return new CombatTile(new Vector2Int(4, 2)) { Kind = kind, Elevation = elevation };
        }

        private static string Describe(CombatTile tile)
        {
            return new CombatTileSpeechFormatter(null, null, includeEnemyInfluence: false).DescribeTile(tile);
        }

        [TestMethod]
        public void ConfigurableAnnouncementComposerUsesSuffixBetweenRenderedPartsOnly()
        {
            AnnouncementGroupDefinition group = new AnnouncementGroupDefinition(
                "test",
                "Test",
                ModStrings.Screens.TileAnnouncements,
                new AnnouncementElementDefinition("first", ModStrings.Screens.AnnouncementReachable),
                new AnnouncementElementDefinition("second", ModStrings.Screens.AnnouncementCoordinates),
                new AnnouncementElementDefinition("third", ModStrings.Screens.AnnouncementInfluence));

            string text = ComposeWithDefaults(group, new[]
            {
                new AnnouncementPart("first", "one"),
                new AnnouncementPart("second", "two")
            });

            Assert.AreEqual("one, two", text);
        }

        [TestMethod]
        public void ConfigurableAnnouncementComposerHonorsDefaultSuffixOff()
        {
            AnnouncementGroupDefinition group = new AnnouncementGroupDefinition(
                "test_no_suffix",
                "Test",
                ModStrings.Screens.TileAnnouncements,
                new AnnouncementElementDefinition("first", ModStrings.Screens.AnnouncementReachable, defaultSuffix: false),
                new AnnouncementElementDefinition("second", ModStrings.Screens.AnnouncementCoordinates));

            string text = ComposeWithDefaults(group, new[]
            {
                new AnnouncementPart("first", "one"),
                new AnnouncementPart("second", "two")
            });

            Assert.AreEqual("one two", text);
        }

        private static string ComposeWithDefaults(AnnouncementGroupDefinition group, IEnumerable<AnnouncementPart> parts)
        {
            return ConfigurableAnnouncementComposer.Compose(
                group,
                parts,
                testGroup =>
                {
                    List<string> keys = new List<string>();
                    for (int i = 0; i < testGroup.Elements.Count; i++)
                    {
                        keys.Add(testGroup.Elements[i].Key);
                    }

                    return keys;
                },
                (testGroup, element) => element.DefaultEnabled,
                (testGroup, element) => element.DefaultSuffix);
        }
    }
}
