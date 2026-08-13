using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Speech.Spatial;

namespace SongsOfConquestAccess.Tests
{
    [TestClass]
    public sealed class ScannerSpeechFormatterTests
    {
        [TestMethod]
        public void ScannerResultSpeechFormatterUsesCommaSuffixes()
        {
            string text = ComposeWithDefaults(
                ScannerAnnouncementDefinitions.Result,
                new[]
                {
                    new AnnouncementPart(ScannerAnnouncementDefinitions.ResultKeys.Content, "20 Militia, spawn point"),
                    new AnnouncementPart(ScannerAnnouncementDefinitions.ResultKeys.Direction, "2 northeast"),
                    new AnnouncementPart(ScannerAnnouncementDefinitions.ResultKeys.Coordinates, "3, 4"),
                    new AnnouncementPart(ScannerAnnouncementDefinitions.ResultKeys.ResultPosition, "1 of 5")
                });

            Assert.AreEqual("20 Militia, spawn point, 2 northeast, 3, 4, 1 of 5", text);
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
