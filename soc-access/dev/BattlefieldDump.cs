using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Lavapotion.Cartography;
using Lavapotion.Utilities;
using Newtonsoft.Json;
using SongsOfConquest;
using SongsOfConquest.Client.Deployment;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Common.Details;
using SongsOfConquest.Common.Entities;
using SongsOfConquest.Common.Entities.Adventure;
using SongsOfConquest.Common.Map;
using SongsOfConquest.Server.Adventure.Map.Provider;
using SongsOfConquest.Server.Map;
using SongsOfConquestAccess.Battlefields;
using SongsOfConquestAccess.Loader.Dev;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech.Spatial;
using SongsOfConquestAccess.UI;
using Unity.Mathematics;
using UnityEngine;
using Zenject;

namespace SongsOfConquestAccess.Dev
{
    /// <summary>
    /// Development only, never spoken: writes every battlefield layout the game ships as one JSON
    /// file (terrain, passability, spawn points, entities) and one JPEG, the game's own deployment
    /// preview render with the spawn markers and no troops, so the layouts can be described
    /// offline and the descriptions checked against the picture. Called from /eval:
    /// <c>SongsOfConquestAccess.Dev.BattlefieldDump.All(@"C:\...\battlefields")</c>, or
    /// <c>Current(dir)</c> for the layout the open placement page shows.
    /// </summary>
    public static class BattlefieldDump
    {
        private static readonly Color AttackerColor = new Color(0.25f, 0.55f, 1f);
        private static readonly Color DefenderColor = new Color(1f, 0.35f, 0.25f);

        public static string All(string directory)
        {
            try
            {
                using (Services services = Services.Resolve())
                {
                    List<object> written = new List<object>();
                    LevelType[] types = LevelTypeExtensions.GetAllBattleLevelTypes();
                    ContentProfileType[] profiles = (ContentProfileType[])Enum.GetValues(typeof(ContentProfileType));
                    for (int t = 0; t < types.Length; t++)
                    {
                        for (int p = 0; p < profiles.Length; p++)
                        {
                            InternalLevelDefinition[] definitions = services.Levels.GetExclusiveToProfile(types[t], profiles[p], skipAddonCheck: true);
                            for (int d = 0; d < definitions.Length; d++)
                            {
                                InternalLevelDefinition definition = definitions[d];
                                MapFormat map = services.Levels.Load(types[t], definition.Path);
                                if (map == null)
                                {
                                    written.Add(new { type = types[t].ToString(), path = definition.Path, error = "Load returned null" });
                                    continue;
                                }

                                string addon = definition.IsExclusiveToAddon ? definition.ExclusiveAddon.ToString() : null;
                                written.Add(WriteLayout(services, map, types[t], profiles[p].ToString(), addon, Path.Combine(directory, types[t].ToString()), definition.Path));
                            }
                        }
                    }

                    return JsonConvert.SerializeObject(new { count = written.Count, layouts = written });
                }
            }
            catch (Exception e)
            {
                return DevJson.Error(e.ToString());
            }
        }

        /// <summary>The layout the open placement page shows, written as <c>current.json</c> and
        /// <c>current.jpg</c>, with the key the same layout has in <see cref="All"/>.</summary>
        public static string Current(string directory)
        {
            try
            {
                PreBattleMenu menu = Resources.FindObjectsOfTypeAll<PreBattleMenu>()
                    .FirstOrDefault(m => m != null && m.gameObject.activeInHierarchy);
                if (menu == null)
                {
                    return DevJson.Error("no active PreBattleMenu");
                }

                MapFormat map = Field(typeof(PreBattleMenu), "_mapFormat").GetValue(menu) as MapFormat;
                if (map == null)
                {
                    return DevJson.Error("PreBattleMenu has no map");
                }

                using (Services services = Services.Resolve())
                {
                    object summary = WriteLayout(services, map, map.Metadata.Type, null, null, directory, "current");
                    return JsonConvert.SerializeObject(summary);
                }
            }
            catch (Exception e)
            {
                return DevJson.Error(e.ToString());
            }
        }

        private static object WriteLayout(Services services, MapFormat map, LevelType type, string profile, string addon, string directory, string stem)
        {
            Directory.CreateDirectory(directory);
            int width = map.Metadata.Size.x;
            int height = map.Metadata.Size.y;

