using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Deployment;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Client.Gamestate.Facade;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.Menu.Tooltip;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common;
using SongsOfConquest.Common.Battle;
using SongsOfConquest.Common.Details;
using SongsOfConquest.Common.Entities.Adventure;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Gamestate.Facade;
using SongsOfConquest.Common.Localization;
using SongsOfConquest.Common.Map;
using SongsOfConquest.Server.Map;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Scanner;
using SongsOfConquestAccess.Speech;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class PreBattleMenuAdapter
    {
        private static readonly FieldInfo GridContainerField = AccessTools.Field(typeof(PreBattleMenu), "_gridContainer");
        private static readonly FieldInfo MainContainerField = AccessTools.Field(typeof(PreBattleMenu), "_mainContainer");
        private static readonly FieldInfo CancelButtonField = AccessTools.Field(typeof(PreBattleMenu), "_cancelButton");
        private static readonly FieldInfo QuickButtonField = AccessTools.Field(typeof(PreBattleMenu), "_quickButton");
        private static readonly FieldInfo BattleButtonField = AccessTools.Field(typeof(PreBattleMenu), "_battleButton");
        private static readonly FieldInfo ReadyButtonField = AccessTools.Field(typeof(PreBattleMenu), "_readyButton");
        private static readonly FieldInfo InstructionsTextField = AccessTools.Field(typeof(PreBattleMenu), "_instructionsText");
        private static readonly FieldInfo DeploymentRawImageField = AccessTools.Field(typeof(PreBattleMenu), "_deploymentRawImage");
        private static readonly FieldInfo TooltipField = AccessTools.Field(typeof(PreBattleMenu), "_tooltip");
        private static readonly FieldInfo AttackerBannerField = AccessTools.Field(typeof(PreBattleMenu), "_attackerBanner");
        private static readonly FieldInfo DefenderBannerField = AccessTools.Field(typeof(PreBattleMenu), "_defenderBanner");
        private static readonly FieldInfo AttackerScoutingInformationField = AccessTools.Field(typeof(PreBattleMenu), "_attackerScoutingInformation");
        private static readonly FieldInfo AttackerThreatLevelField = AccessTools.Field(typeof(PreBattleMenu), "_attackerThreatLevel");
        private static readonly FieldInfo DefenderScoutingInformationField = AccessTools.Field(typeof(PreBattleMenu), "_defenderScoutingInformation");
        private static readonly FieldInfo DefenderThreatLevelField = AccessTools.Field(typeof(PreBattleMenu), "_defenderThreatLevel");
        private static readonly FieldInfo LocalizationField = AccessTools.Field(typeof(PreBattleMenu), "_localizationHandler");
        private static readonly FieldInfo AdventureFacadeField = AccessTools.Field(typeof(PreBattleMenu), "_adventureFacade");
        private static readonly FieldInfo FactionLookupField = AccessTools.Field(typeof(PreBattleMenu), "_factionLookup");
        private static readonly FieldInfo DeploymentMenuField = AccessTools.Field(typeof(PreBattleMenu), "_deploymentMenu");
        private static readonly FieldInfo MapFormatField = AccessTools.Field(typeof(PreBattleMenu), "_mapFormat");
        private static readonly FieldInfo StateMachineField = AccessTools.Field(typeof(PreBattleMenu), "_stateMachine");
        private static readonly FieldInfo AttackingCommanderField = AccessTools.Field(typeof(PreBattleMenu), "_attackingCommander");
        private static readonly FieldInfo DefendingCommanderField = AccessTools.Field(typeof(PreBattleMenu), "_defendingCommander");

        private static readonly FieldInfo DeploymentControllerField = AccessTools.Field(typeof(DeploymentMenu), "_deploymentUIController");
        private static readonly FieldInfo CurrentContainerField = AccessTools.Field(typeof(DeploymentUIController), "_currentContainer");
        private static readonly FieldInfo DeploymentRendererField = AccessTools.Field(typeof(DeploymentUIController), "_renderer");
        private static readonly FieldInfo DeploymentRendererCameraField = AccessTools.Field(typeof(DeploymentRenderer), "_camera");
        private static readonly FieldInfo DeploymentRendererCanvasField = AccessTools.Field(typeof(DeploymentRenderer), "_canvas");
        private static readonly FieldInfo DeploymentAttackerField = AccessTools.Field(typeof(DeploymentMenu), "_attacker");
        private static readonly FieldInfo DeploymentDefenderField = AccessTools.Field(typeof(DeploymentMenu), "_defender");
        private static readonly FieldInfo AttackerSpawnPointsField = AccessTools.Field(typeof(DeploymentMenu), "_attackerSpawnPoints");
        private static readonly FieldInfo DefenderSpawnPointsField = AccessTools.Field(typeof(DeploymentMenu), "_defenderSpawnPoints");
        private static readonly MethodInfo GrabMethod = AccessTools.Method(typeof(DeploymentUIController), "Grab");
        private static readonly MethodInfo DropMethod = AccessTools.Method(typeof(DeploymentUIController), "Drop");

        private readonly PreBattleMenu _menu;
        private GameObject _cursorOverlay;
        private RectTransform[] _cursorOverlaySegments;

        public PreBattleMenuAdapter(PreBattleMenu menu)
        {
            _menu = menu;
        }

        public object SourceKey
        {
            get { return _menu; }
        }

        public bool IsPresent()
        {
            if (_menu == null || _menu.gameObject == null || !_menu.gameObject.activeInHierarchy)
            {
                return false;
            }

            GameObject main = GetField<GameObject>(MainContainerField);
            GameObject grid = GetField<GameObject>(GridContainerField);
            return main != null
                && main.activeInHierarchy
                && grid != null
                && grid.activeInHierarchy
                && GetDeploymentMenu() != null
                && GetMap() != null;
        }

        public string OurWielderText
        {
            get
            {
                BattleSide? ownSide = GetOwnSide();
                ICommanderState commander = ownSide == BattleSide.Right_Defender
                    ? GetField<ICommanderState>(DefendingCommanderField)
                    : GetField<ICommanderState>(AttackingCommanderField);
                return commander != null
                    ? GetCommanderName(commander, ModText.Get(ModStrings.Screens.YourWielder))
                    : ModText.Get(ModStrings.Screens.YourWielder);
            }
        }

        public string OpponentText
        {
            get
            {
                BattleSide? ownSide = GetOwnSide();
                bool opponentIsAttacker = ownSide == BattleSide.Right_Defender;
                ICommanderState commander = opponentIsAttacker
                    ? GetField<ICommanderState>(AttackingCommanderField)
                    : GetField<ICommanderState>(DefendingCommanderField);

                string name = commander != null
                    ? GetCommanderName(commander, ModText.Get(ModStrings.Screens.Opponent))
                    : ModText.Get(ModStrings.Screens.Opponent);
                string scouting = opponentIsAttacker
                    ? GetUIText(AttackerScoutingInformationField)
                    : GetUIText(DefenderScoutingInformationField);
                string threat = opponentIsAttacker
                    ? GetUIText(AttackerThreatLevelField)
                    : GetUIText(DefenderThreatLevelField);
                return MenuButtonTextUtility.JoinParts(name, scouting, threat);
            }
        }

        public string InstructionText
        {
            get { return GetUIText(InstructionsTextField); }
        }

        public void FocusOurWielder()
        {
            FocusTile(null);
        }

        public void FocusOpponent()
        {
            FocusTile(null);
        }

        public Tooltip OurWielderTooltip
        {
            get { return GetCommanderTooltip(own: true); }
        }

        public string WithdrawButtonLabel { get { return GetButtonLabel(CancelButtonField, "Withdraw"); } }
        public bool Withdraw() { return ActivateButton(CancelButtonField); }
        public void FocusWithdrawButton() { FocusButton(CancelButtonField); }
        public bool IsWithdrawButtonEnabled() { return IsButtonEnabled(CancelButtonField); }
        public bool IsWithdrawButtonVisible() { return IsButtonVisible(CancelButtonField); }
        public Tooltip WithdrawButtonTooltip { get { return GetButtonTooltip(CancelButtonField); } }

        public string ManualBattleButtonLabel { get { return GetButtonLabel(BattleButtonField, "Manual battle"); } }
        public bool ManualBattle() { return ActivateButton(BattleButtonField); }
        public void FocusManualBattleButton() { FocusButton(BattleButtonField); }
        public bool IsManualBattleButtonEnabled() { return IsButtonEnabled(BattleButtonField); }
        public bool IsManualBattleButtonVisible() { return IsButtonVisible(BattleButtonField); }
        public Tooltip ManualBattleButtonTooltip { get { return GetButtonTooltip(BattleButtonField); } }

        public string QuickBattleButtonLabel { get { return GetButtonLabel(QuickButtonField, "Quick battle"); } }
        public bool QuickBattle() { return ActivateButton(QuickButtonField); }
        public void FocusQuickBattleButton() { FocusButton(QuickButtonField); }
        public bool IsQuickBattleButtonEnabled() { return IsButtonEnabled(QuickButtonField); }
        public bool IsQuickBattleButtonVisible() { return IsButtonVisible(QuickButtonField); }
        public Tooltip QuickBattleButtonTooltip { get { return GetButtonTooltip(QuickButtonField); } }

        public string ReadyButtonLabel { get { return GetButtonLabel(ReadyButtonField, "Ready"); } }
        public bool Ready() { return ActivateButton(ReadyButtonField); }
        public void FocusReadyButton() { FocusButton(ReadyButtonField); }
        public bool IsReadyButtonEnabled() { return IsButtonEnabled(ReadyButtonField); }
        public bool IsReadyButtonVisible() { return IsButtonVisible(ReadyButtonField); }
        public Tooltip ReadyButtonTooltip { get { return GetButtonTooltip(ReadyButtonField); } }

        public TroopPlacementSnapshot BuildSnapshot()
        {
            MapFormat map = GetMap();
            DeploymentMenu deployment = GetDeploymentMenu();
            TroopPlacementSnapshot snapshot = new TroopPlacementSnapshot(map != null ? map.Metadata.Size : Vector2Int.zero, GetOwnSide(), this);
            if (map == null || deployment == null)
            {
                return snapshot;
            }

            AddTiles(snapshot, map);
            AddSpawnPoints(snapshot, deployment, BattleSide.Left_Attacker);
            AddSpawnPoints(snapshot, deployment, BattleSide.Right_Defender);
            AddTroops(snapshot, deployment, BattleSide.Left_Attacker);
            AddTroops(snapshot, deployment, BattleSide.Right_Defender);
            AddMapEntities(snapshot, map);
            return snapshot;
        }

        public ScannerSnapshot BuildScannerSnapshot(Vector2Int origin)
        {
            TroopPlacementSnapshot placement = BuildSnapshot();
            ScannerSnapshot snapshot = new ScannerSnapshot();
            InitializeTroopPlacementScannerCategories(snapshot);
            AddTroopPlacementTroopScannerResults(snapshot, placement);
            AddTroopPlacementSpawnScannerResults(snapshot, placement);
            AddTroopPlacementTerrainScannerResults(snapshot, placement);
            return snapshot;
        }

        private static void InitializeTroopPlacementScannerCategories(ScannerSnapshot snapshot)
        {
            ScannerCategory troops = snapshot.GetOrAddCategory("Troops");
            troops.GetOrAddSubcategory("All");
            troops.GetOrAddSubcategory("Friendly");
            troops.GetOrAddSubcategory("Enemy");

            ScannerCategory spawnPoints = snapshot.GetOrAddCategory("Spawn points");
            spawnPoints.GetOrAddSubcategory("All");
            spawnPoints.GetOrAddSubcategory("Friendly");
            spawnPoints.GetOrAddSubcategory("Enemy");

            ScannerCategory terrain = snapshot.GetOrAddCategory("Terrain");
            terrain.GetOrAddSubcategory("Elevated ground 1");
            terrain.GetOrAddSubcategory("Elevated ground 2");
            terrain.GetOrAddSubcategory("Elevated ground 3");
            terrain.GetOrAddSubcategory("Impassable terrain");
        }

        private void AddTroopPlacementTroopScannerResults(ScannerSnapshot snapshot, TroopPlacementSnapshot placement)
        {
            AddTroopPlacementTroopScannerResults(snapshot, placement, own: true);
            AddTroopPlacementTroopScannerResults(snapshot, placement, own: false);
        }

        private void AddTroopPlacementSpawnScannerResults(ScannerSnapshot snapshot, TroopPlacementSnapshot placement)
        {
            AddTroopPlacementSpawnScannerResults(snapshot, placement, own: true);
            AddTroopPlacementSpawnScannerResults(snapshot, placement, own: false);
        }

        private void AddTroopPlacementTerrainScannerResults(ScannerSnapshot snapshot, TroopPlacementSnapshot placement)
        {
            for (int elevation = 1; elevation <= 3; elevation++)
            {
                foreach (TroopPlacementTile tile in placement.Tiles)
                {
                    if (tile.Elevation == elevation)
                    {
                        ScannerResult result = new ScannerResult(
                            ScannerTileKey("terrain:elevated:" + elevation, tile.Point),
                            "elevated ground, height " + elevation,
                            tile.Point)
                        {
                            Kind = ScannerResultKind.TerrainPoint
                        };
                        snapshot.Add("Terrain", "Elevated ground " + elevation, result);
                    }
                }
            }

            foreach (TroopPlacementTile tile in placement.Tiles)
            {
                if (tile.IsImpassable)
                {
                    ScannerResult result = new ScannerResult(
                        ScannerTileKey("terrain:impassable", tile.Point),
                        "impassable",
                        tile.Point)
                    {
                        Kind = ScannerResultKind.TerrainPoint
                    };
                    snapshot.Add("Terrain", "Impassable terrain", result);
                }
            }
        }

        private void AddTroopPlacementTroopScannerResults(ScannerSnapshot snapshot, TroopPlacementSnapshot placement, bool own)
        {
            foreach (TroopPlacementTile tile in placement.Tiles)
            {
                if (tile.TroopSide.HasValue && IsOwnSide(placement, tile.TroopSide.Value) == own)
                {
                    ScannerResult result = new ScannerResult(
                        ScannerTileKey(own ? "troop:friendly" : "troop:enemy", tile.Point),
                        FirstNonEmpty(tile.TroopLabel, "Unknown troop"),
                        tile.Point);
                    snapshot.Add("Troops", "All", CloneResult(result));
                    snapshot.Add("Troops", own ? "Friendly" : "Enemy", result);
                }
            }
        }

        private void AddTroopPlacementSpawnScannerResults(ScannerSnapshot snapshot, TroopPlacementSnapshot placement, bool own)
        {
            foreach (TroopPlacementTile tile in placement.Tiles)
            {
                if (tile.SpawnSide.HasValue && IsOwnSide(placement, tile.SpawnSide.Value) == own)
                {
                    ScannerResult result = new ScannerResult(
                        ScannerTileKey(own ? "spawn:friendly" : "spawn:enemy", tile.Point),
                        "spawn point",
                        tile.Point);
                    snapshot.Add("Spawn points", "All", CloneResult(result));
                    snapshot.Add("Spawn points", own ? "Friendly" : "Enemy", result);
                }
            }
        }

        private static string ScannerTileKey(string prefix, Vector2Int point)
        {
            return prefix + ":" + point.x + ":" + point.y;
        }

        private static ScannerResult CloneResult(ScannerResult result)
        {
            ScannerResult clone = new ScannerResult(result.Key, result.Label, result.Position)
            {
                NotVisible = result.NotVisible,
                StableReference = result.StableReference,
                Kind = result.Kind
            };
            clone.Points.AddRange(result.Points);
            return clone;
        }

        public bool ValidateScannerResult(ScannerResult result)
        {
            return result != null && BuildSnapshot().IsValidTile(result.Position);
        }

        public bool TryMoveTroop(Vector2Int source, Vector2Int destination)
        {
            DeploymentMenu deployment = GetDeploymentMenu();
            if (deployment == null || DeploymentControllerField == null || GrabMethod == null || DropMethod == null)
            {
                SocAccessPlugin.Instance?.LogWarning("PreBattleMenuAdapter could not resolve native deployment grab/drop methods");
                return false;
            }

            object controller = DeploymentControllerField.GetValue(deployment);
            if (controller == null)
            {
                return false;
            }

            string before = BuildPlacementSignature(deployment);
            try
            {
                GrabMethod.Invoke(controller, new object[] { new int2(source.x, source.y) });
                if (CurrentContainerField != null && CurrentContainerField.GetValue(controller) == null)
                {
                    return false;
                }

                DropMethod.Invoke(controller, new object[] { (int2?)new int2(destination.x, destination.y) });
            }
            catch (Exception ex)
            {
                SocAccessPlugin.Instance?.LogWarning("PreBattleMenuAdapter native drag/drop failed: " + ex.Message);
                return false;
            }

            string after = BuildPlacementSignature(deployment);
            return !string.Equals(before, after, StringComparison.Ordinal);
        }

        public void FocusTile(TroopPlacementTile tile)
        {
            DeploymentMenu deployment = GetDeploymentMenu();
            if (deployment == null)
            {
                return;
            }

            Action<OnTroopHoveredPayload> hovered = deployment.OnTroopHovered;
            if (hovered == null)
            {
                return;
            }

            if (tile != null && tile.Troop != null)
            {
                hovered(new OnTroopHoveredPayload
                {
                    Troop = tile.Troop,
                    DetailsAreHidden = tile.TroopDetailsHidden
                });
                return;
            }

            hovered(default(OnTroopHoveredPayload));
        }

        public void HideNativeTooltip()
        {
            FocusTile(null);
        }

        public void AddDeploymentChangedHandler(Action<OnChangedPayload> handler)
        {
            DeploymentMenu deployment = GetDeploymentMenu();
            if (deployment == null || handler == null)
            {
                return;
            }

            deployment.OnChanged = (Action<OnChangedPayload>)Delegate.Combine(deployment.OnChanged, handler);
        }

        public void RemoveDeploymentChangedHandler(Action<OnChangedPayload> handler)
        {
            DeploymentMenu deployment = GetDeploymentMenu();
            if (deployment == null || handler == null)
            {
                return;
            }

            deployment.OnChanged = (Action<OnChangedPayload>)Delegate.Remove(deployment.OnChanged, handler);
        }

        public void SetFocusedTileOverlay(Vector2Int tile)
        {
            if (!IsPresent())
            {
                return;
            }

            try
            {
                EnsureCursorOverlay();
                if (_cursorOverlay == null || _cursorOverlaySegments == null)
                {
                    return;
                }

                SetScreenOverlayPosition(GetScreenPoint(tile));
                _cursorOverlay.SetActive(true);
            }
            catch (Exception exception)
            {
                SocAccessPlugin.Instance?.LogWarning("PreBattleMenuAdapter failed to set focused tile overlay: " + exception.Message);
            }
        }

        public void ClearFocusedTileOverlay()
        {
            if (_cursorOverlay == null)
            {
                return;
            }

            try
            {
                UnityEngine.Object.Destroy(_cursorOverlay);
                _cursorOverlay = null;
                _cursorOverlaySegments = null;
            }
            catch (Exception exception)
            {
                SocAccessPlugin.Instance?.LogWarning("PreBattleMenuAdapter failed to clear focused tile overlay: " + exception.Message);
            }
        }

        public Tooltip GetTileTooltip(TroopPlacementTile tile)
        {
            if (tile == null || tile.Troop == null || tile.TroopDetailsHidden)
            {
                return null;
            }

            IDetails details = GetTroopDetails(tile.Troop.Id);
            TooltipBehaviour tooltipBehaviour = GetField<TooltipBehaviour>(TooltipField);
            ILocalizationHandler localization = GetLocalization();
            if (details == null || tooltipBehaviour == null || localization == null)
            {
                return null;
            }

            Vector2 screenPoint = GetScreenPoint(tile.Point);
            return new Tooltip(
                () => NativeTooltipUtility.ToSpeechLines(details, localization),
                new VisualTooltipMetadata(tooltipBehaviour, screenPoint, details));
        }

        private Tooltip GetCommanderTooltip(bool own)
        {
            ICommanderState commander = GetCommanderForParticipant(own);
            IClientAdventureFacade facade = GetField<IClientAdventureFacade>(AdventureFacadeField);
            TooltipBehaviour tooltipBehaviour = GetField<TooltipBehaviour>(TooltipField);
            ILocalizationHandler localization = GetLocalization();
            if (commander == null || commander.GetIsEmpty() || facade == null || tooltipBehaviour == null || localization == null)
            {
                return null;
            }

            IDetails details;
            try
            {
                details = facade.Commanders.GetDetails(commander.Id);
            }
            catch (Exception ex)
            {
                SocAccessPlugin.Instance?.LogWarning("PreBattleMenuAdapter failed to resolve commander tooltip: " + ex.Message);
                return null;
            }

            if (details == null)
            {
                return null;
            }

            Component anchor = GetCommanderBannerComponent(own);
            Vector2 screenPoint = GetComponentCenterScreenPoint(anchor);
            return new Tooltip(
                () => NativeTooltipUtility.ToSpeechLines(details, localization),
                new VisualTooltipMetadata(tooltipBehaviour, screenPoint, details));
        }

        private ICommanderState GetCommanderForParticipant(bool own)
        {
            BattleSide? ownSide = GetOwnSide();
            bool useAttacker = own
                ? ownSide != BattleSide.Right_Defender
                : ownSide == BattleSide.Right_Defender;
            return useAttacker
                ? GetField<ICommanderState>(AttackingCommanderField)
                : GetField<ICommanderState>(DefendingCommanderField);
        }

        private Component GetCommanderBannerComponent(bool own)
        {
            BattleSide? ownSide = GetOwnSide();
            bool useAttacker = own
                ? ownSide != BattleSide.Right_Defender
                : ownSide == BattleSide.Right_Defender;
            return GetField<Banner>(useAttacker ? AttackerBannerField : DefenderBannerField);
        }

        public bool CanResolveTile(Vector2Int tile)
        {
            DeploymentRenderer renderer = GetDeploymentRenderer();
            if (renderer == null)
            {
                return true;
            }

            float3 ignored;
            return renderer.PointToWorld(new int2(tile.x, tile.y), out ignored);
        }

        private string GetButtonLabel(FieldInfo field, string fallbackLabel)
        {
            string label = MenuButtonTextUtility.GetStandardButtonLabel(GetField<UIButton>(field));
            return string.IsNullOrWhiteSpace(label) ? fallbackLabel : label;
        }

        private bool ActivateButton(FieldInfo field)
        {
            return NativeSelectionUtility.Click(GetField<UIButton>(field));
        }

        private void FocusButton(FieldInfo field)
        {
            HideNativeTooltip();
            NativeSelectionUtility.Select(GetField<UIButton>(field));
        }

        private bool IsButtonEnabled(FieldInfo field)
        {
            UIButton button = GetField<UIButton>(field);
            return button == null || button.Interactable;
        }

        private bool IsButtonVisible(FieldInfo field)
        {
            UIButton button = GetField<UIButton>(field);
            return button != null && button.Active;
        }

        private Tooltip GetButtonTooltip(FieldInfo field)
        {
            return Tooltip.ForComponent(GetField<UIButton>(field), GetLocalization());
        }

        private void AddTiles(TroopPlacementSnapshot snapshot, MapFormat map)
        {
            for (int y = 0; y < snapshot.Size.y; y++)
            {
                for (int x = 0; x < snapshot.Size.x; x++)
                {
                    Vector2Int point = new Vector2Int(x, y);
                    if (!IsGridTile(map, point))
                    {
                        continue;
                    }

                    TroopPlacementTile tile = snapshot.GetOrCreate(point);
                    int index = map.PointToIndex(point);
                    byte[] elevations = map.Contents.ElevationsArray;
                    byte[] decorations = map.Contents.DecorationsArray;
                    byte[] water = map.Contents.WaterArray;
                    tile.Elevation = elevations != null && index < elevations.Length ? elevations[index] : (byte)0;
                    tile.IsImpassable = (water != null && index < water.Length && water[index] != 0)
                        || (decorations != null && index < decorations.Length && IsBlocker(decorations[index]));
                }
            }
        }

        private void AddSpawnPoints(TroopPlacementSnapshot snapshot, DeploymentMenu deployment, BattleSide side)
        {
            EntitySpawnPointsEntry[] entries = GetSpawnPoints(deployment, side);
            if (entries == null)
            {
                return;
            }

            for (int i = 0; i < entries.Length; i++)
            {
                Vector2Int point = entries[i].Point;
                if (!snapshot.IsValidTile(point))
                {
                    continue;
                }

                TroopPlacementTile tile = snapshot.GetOrCreate(point);
                tile.SpawnSide = side;
                tile.SpawnPointId = i;
                tile.GlobalSpawnPointId = side == BattleSide.Left_Attacker
                    ? i
                    : GetAttackerSpawnCount(deployment) + i;
            }
        }

        private void AddTroops(TroopPlacementSnapshot snapshot, DeploymentMenu deployment, BattleSide side)
        {
            if (!ShouldShowSide(side))
            {
                return;
            }

            DeploymentTeam team = GetDeploymentTeam(deployment, side);
            BattleTroopPlacementDefinition[] placements = side == BattleSide.Left_Attacker
                ? deployment.AttackerPlacements
                : deployment.DefenderPlacements;
            if (team == null || team.Troops == null || placements == null)
            {
                return;
            }

            EntitySpawnPointsEntry[] spawnPoints = GetSpawnPoints(deployment, side);
            bool detailsHidden = ShouldHideDetails(side);
            for (int i = 0; i < placements.Length; i++)
            {
                BattleTroopPlacementDefinition placement = placements[i];
                if (placement == null || spawnPoints == null || placement.SpawnpointId < 0 || placement.SpawnpointId >= spawnPoints.Length)
                {
                    continue;
                }

                ICommonTroopState troop = FindTroop(team.Troops, placement.TroopId);
                Vector2Int point = spawnPoints[placement.SpawnpointId].Point;
                TroopPlacementTile tile = snapshot.GetOrCreate(point);
                tile.TroopSide = side;
                tile.Troop = troop;
                tile.TroopId = placement.TroopId;
                tile.TroopDetailsHidden = detailsHidden;
                tile.TroopLabel = detailsHidden ? "Unknown" : BuildTroopLabel(troop);
            }
        }

        private void AddMapEntities(TroopPlacementSnapshot snapshot, MapFormat map)
        {
            if (map.Contents.MapEntities == null)
            {
                return;
            }

            for (int i = 0; i < map.Contents.MapEntities.Count; i++)
            {
                MapEntityFormat entity = map.Contents.MapEntities[i];
                if (entity == null || entity.Id == 2 || entity.Id == 3)
                {
                    continue;
                }

                Vector2Int point = new Vector2Int(entity.X, entity.Y);
                if (!snapshot.IsValidTile(point))
                {
                    continue;
                }

                string name = SpeechTextSanitizer.Normalize(entity.Name);
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                snapshot.GetOrCreate(point).EntityLabel = name;
            }
        }

        private string BuildTroopLabel(ICommonTroopState troop)
        {
            if (troop == null)
            {
                return string.Empty;
            }

            int size = troop.Stats != null ? troop.Stats.Size : 0;
            string name = string.Empty;
            try
            {
                IFactionLookup factionLookup = GetField<IFactionLookup>(FactionLookupField);
                ILocalizationHandler localization = GetLocalization();
                if (factionLookup != null && localization != null)
                {
                    name = localization.GetPluralTextGeneric(factionLookup.GetUnit(troop.Reference).NameKey, size);
                }
            }
            catch (Exception ex)
            {
                SocAccessPlugin.Instance?.LogWarning("PreBattleMenuAdapter failed to localize troop name: " + ex.Message);
            }

            name = SpeechTextSanitizer.Normalize(name);
            if (string.IsNullOrWhiteSpace(name))
            {
                name = "troops";
            }

            return size > 0 ? size + " " + name : name;
        }

        private static bool IsOwnSide(TroopPlacementSnapshot snapshot, BattleSide side)
        {
            return snapshot != null && snapshot.OwnSide.HasValue && snapshot.OwnSide.Value == side;
        }

        private static string FirstNonEmpty(string preferred, string fallback)
        {
            return string.IsNullOrWhiteSpace(preferred) ? fallback : preferred;
        }

        private bool ShouldShowSide(BattleSide side)
        {
            BattleSide? ownSide = GetOwnSide();
            if (!ownSide.HasValue || ownSide.Value == side)
            {
                return true;
            }

            DeploymentTeam team = GetDeploymentTeam(GetDeploymentMenu(), side);
            return team != null && team.ScoutingDetail != ScoutingDetailLevel.VeryFar;
        }

        private bool ShouldHideDetails(BattleSide side)
        {
            BattleSide? ownSide = GetOwnSide();
            if (!ownSide.HasValue || ownSide.Value == side)
            {
                return false;
            }

            DeploymentTeam team = GetDeploymentTeam(GetDeploymentMenu(), side);
            return team != null && team.ScoutingDetail == ScoutingDetailLevel.Close;
        }

        private BattleSide? GetOwnSide()
        {
            string state = GetCurrentStateName();
            if (state == "HostilePlacement"
                || state == "HotSeatAttackerPlacement"
                || state == "MultiplayerAttackerPlacement")
            {
                return BattleSide.Left_Attacker;
            }

            if (state == "HotSeatDefenderPlacement"
                || state == "MultiplayerDefenderPlacement")
            {
                return BattleSide.Right_Defender;
            }

            return null;
        }

        private string GetCurrentStateName()
        {
            object stateMachine = GetField<object>(StateMachineField);
            if (stateMachine == null)
            {
                return string.Empty;
            }

            PropertyInfo property = AccessTools.Property(stateMachine.GetType(), "CurrentStateType");
            object value = property != null ? property.GetValue(stateMachine, null) : null;
            return value != null ? value.ToString() : string.Empty;
        }

        private DeploymentTeam GetDeploymentTeam(DeploymentMenu deployment, BattleSide side)
        {
            if (deployment == null)
            {
                return null;
            }

            FieldInfo field = side == BattleSide.Left_Attacker ? DeploymentAttackerField : DeploymentDefenderField;
            return field != null ? field.GetValue(deployment) as DeploymentTeam : null;
        }

        private EntitySpawnPointsEntry[] GetSpawnPoints(DeploymentMenu deployment, BattleSide side)
        {
            if (deployment == null)
            {
                return null;
            }

            FieldInfo field = side == BattleSide.Left_Attacker ? AttackerSpawnPointsField : DefenderSpawnPointsField;
            return field != null ? field.GetValue(deployment) as EntitySpawnPointsEntry[] : null;
        }

        private int GetAttackerSpawnCount(DeploymentMenu deployment)
        {
            EntitySpawnPointsEntry[] spawns = GetSpawnPoints(deployment, BattleSide.Left_Attacker);
            return spawns != null ? spawns.Length : 0;
        }

        private DeploymentMenu GetDeploymentMenu()
        {
            return GetField<IDeploymentMenu>(DeploymentMenuField) as DeploymentMenu;
        }

        private MapFormat GetMap()
        {
            return GetField<MapFormat>(MapFormatField);
        }

        private RawImage GetDeploymentRawImage()
        {
            return GetField<RawImage>(DeploymentRawImageField);
        }

        private ILocalizationHandler GetLocalization()
        {
            return GetField<ILocalizationHandler>(LocalizationField);
        }

        private string GetCommanderName(ICommanderState commander, string fallback)
        {
            if (commander == null)
            {
                return fallback;
            }

            try
            {
                IClientAdventureFacade facade = GetField<IClientAdventureFacade>(AdventureFacadeField);
                string name = facade != null && facade.Commanders != null
                    ? facade.Commanders.GetName(commander.Id)
                    : string.Empty;
                name = SpeechTextSanitizer.Normalize(name);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    return name;
                }
            }
            catch (Exception ex)
            {
                SocAccessPlugin.Instance?.LogWarning("PreBattleMenuAdapter failed to resolve commander name: " + ex.Message);
            }

            return fallback;
        }

        private IDetails GetTroopDetails(int troopId)
        {
            try
            {
                IClientAdventureFacade facade = GetField<IClientAdventureFacade>(AdventureFacadeField);
                return facade != null && facade.Troops != null ? facade.Troops.GetDetails(troopId) : null;
            }
            catch (Exception ex)
            {
                SocAccessPlugin.Instance?.LogWarning("PreBattleMenuAdapter failed to resolve troop details: " + ex.Message);
                return null;
            }
        }

        private T GetField<T>(FieldInfo field) where T : class
        {
            return field != null && _menu != null ? field.GetValue(_menu) as T : null;
        }

        private string GetUIText(FieldInfo field)
        {
            UITextMesh text = GetField<UITextMesh>(field);
            if (!IsVisibleText(text))
            {
                return string.Empty;
            }

            return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(text));
        }

        private static bool IsVisibleText(UITextMesh text)
        {
            return text != null
                && text.Active
                && text.gameObject != null
                && text.gameObject.activeInHierarchy;
        }

        private static ICommonTroopState FindTroop(ICommonTroopState[] troops, int troopId)
        {
            if (troops == null)
            {
                return null;
            }

            for (int i = 0; i < troops.Length; i++)
            {
                if (troops[i] != null && troops[i].Id == troopId)
                {
                    return troops[i];
                }
            }

            return null;
        }

        private bool IsGridTile(MapFormat map, Vector2Int point)
        {
            if (map == null || !map.IsPointWithinMap(point))
            {
                return false;
            }

            return CanResolveTile(point);
        }

        private static bool IsBlocker(byte value)
        {
            return value == 4 || value == 9 || value == 10;
        }

        private void EnsureCursorOverlay()
        {
            if (_cursorOverlay != null && _cursorOverlaySegments != null)
            {
                return;
            }

            _cursorOverlay = new GameObject("SongsOfConquestAccess_TroopPlacementCursor");
            Canvas canvas = _cursorOverlay.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Keep the cursor above map visuals and the native overlay canvas
            // (29998), but below native tooltip canvases (30001) and windows.
            canvas.sortingOrder = 29999;
            CanvasScaler scaler = _cursorOverlay.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            CanvasGroup canvasGroup = _cursorOverlay.AddComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            _cursorOverlaySegments = new[]
            {
                CreateOverlaySegment("Top"),
                CreateOverlaySegment("Right"),
                CreateOverlaySegment("Bottom"),
                CreateOverlaySegment("Left")
            };
        }

        private RectTransform CreateOverlaySegment(string name)
        {
            GameObject segment = new GameObject(name);
            segment.transform.SetParent(_cursorOverlay.transform, false);
            Image image = segment.AddComponent<Image>();
            image.color = Color.yellow;
            image.raycastTarget = false;
            RectTransform rect = segment.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            return rect;
        }

        private void SetScreenOverlayPosition(Vector2 point)
        {
            const float size = 42f;
            const float thickness = 4f;
            if (_cursorOverlaySegments == null || _cursorOverlaySegments.Length != 4)
            {
                return;
            }

            SetSegment(_cursorOverlaySegments[0], point + new Vector2(0f, size * 0.5f), new Vector2(size, thickness));
            SetSegment(_cursorOverlaySegments[1], point + new Vector2(size * 0.5f, 0f), new Vector2(thickness, size));
            SetSegment(_cursorOverlaySegments[2], point + new Vector2(0f, -size * 0.5f), new Vector2(size, thickness));
            SetSegment(_cursorOverlaySegments[3], point + new Vector2(-size * 0.5f, 0f), new Vector2(thickness, size));
        }

        private static void SetSegment(RectTransform segment, Vector2 position, Vector2 size)
        {
            if (segment == null)
            {
                return;
            }

            segment.anchoredPosition = position;
            segment.sizeDelta = size;
        }

        private Vector2 GetScreenPoint(Vector2Int tile)
        {
            RawImage rawImage = GetDeploymentRawImage();
            DeploymentRenderer renderer = GetDeploymentRenderer();
            Camera camera = DeploymentRendererCameraField != null && renderer != null
                ? DeploymentRendererCameraField.GetValue(renderer) as Camera
                : null;
            if (rawImage == null || camera == null || renderer == null)
            {
                return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            }

            float3 localPoint;
            if (!renderer.PointToWorld(new int2(tile.x, tile.y), out localPoint))
            {
                return GetRawImageCenterScreenPoint(rawImage);
            }

            Vector3 cameraWorldPoint = ToDeploymentCameraWorldPoint(renderer, localPoint);
            Vector3 viewport = camera.WorldToViewportPoint(cameraWorldPoint);
            return RawImageViewportToScreenPoint(rawImage, new Vector2(viewport.x, viewport.y));
        }

        private DeploymentRenderer GetDeploymentRenderer()
        {
            DeploymentMenu deployment = GetDeploymentMenu();
            if (deployment == null || DeploymentControllerField == null || DeploymentRendererField == null)
            {
                return null;
            }

            object controller = DeploymentControllerField.GetValue(deployment);
            return controller != null ? DeploymentRendererField.GetValue(controller) as DeploymentRenderer : null;
        }

        private static Vector3 ToDeploymentCameraWorldPoint(DeploymentRenderer renderer, float3 localPoint)
        {
            Vector3 point = new Vector3(localPoint.x, localPoint.y, localPoint.z);
            if (renderer == null || DeploymentRendererCanvasField == null)
            {
                return point;
            }

            Canvas canvas = DeploymentRendererCanvasField.GetValue(renderer) as Canvas;
            return canvas != null ? canvas.transform.TransformPoint(point) : point;
        }

        private static Vector2 RawImageViewportToScreenPoint(RawImage rawImage, Vector2 viewport)
        {
            RectTransform rectTransform = rawImage != null ? rawImage.rectTransform : null;
            if (rectTransform == null)
            {
                return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            }

            Vector3[] corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            Vector2 topRight = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
            return new Vector2(
                Mathf.Lerp(bottomLeft.x, topRight.x, viewport.x),
                Mathf.Lerp(bottomLeft.y, topRight.y, viewport.y));
        }

        private static Vector2 GetComponentCenterScreenPoint(Component component)
        {
            RectTransform rectTransform = component != null ? component.GetComponent<RectTransform>() : null;
            if (rectTransform == null)
            {
                return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            }

            Vector3[] corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            Vector2 topRight = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
            return new Vector2(
                Mathf.Lerp(bottomLeft.x, topRight.x, 0.5f),
                Mathf.Lerp(bottomLeft.y, topRight.y, 0.5f));
        }

        private static Vector2 GetRawImageCenterScreenPoint(RawImage rawImage)
        {
            return RawImageViewportToScreenPoint(rawImage, new Vector2(0.5f, 0.5f));
        }

        private static string BuildPlacementSignature(DeploymentMenu deployment)
        {
            if (deployment == null)
            {
                return string.Empty;
            }

            return BuildPlacementSignature(deployment.AttackerPlacements)
                + "|"
                + BuildPlacementSignature(deployment.DefenderPlacements);
        }

        private static string BuildPlacementSignature(BattleTroopPlacementDefinition[] placements)
        {
            if (placements == null || placements.Length == 0)
            {
                return string.Empty;
            }

            List<string> parts = new List<string>(placements.Length);
            for (int i = 0; i < placements.Length; i++)
            {
                BattleTroopPlacementDefinition placement = placements[i];
                if (placement != null)
                {
                    parts.Add(placement.TroopId + ":" + placement.SpawnpointId);
                }
            }

            parts.Sort(StringComparer.Ordinal);
            return string.Join(",", parts.ToArray());
        }
    }

    internal sealed class TroopPlacementSnapshot
    {
        private readonly Dictionary<Vector2Int, TroopPlacementTile> _tiles = new Dictionary<Vector2Int, TroopPlacementTile>();

        private readonly PreBattleMenuAdapter _adapter;

        public TroopPlacementSnapshot(Vector2Int size, BattleSide? ownSide, PreBattleMenuAdapter adapter)
        {
            Size = size;
            OwnSide = ownSide;
            _adapter = adapter;
        }

        public Vector2Int Size { get; private set; }

        public BattleSide? OwnSide { get; private set; }

        public IEnumerable<TroopPlacementTile> Tiles
        {
            get { return _tiles.Values; }
        }

        public TroopPlacementTile Get(Vector2Int point)
        {
            TroopPlacementTile tile;
            return _tiles.TryGetValue(point, out tile) ? tile : null;
        }

        public TroopPlacementTile GetOrCreate(Vector2Int point)
        {
            TroopPlacementTile tile;
            if (!_tiles.TryGetValue(point, out tile))
            {
                tile = new TroopPlacementTile(point);
                _tiles.Add(point, tile);
            }

            return tile;
        }

        public bool IsValidTile(Vector2Int point)
        {
            return _tiles.ContainsKey(point) && (_adapter == null || _adapter.CanResolveTile(point));
        }

        public List<TroopPlacementTile> GetSpawnPoints(bool own)
        {
            List<TroopPlacementTile> result = new List<TroopPlacementTile>();
            foreach (TroopPlacementTile tile in _tiles.Values)
            {
                if (!tile.SpawnSide.HasValue)
                {
                    continue;
                }

                bool isOwn = OwnSide.HasValue && tile.SpawnSide.Value == OwnSide.Value;
                if (isOwn == own)
                {
                    result.Add(tile);
                }
            }

            result.Sort((left, right) =>
            {
                int idCompare = left.GlobalSpawnPointId.CompareTo(right.GlobalSpawnPointId);
                if (idCompare != 0)
                {
                    return idCompare;
                }

                int xCompare = left.Point.x.CompareTo(right.Point.x);
                return xCompare != 0 ? xCompare : left.Point.y.CompareTo(right.Point.y);
            });
            return result;
        }
    }

    internal sealed class TroopPlacementTile
    {
        public TroopPlacementTile(Vector2Int point)
        {
            Point = point;
            SpawnPointId = -1;
            GlobalSpawnPointId = -1;
            TroopId = -1;
        }

        public Vector2Int Point { get; private set; }

        public byte Elevation { get; set; }

        public bool IsImpassable { get; set; }

        public BattleSide? SpawnSide { get; set; }

        public int SpawnPointId { get; set; }

        public int GlobalSpawnPointId { get; set; }

        public BattleSide? TroopSide { get; set; }

        public int TroopId { get; set; }

        public ICommonTroopState Troop { get; set; }

        public bool TroopDetailsHidden { get; set; }

        public string TroopLabel { get; set; }

        public string EntityLabel { get; set; }
    }
}
