namespace SongsOfConquestAccess.Events
{
    internal static class AccessibilityEvents
    {
        public static class Map
        {
            public const string WielderSelected = "map.wielder.selected";
            public const string WielderMoved = "map.wielder.moved";
            public const string BuildSiteSelected = "map.build_site.selected";
            public const string DestinationSet = "map.destination.set";
            public const string DestinationCleared = "map.destination.cleared";
            public const string ActionFailed = "map.action.failed";
            public const string HudVisibilityChanged = "map.hud.visibility_changed";
            public const string CameraFocus = "map.camera_focus";
            public const string RoundChanged = "map.round.changed";
        }

        public static class Notification
        {
            public const string AdventureIcon = "notification.adventure.icon";
            public const string AdventureSimple = "notification.adventure.simple";
            public const string CommanderLevelUp = "notification.adventure.commander_level_up";
            public const string AdventureHud = "notification.adventure.hud";
            public const string Objective = "notification.adventure.objective";
            public const string AdventureNewTurn = "notification.adventure.new_turn";
            public const string WorldMessage = "notification.world.message";
            public const string Centered = "notification.centered";
            public const string CenteredHeavy = "notification.centered_heavy";
        }

        public static class Story
        {
            public const string CameraFocusStarted = "story.camera_focus.started";
        }

        public static class ArmyExchange
        {
            public const string InvalidDestination = "army_exchange.invalid_destination";
        }

        public static class Combat
        {
            public const string NewTurn = "combat.turn.new";
            public const string NewRound = "combat.round.new";
            public const string QueueChanged = "combat.queue.changed";
            public const string TroopMoved = "combat.troop.moved";
            public const string Attack = "combat.attack";
            public const string Damage = "combat.damage";
            public const string SpellCast = "combat.spell.cast";
            public const string FaeyFire = "combat.faey_fire";
            public const string BacteriaRemoved = "combat.bacteria.removed";
            public const string BacteriaModifierApplied = "combat.bacteria.modifier_applied";
            public const string BacteriaRemovedSummary = "combat.bacteria.removed_summary";
            public const string BacteriaModifierSummary = "combat.bacteria.modifier_summary";
            public const string EssenceGenerated = "combat.essence.generated";
            public const string TroopCreated = "combat.troop.created";
            public const string MapEntityCreated = "combat.map_entity.created";
            public const string MapEntityDestroyed = "combat.map_entity.destroyed";
            public const string TroopPushed = "combat.troop.pushed";
            public const string AbilityUsed = "combat.ability.used";
            public const string Teleport = "combat.teleport";
            public const string BurrowUp = "combat.burrow_up";
            public const string BattleResult = "combat.battle_result";
            public const string HudNotification = "combat.hud.notification";
        }
    }
}