            EntitySpawnPointsEntry[] attackers = map.GetEntitySpawnPoints(
                services.Objects, services.Manifests.GetBattleBlueprint(BattleMapEntities.UtilityAttackerSpawnpoint), TroopSpawnPointType.Any);
            EntitySpawnPointsEntry[] defenders = map.GetEntitySpawnPoints(
                services.Objects, services.Manifests.GetBattleBlueprint(BattleMapEntities.UtilityDefenderSpawnpoint), TroopSpawnPointType.Any);

            services.Renderer.Clear();
            services.Renderer.SetMap(map);
            for (int i = 0; i < attackers.Length; i++)
            {
                services.Renderer.AddSpawnpoint(i, BattleSide.Left_Attacker, new int2(attackers[i].Point.x, attackers[i].Point.y), attackers[i].type);
            }

            for (int i = 0; i < defenders.Length; i++)
            {
                services.Renderer.AddSpawnpoint(attackers.Length + i, BattleSide.Right_Defender, new int2(defenders[i].Point.x, defenders[i].Point.y), defenders[i].type);
            }

            string imagePath = Path.Combine(directory, stem + ".jpg");
            File.WriteAllBytes(imagePath, services.RenderJpg());

            Cell[,] cells = new Cell[width, height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    cells[x, y] = ReadCell(services, map, x, y);
                }
            }

