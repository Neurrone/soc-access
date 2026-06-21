using System.Collections.Generic;

namespace SongsOfConquestAccess.Speech.Spatial
{
    internal static class AnnouncementDefinitions
    {
        public static IReadOnlyList<AnnouncementGroupDefinition> All
        {
            get
            {
                List<AnnouncementGroupDefinition> groups = new List<AnnouncementGroupDefinition>();
                groups.AddRange(ScannerAnnouncementDefinitions.All);
                groups.AddRange(AdventureMapAnnouncementDefinitions.All);
                groups.AddRange(TroopDeploymentAnnouncementDefinitions.All);
                groups.AddRange(CombatAnnouncementDefinitions.All);
                return groups;
            }
        }
    }
}
