using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Audio;
using SongsOfConquestAccess.Screens;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Tests
{
    [TestClass]
    public sealed class AudioGlossaryScreenTests
    {
        [TestMethod]
        public void GlossaryListsEveryCueByNameAlone()
        {
            AudioGlossaryScreen screen = new AudioGlossaryScreen();

            MenuWidget menu = screen.RootWidget.GetChildById("audio-glossary-cues") as MenuWidget;

            Assert.IsNotNull(menu);
            for (int i = 0; i < CueLibrary.AllCues.Count; i++)
            {
                CueDefinition cue = CueLibrary.AllCues[i];
                Assert.IsTrue(menu.SetFocusedItemById("audio-glossary-cue-" + cue.Key), cue.Key);
                Assert.AreEqual(cue.Name.Text, menu.FocusedItem.GetLabel(), cue.Key);
                Assert.IsTrue(
                    string.IsNullOrEmpty(menu.FocusedItem.GetStatus()),
                    cue.Key + " must not read a category suffix");
            }
        }

        [TestMethod]
        public void GlossaryOffersConfigureAndBack()
        {
            AudioGlossaryScreen screen = new AudioGlossaryScreen();

            Assert.IsNotNull(screen.RootWidget.GetChildById("audio-glossary-configure"));
            Assert.IsNotNull(screen.RootWidget.GetChildById("audio-glossary-back"));
            Assert.IsTrue(screen.IsPresent());
        }

        [TestMethod]
        public void CueSettingsScreenExposesEveryTuningControl()
        {
            AudioCueSettingsScreen screen = new AudioCueSettingsScreen(CueLibrary.GetCue(CueLibrary.TerrainWater));

            string prefix = "audio-cue-settings-" + CueLibrary.TerrainWater;
            Assert.IsNotNull(screen.RootWidget.GetChildById(prefix + "-enabled"));
            Assert.IsNotNull(screen.RootWidget.GetChildById(prefix + "-volume"));
            Assert.IsNotNull(screen.RootWidget.GetChildById(prefix + "-pitch"));
            Assert.IsNotNull(screen.RootWidget.GetChildById(prefix + "-duration"));
            Assert.IsNotNull(screen.RootWidget.GetChildById(prefix + "-play"));
            Assert.IsNotNull(screen.RootWidget.GetChildById(prefix + "-reset"));
            Assert.IsNotNull(screen.RootWidget.GetChildById(prefix + "-back"));
        }
    }
}