            // The mod's own reading of the ground, so the JSON says what the scanner says.
            BattlefieldTerrain terrain = BattlefieldTerrain.Analyse(
                new Vector2Int(width, height), TerrainCells(cells, width, height), type.IsSiege());
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    cells[x, y].Kind = terrain.GetKind(new Vector2Int(x, y));
                    cells[x, y].CliffNeighbours = Spoken(terrain.CliffNeighbours(new Vector2Int(x, y)));
                }
            }

            List<object> regions = new List<object>();
            List<object> chokePoints = new List<object>();
            foreach (BattlefieldRegion region in terrain.Regions)
            {
                object described = Describe(region, width, height);
                if (region.Kind == BattlefieldRegionKind.ChokePoint)
                {
                    chokePoints.Add(described);
                }
                else
                {
                    regions.Add(described);
                }
            }

            Dictionary<string, string> spawnGlyphs = new Dictionary<string, string>();
            List<object> attackerList = SpawnList(attackers, 0, cells, "A", spawnGlyphs);
            List<object> defenderList = SpawnList(defenders, attackers.Length, cells, "D", spawnGlyphs);

            List<object> entities = new List<object>();
            for (int i = 0; i < map.Contents.MapEntities.Count; i++)
            {
                MapEntityFormat entity = map.Contents.MapEntities[i];
                if (entity.Id == (ushort)BattleMapEntities.UtilityAttackerSpawnpoint || entity.Id == (ushort)BattleMapEntities.UtilityDefenderSpawnpoint)
                {
                    continue;
                }

                entities.Add(new
                {
                    blueprintId = entity.Id,
                    name = Enum.GetName(typeof(BattleMapEntities), (int)entity.Id) ?? entity.Name,
                    x = (int)entity.X,
                    y = (int)entity.Y,
                    spoken = Spoken(entity.X, entity.Y)
                });
            }

            List<object> cellList = new List<object>();
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Cell c = cells[x, y];
                    cellList.Add(new
                    {
                        x,
                        y,
                        spoken = c.Spoken,
                        onGrid = c.OnGrid,
                        elevation = c.Elevation,
                        water = c.Water,
                        impassable = c.Impassable,
                        travelCost = float.IsPositiveInfinity(c.TravelCost) ? (float?)null : c.TravelCost,
                        previewBlocked = c.PreviewBlocked,
                        kind = c.Kind.ToString(),
                        decoration = c.Decoration,
                        decorationName = DecorationName(type, c.Decoration),
                        terrainType = c.TerrainType,
                        theme = c.Theme,
                        standaloneDecoration = c.StandaloneDecoration,
                        effect = c.Effect,
                        cliffNeighbours = c.CliffNeighbours
                    });
                }
            }

            string key = type + "/" + map.Metadata.PathName;
            object record = new
            {
                key,
                levelType = type.ToString(),
                name = map.Metadata.Name,
                pathName = map.Metadata.PathName,
                description = map.Metadata.Description,
                info = map.Metadata.Info,
                contentProfile = profile,
                addon,
                isSiege = type.IsSiege(),
                isSiegeWithWalls = type.IsSiegeWithWalls(),
                size = new { width, height },
                context = new
                {
                    whenChosen = WhenChosen(type),
                    coordinates = "x runs 0 to width-1 from left to right as the picture shows it; y runs 0 to height-1 from the bottom (nearest the viewer) to the top. Odd rows sit half a cell to the right of even rows, which the mod speaks as x.5 (the 'spoken' fields), so the JSON and the picture agree with what the player hears.",
                    picture = "The image is the game's own deployment preview: light flat hexes are walkable ground, taller blocks are elevated ground (height = elevation), hatched or dark cells are blocked, missing cells are water. Blue markers are attacker spawn points, red markers are defender spawn points; a different marker shape means a 'Defence' spawn (siege engines).",
                    movement = "Troops step between neighbouring cells only when the elevation differs by at most 1; a bigger step is a cliff (listed per cell under cliffNeighbours as the spoken coordinate of the neighbour that cannot be reached directly). Water and impassable cells cannot be entered. Higher ground gives melee and ranged bonuses.",
                    vocabulary = "Every entry in regions and chokePoints carries the exact label the mod speaks for it, and cells carry the same kind the cursor reads out. Name a feature with that label's words.",
                    sides = "Attacker spawn points are blueprint 2, defender spawn points blueprint 3. The player is the attacker when they started the fight and the defender when attacked. The spawn point numbers the mod speaks follow the game's order: by type (Default, Defence, Fallback), then x ascending, then y descending.",
                    deployment = "Auto-placement puts ranged troops on Default spawns first, siege engines on Defence spawns, and melee troops away from towers, walls and stairs. The player may move a troop to any spawn point of their own side."
                },
                regions,
                chokePoints,
                spawnPoints = new { attacker = attackerList, defender = defenderList },
                derived = new
                {
                    attackerSpawnX = Range(attackers, e => e.Point.x),
                    attackerSpawnY = Range(attackers, e => e.Point.y),
                    defenderSpawnX = Range(defenders, e => e.Point.x),
                    defenderSpawnY = Range(defenders, e => e.Point.y),
                    attackerIsLeft = attackers.Length > 0 && defenders.Length > 0 && attackers.Average(e => e.Point.x) < defenders.Average(e => e.Point.x),
                    impassableCells = cells.Cast<Cell>().Count(c => c.OnGrid && c.Impassable),
                    waterCells = cells.Cast<Cell>().Count(c => c.Water),
                    elevatedCells = cells.Cast<Cell>().Count(c => c.OnGrid && c.Elevation > 0),
                    maxElevation = cells.Cast<Cell>().Max(c => c.Elevation)
                },
                entities,
                asciiLegend = "Rows top to bottom are y = height-1 down to 0; odd rows are indented half a cell. '~' water, '#' impassable, ' ' off the hex grid, '.' flat ground, 1-3 elevated ground of that height. In asciiSpawns: A/a attacker spawn (a = Defence type), D/d defender spawn, on top of the terrain glyphs.",
                asciiTerrain = Ascii(cells, width, height, null),
                asciiSpawns = Ascii(cells, width, height, spawnGlyphs),
                cells = cellList,
                image = Path.GetFileName(imagePath)
            };

            string jsonPath = Path.Combine(directory, stem + ".json");
            File.WriteAllText(jsonPath, JsonConvert.SerializeObject(record, Formatting.Indented));
            return new
            {
                key,
                name = map.Metadata.Name,
                profile,
                addon,
                size = width + "x" + height,
                attackers = attackers.Length,
                defenders = defenders.Length,
                attackerIsLeft = attackers.Length > 0 && defenders.Length > 0 && attackers.Average(e => e.Point.x) < defenders.Average(e => e.Point.x),
                json = jsonPath,
                image = imagePath
            };
        }

        /// <summary>The game's own names for what a battlefield cell can hold: every theme's
        /// decorations (with their localization key, travel cost and blocking), the effect brushes,
        /// and the battle map entity blueprints. Research only: what a decoration vocabulary for the
        /// cursor and the descriptions could be built from.</summary>
        public static string Vocabulary()
        {
            try
            {
                CartographyManifest manifest = CartographyManifestLoader.Instance != null ? CartographyManifestLoader.Instance.BattleManifest : null;
                if (manifest == null)
                {
                    return DevJson.Error("no battle manifest loaded");
                }

                ICartographyManifest contract = manifest;
                List<object> decorations = new List<object>();
                for (int theme = 0; theme < 8; theme++)
                {
                    ThemeManifest themeManifest = contract.GetTheme(theme) as ThemeManifest;
                    if (themeManifest == null)
                    {
                        continue;
                    }

                    for (int decoration = 1; decoration < 16; decoration++)
                    {
                        ThemeManifest.DecorationManifest entry = themeManifest.GetDecoration(decoration) as ThemeManifest.DecorationManifest;
                        if (entry == null)
                        {
                            continue;
                        }

                        List<object> brushes = new List<object>();
                        // The brush types live in Lavapotion.Cartography, which the mod does not
                        // reference; an array of them is still an array of UnityEngine.Object.
                        AddBrushes(brushes, "tile", Brushes(entry, "TileBrushes"));
                        AddBrushes(brushes, "chunk", Brushes(entry, "ChunkBrushes"));
                        AddBrushes(brushes, "world", Brushes(entry, "WorldBrushes"));
                        decorations.Add(new
                        {
                            theme,
                            themeName = themeManifest.Name,
                            decoration,
                            brushes,
                            nameKey = entry.NameKey,
                            name = GameText.Get(entry.NameKey, string.Empty),
                            travelCost = float.IsPositiveInfinity(entry.TravelCost) ? (float?)null : entry.TravelCost,
                            blocking = float.IsPositiveInfinity(entry.TravelCost) || entry.TravelCost > 0f
                        });
                    }
                }

                List<object> effects = new List<object>();
                for (int effect = 1; effect < 16; effect++)
                {
                    try
                    {
                        BrushSet brush = manifest.GetEffectBrush(effect);
                        effects.Add(new { effect, name = brush.name, blocking = brush.isBlocking });
                    }
                    catch (Exception)
                    {
                    }
                }

                IMapEntityManifestRetriever manifests = ProjectContext.Instance.Container.TryResolve<IMapEntityManifestRetriever>();
                List<object> entities = new List<object>();
                foreach (BattleMapEntities id in Enum.GetValues(typeof(BattleMapEntities)))
                {
                    IMapEntityBlueprint blueprint = null;
                    try { blueprint = manifests != null ? manifests.GetBattleBlueprint(id) : null; } catch (Exception) { }
                    entities.Add(new
                    {
                        id = (int)id,
                        enumName = id.ToString(),
                        nameKey = blueprint != null ? blueprint.NameKey : null,
                        name = blueprint != null ? GameText.Get(blueprint.NameKey, string.Empty) : null
                    });
                }

                return JsonConvert.SerializeObject(new { decorations, effects, entities });
            }
            catch (Exception e)
            {
                return DevJson.Error(e.ToString());
            }
        }

        private static UnityEngine.Object[] Brushes(object entry, string property)
        {
            PropertyInfo info = entry.GetType().GetProperty(property, BindingFlags.Public | BindingFlags.Instance);
            return info != null ? info.GetValue(entry, null) as UnityEngine.Object[] : null;
        }

        /// <summary>A brush asset's name and its editor description, which is the only record of
        /// what the brush draws (a battle decoration has no localized name).</summary>
        private static void AddBrushes(List<object> into, string kind, UnityEngine.Object[] brushes)
        {
            if (brushes == null)
            {
                return;
            }

            for (int i = 0; i < brushes.Length; i++)
            {
                UnityEngine.Object brush = brushes[i];
                if (brush == null)
                {
                    continue;
                }

                FieldInfo description = brush.GetType().GetField("_description", BindingFlags.NonPublic | BindingFlags.Instance);
                into.Add(new
                {
                    kind,
                    type = brush.GetType().Name,
                    name = brush.name,
                    description = description != null ? description.GetValue(brush) as string : null
                });
            }
        }

        private sealed class Cell
        {
            public string Spoken;
            public bool OnGrid;
            public int Elevation;
            public bool Water;
            public bool Impassable;
            public float TravelCost;
            public bool PreviewBlocked;
            public int Decoration;
            public int TerrainType;
            public int Theme;
            public int StandaloneDecoration;
            public int Effect;
            public BattlefieldCellKind Kind;
            public List<string> CliffNeighbours;
        }

        private static Cell ReadCell(Services services, MapFormat map, int x, int y)
        {
            int index = map.PointToIndex(new Vector2Int(x, y));
            MapFormat.AdventureMapContents contents = map.Contents;
            int theme = At(contents.ThemesArray, index);
            int terrainType = At(contents.TypesArray, index);
            int customType = At(contents.CustomTypesArray, index);
            int decoration = At(contents.DecorationsArray, index);
            int effect = At(contents.EffectsArray, index);
            int standalone = At(contents.StandaloneDecorationsArray, index);
            int water = At(contents.WaterArray, index);
            int bridge = At(contents.BridgesArray, index);

            // The game's own cost sum (AbstractStaticMapCache.UpdatePoint, battle branch: no road
            // factor); infinite cost is what IsWalkableStatic reads as impassable.
            float cost;
            if (services.Manifest == null)
            {
                cost = water > 0 || IsPreviewBlocker(decoration) ? float.PositiveInfinity : 1f;
            }
            else
            {
                float typeCost = customType > 0 ? services.Manifest.GetCustomTypeTravelCost(customType) : services.Manifest.GetTypeTravelCost(theme, terrainType);
                float decorationCost = decoration == 0 ? 0f : services.Manifest.GetDecorationTravelCost(theme, decoration);
                float effectCost = effect == 0 ? 0f : services.Manifest.GetEffectTravelCost(effect);
                float bridgeCost = bridge == 0 ? 0f : services.Manifest.GetBridgeTravelCost(bridge);
                float standaloneCost = standalone == 0 ? 0f : services.Manifest.GetStandaloneDecorationTravelCost(standalone);
                float waterCost = water > 0 ? float.PositiveInfinity : 0f;
                cost = typeCost + (standalone > 0 ? standaloneCost : decorationCost + waterCost) + effectCost + bridgeCost;
            }

            float3 ignored;
            bool onGrid = map.IsPointWithinMap(new Vector2Int(x, y)) && services.Renderer.PointToWorld(new int2(x, y), out ignored);
            return new Cell
            {
                Spoken = Spoken(x, y),
                OnGrid = onGrid,
                Elevation = At(contents.ElevationsArray, index),
                Water = water > 0,
                Impassable = float.IsPositiveInfinity(cost) || !onGrid,
                TravelCost = cost,
                PreviewBlocked = IsPreviewBlocker(decoration),
                Decoration = decoration,
                TerrainType = terrainType,
                Theme = theme,
                StandaloneDecoration = standalone,
                Effect = effect
            };
        }

        private static List<BattlefieldCell> TerrainCells(Cell[,] cells, int width, int height)
        {
            List<BattlefieldCell> terrain = new List<BattlefieldCell>(width * height);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Cell cell = cells[x, y];
                    terrain.Add(new BattlefieldCell(
                        new Vector2Int(x, y), cell.OnGrid, cell.Elevation, cell.Impassable, cell.Decoration));
                }
            }

            return terrain;
        }

        /// <summary>One group of ground as the JSON carries it: the label the mod speaks for it
        /// word for word, so a description written from this file and the scanner reading the same
        /// board cannot use two vocabularies.</summary>
        private static object Describe(BattlefieldRegion region, int width, int height)
        {
            double x = 0;
            double y = 0;
            for (int i = 0; i < region.Cells.Count; i++)
            {
                x += region.Cells[i].x;
                y += region.Cells[i].y;
            }

            return new
            {
                label = BattlefieldText.Region(region),
                kind = region.Kind.ToString(),
                shape = region.Shape.ToString(),
                cells = region.Count,
                height = region.Height,
                position = Position(x / region.Count, y / region.Count, width, height),
                spoken = Spoken(region.Cells)
            };
        }

        private static List<string> Spoken(List<Vector2Int> points)
        {
            List<string> spoken = new List<string>(points.Count);
            for (int i = 0; i < points.Count; i++)
            {
                spoken.Add(Spoken(points[i].x, points[i].y));
            }

            return spoken;
        }

        /// <summary>Where on the board a point sits, in the words the prompt allows: thirds
        /// horizontally (left, centre, right) and vertically (bottom, middle, top).</summary>
        private static string Position(double x, double y, int width, int height)
        {
            string horizontal = x < width / 3.0 ? "left" : x < 2 * width / 3.0 ? "centre" : "right";
            string vertical = y < height / 3.0 ? "bottom" : y < 2 * height / 3.0 ? "middle" : "top";
            return vertical == "middle" && horizontal == "centre" ? "centre" : vertical + " " + horizontal;
        }

        private static List<object> SpawnList(EntitySpawnPointsEntry[] spawns, int firstGlobalId, Cell[,] cells, string glyph, Dictionary<string, string> glyphs)
        {
            List<object> list = new List<object>();
            for (int i = 0; i < spawns.Length; i++)
            {
                Vector2Int p = spawns[i].Point;
                Cell cell = cells[p.x, p.y];
                glyphs[p.x + "," + p.y] = spawns[i].type == TroopSpawnPointType.Defence ? glyph.ToLowerInvariant() : glyph;
                list.Add(new
                {
                    index = i,
                    globalId = firstGlobalId + i,
                    x = p.x,
                    y = p.y,
                    spoken = cell.Spoken,
                    type = spawns[i].type.ToString(),
                    elevation = cell.Elevation
                });
            }

            return list;
        }

        private static string Ascii(Cell[,] cells, int width, int height, Dictionary<string, string> spawnGlyphs)
        {
            List<string> rows = new List<string>();
            for (int y = height - 1; y >= 0; y--)
            {
                string row = (y & 1) == 1 ? " " : string.Empty;
                for (int x = 0; x < width; x++)
                {
                    Cell c = cells[x, y];
                    string glyph;
                    string spawn;
                    if (spawnGlyphs != null && spawnGlyphs.TryGetValue(x + "," + y, out spawn))
                    {
                        glyph = spawn;
                    }
                    else if (!c.OnGrid)
                    {
                        glyph = " ";
                    }
                    else if (c.Water)
                    {
                        glyph = "~";
                    }
                    else if (c.Impassable)
                    {
                        glyph = "#";
                    }
                    else
                    {
                        glyph = c.Elevation == 0 ? "." : c.Elevation.ToString(CultureInfo.InvariantCulture);
                    }

                    row += glyph + " ";
                }

                rows.Add(row.TrimEnd());
            }

            return string.Join("\n", rows.ToArray());
        }

        private static string WhenChosen(LevelType type)
        {
            switch (type)
            {
                case LevelType.Battle_Plains:
                    return "A field battle on open ground: more than 85% of the adventure tiles within two of the defender are plain, or the fallback when no other type qualifies.";
                case LevelType.Battle_Woods:
                    return "A field battle in forest: more than a third of the adventure tiles within two of the defender are trees.";
                case LevelType.Battle_Hills:
                    return "A field battle in hills: more than a quarter of the adventure tiles within two of the defender are mountains or two or more elevation steps from the battle tile.";
                case LevelType.Battle_Water:
                    return "A field battle beside water: more than 15% of the adventure tiles within two of the defender are water.";
                case LevelType.Battle_Walls:
                    return "A field battle beside walls: more than 5% of the adventure tiles within two of the defender are wall decorations.";
                case LevelType.Battle:
                    return "The generic field battle, used when no terrain-specific type has a layout.";
                case LevelType.BattleTownSiege:
                    return "A siege of a town without walls.";
                case LevelType.BattleTownSiege_Walls:
                    return "A siege of a walled town: the defender holds walls, towers and a gate.";
                case LevelType.BattleSmallSettlementSiege:
                    return "A siege of a small settlement.";
                case LevelType.BattleLargeSettlementSiege:
                    return "A siege of a large settlement.";
                case LevelType.BattleCampaign:
                    return "A scripted campaign battle, pinned by name by the hostile army that uses it.";
                default:
                    return type.ToString();
            }
        }

        private static string DecorationName(LevelType type, int decoration)
        {
            if (decoration == 0)
            {
                return null;
            }

            if (type.IsSiege())
            {
                switch (decoration)
                {
                    case 6: return "tower";
                    case 7: return "wall";
                    case 8: return "stairs";
                }
            }

            return IsPreviewBlocker(decoration) ? "impassable" : null;
        }

        private static bool IsPreviewBlocker(int decoration)
        {
            return decoration == 4 || decoration == 9 || decoration == 10;
        }

        private static string Spoken(int x, int y)
        {
            return HexCoordinateFormatter.Format(new Vector2Int(x, y));
        }

        private static int At(byte[] array, int index)
        {
            return array != null && array.Length > index ? array[index] : 0;
        }

        private static int[] Range(EntitySpawnPointsEntry[] spawns, Func<EntitySpawnPointsEntry, int> select)
        {
            return spawns.Length == 0 ? new int[0] : new[] { spawns.Min(select), spawns.Max(select) };
        }

        private static FieldInfo Field(Type type, string name)
        {
            FieldInfo field = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (field == null)
            {
                throw new MissingFieldException(type.Name, name);
            }

            return field;
        }

        private sealed class Services : IDisposable
        {
            public IInternalLevelSerializer Levels;
            public IMapEntityManifestRetriever Manifests;
            public IObjectSerializer Objects;
            public CartographyManifest Manifest;
            public DeploymentRenderer Renderer;
            private Camera _camera;

            public static Services Resolve()
            {
                DiContainer project = ProjectContext.Instance.Container;
                Services services = new Services
                {
                    Levels = project.TryResolve<IInternalLevelSerializer>(),
                    Manifests = project.TryResolve<IMapEntityManifestRetriever>(),
                    Objects = project.TryResolve<IObjectSerializer>(),
                    Manifest = CartographyManifestLoader.Instance != null ? CartographyManifestLoader.Instance.BattleManifest : null
                };
                if (services.Levels == null || services.Manifests == null || services.Objects == null)
                {
                    throw new InvalidOperationException("project container lacks level serializer, manifest retriever or object serializer");
                }

                // The deployment installer is a ScriptableObjectInstaller on a GameObjectContext
                // (the battle menu's), so neither the project nor the scene container sees it.
                IDeploymentUIControllerFactory factory = DevFixtures.ResolveFromScenes<IDeploymentUIControllerFactory>()
                    ?? SongsOfConquestAccess.Screens.SceneSubContainers.Resolve<IDeploymentUIControllerFactory>();
                if (factory == null)
                {
                    throw new InvalidOperationException("no IDeploymentUIControllerFactory in any loaded scene or sub-container; open the adventure map first");
                }

                DeploymentUIControllerSettings settings = Field(typeof(DeploymentUIControllerFactory), "_settings").GetValue(factory) as DeploymentUIControllerSettings;
                if (settings == null || settings.rendererSettings == null)
                {
                    throw new InvalidOperationException("deployment factory has no renderer settings");
                }

                services.Renderer = new DeploymentRenderer(
                    settings.rendererSettings,
                    new DeploymentTeam { Id = 1, Color = AttackerColor, ColorIndex = 0 },
                    new DeploymentTeam { Id = 2, Color = DefenderColor, ColorIndex = 1 });
                // The game's own renderer parks its canvas at z = -1024; this one goes elsewhere so
                // neither camera sees the other's meshes (the live page's troop pucks showed up in
                // the first dump).
                Canvas canvas = Field(typeof(DeploymentRenderer), "_canvas").GetValue(services.Renderer) as Canvas;
                if (canvas != null)
                {
                    canvas.transform.position = new Vector3(0f, -4096f, -4096f);
                }

                services._camera = Field(typeof(DeploymentRenderer), "_camera").GetValue(services.Renderer) as Camera;
                if (services._camera == null)
                {
                    throw new InvalidOperationException("deployment renderer has no camera");
                }

                return services;
            }

            /// <summary>The preview at half size as a JPEG: the game's texture is 1920 by 1080 and
            /// a PNG of it is 1.4 MB, which 83 layouts make 113 MB; the picture is for describing a
            /// 13 by 9 grid, which 960 by 540 shows in full.</summary>
            public byte[] RenderJpg()
            {
                RenderTexture target = Renderer.texture;
                _camera.Render();
                RenderTexture small = RenderTexture.GetTemporary(target.width / 2, target.height / 2, 0);
                RenderTexture previous = RenderTexture.active;
                Texture2D texture = new Texture2D(small.width, small.height, TextureFormat.RGB24, false);
                try
                {
                    Graphics.Blit(target, small);
                    RenderTexture.active = small;
                    texture.ReadPixels(new Rect(0, 0, small.width, small.height), 0, 0);
                    texture.Apply();
                    return texture.EncodeToJPG(90);
                }
                finally
                {
                    RenderTexture.active = previous;
                    RenderTexture.ReleaseTemporary(small);
                    UnityEngine.Object.Destroy(texture);
                }
            }

            public void Dispose()
            {
                if (Renderer != null)
                {
                    Renderer.Dispose();
                    Renderer = null;
                }
            }
        }
    }
}
