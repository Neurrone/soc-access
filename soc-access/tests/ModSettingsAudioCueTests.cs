using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Audio;
using SongsOfConquestAccess.Audio.Synth;

namespace SongsOfConquestAccess.Tests
{
    [TestClass]
    public sealed class ModSettingsAudioCueTests : ModSettingsFixture
    {
        [TestInitialize]
        public void BindTheConfig()
        {
            BindTemporaryConfig();
        }

        [TestMethod]
        public void EveryCueBindsWithDocumentedDefaults()
        {
            Assert.IsTrue(ModSettings.TileCuesEnabled);
            for (int i = 0; i < CueLibrary.AllCues.Count; i++)
            {
                string key = CueLibrary.AllCues[i].Key;
                Assert.IsTrue(ModSettings.GetCueEnabled(key), key);
                Assert.AreEqual(ModSettings.CueVolumeDefault, ModSettings.GetCueVolume(key), key);
                Assert.AreEqual(ModSettings.CuePitchSemitonesDefault, ModSettings.GetCuePitchSemitones(key), key);
                Assert.AreEqual(ModSettings.CueDurationScaleDefault, ModSettings.GetCueDurationScale(key), key);
            }
        }

        [TestMethod]
        public void CueSettersRoundTripAndClampToRange()
        {
            ModSettings.SetCueEnabled(CueLibrary.TerrainRoad, false);
            ModSettings.SetCueVolume(CueLibrary.TerrainRoad, 65);
            ModSettings.SetCuePitchSemitones(CueLibrary.TerrainRoad, -5);
            ModSettings.SetCueDurationScale(CueLibrary.TerrainRoad, 180);

            Assert.IsFalse(ModSettings.GetCueEnabled(CueLibrary.TerrainRoad));
            Assert.AreEqual(65, ModSettings.GetCueVolume(CueLibrary.TerrainRoad));
            Assert.AreEqual(-5, ModSettings.GetCuePitchSemitones(CueLibrary.TerrainRoad));
            Assert.AreEqual(180, ModSettings.GetCueDurationScale(CueLibrary.TerrainRoad));

            ModSettings.SetCueVolume(CueLibrary.TerrainRoad, 400);
            ModSettings.SetCuePitchSemitones(CueLibrary.TerrainRoad, -99);
            ModSettings.SetCueDurationScale(CueLibrary.TerrainRoad, 5);

            Assert.AreEqual(ModSettings.CueVolumeMaximum, ModSettings.GetCueVolume(CueLibrary.TerrainRoad));
            Assert.AreEqual(ModSettings.CuePitchSemitonesMinimum, ModSettings.GetCuePitchSemitones(CueLibrary.TerrainRoad));
            Assert.AreEqual(ModSettings.CueDurationScaleMinimum, ModSettings.GetCueDurationScale(CueLibrary.TerrainRoad));
        }

        /// <summary>The cue dialog writes every change as the player listens, so Cancel has to be
        /// able to put the whole tuning back. The enabled flag is not the dialog's: it is ticked in
        /// the glossary's table and a cancelled dialog leaves it where it stands.</summary>
        [TestMethod]
        public void SnapshotAndRestorePutTheWholeTuningBack()
        {
            ModSettings.SetCueVolume(CueLibrary.TerrainRoad, 45);
            ModSettings.SetCuePitchSemitones(CueLibrary.TerrainRoad, 3);
            CueTuning snapshot = ModSettings.SnapshotCue(CueLibrary.TerrainRoad);

            ModSettings.SetCueEnabled(CueLibrary.TerrainRoad, false);
            ModSettings.SetCueVolume(CueLibrary.TerrainRoad, 90);
            ModSettings.SetCuePitchSemitones(CueLibrary.TerrainRoad, -8);
            ModSettings.SetCueDurationScale(CueLibrary.TerrainRoad, 200);
            ModSettings.RestoreCue(CueLibrary.TerrainRoad, snapshot);

            Assert.IsFalse(ModSettings.GetCueEnabled(CueLibrary.TerrainRoad));
            Assert.AreEqual(45, ModSettings.GetCueVolume(CueLibrary.TerrainRoad));
            Assert.AreEqual(3, ModSettings.GetCuePitchSemitones(CueLibrary.TerrainRoad));
            Assert.AreEqual(ModSettings.CueDurationScaleDefault, ModSettings.GetCueDurationScale(CueLibrary.TerrainRoad));
        }

        /// <summary>Reset restores the cue's SOUND. Whether it plays at all is the glossary table's
        /// checkbox, and a player who silenced a cue did not ask for it back.</summary>
        [TestMethod]
        public void ResetCueRestoresDefaultsAndLeavesEnabledAlone()
        {
            ModSettings.SetCueEnabled(CueLibrary.HexActive, false);
            ModSettings.SetCueVolume(CueLibrary.HexActive, 10);
            ModSettings.SetCuePitchSemitones(CueLibrary.HexActive, 7);
            ModSettings.SetCueDurationScale(CueLibrary.HexActive, 200);

            ModSettings.ResetCue(CueLibrary.HexActive);

            Assert.IsFalse(ModSettings.GetCueEnabled(CueLibrary.HexActive));
            Assert.AreEqual(ModSettings.CueVolumeDefault, ModSettings.GetCueVolume(CueLibrary.HexActive));
            Assert.AreEqual(ModSettings.CuePitchSemitonesDefault, ModSettings.GetCuePitchSemitones(CueLibrary.HexActive));
            Assert.AreEqual(ModSettings.CueDurationScaleDefault, ModSettings.GetCueDurationScale(CueLibrary.HexActive));
        }

        /// <summary>The beacon is not a cue and has one setting: a volume on the cue scale, which the
        /// glossary's beacon dialog moves and resets.</summary>
        [TestMethod]
        public void BeaconVolumeStartsFullAndClamps()
        {
            Assert.AreEqual(ModSettings.BeaconVolumeDefault, ModSettings.GetBeaconVolume());

            ModSettings.SetBeaconVolume(70);
            Assert.AreEqual(70, ModSettings.GetBeaconVolume());

            ModSettings.SetBeaconVolume(400);
            Assert.AreEqual(ModSettings.CueVolumeMaximum, ModSettings.GetBeaconVolume());

            ModSettings.SetBeaconVolume(-10);
            Assert.AreEqual(ModSettings.CueVolumeMinimum, ModSettings.GetBeaconVolume());

            ModSettings.SetBeaconVolume(ModSettings.BeaconVolumeDefault);
            Assert.AreEqual(ModSettings.BeaconVolumeDefault, ModSettings.GetBeaconVolume());
        }

        /// <summary>The volume over every mod sound: full until set, clamped to the slider's range,
        /// and handed to the audio sources as the game's linear gain (50 is half).</summary>
        [TestMethod]
        public void ModVolumeStartsFullClampsAndIsLinearGain()
        {
            Assert.AreEqual(ModSettings.ModVolumeDefault, ModSettings.GetModVolume());
            Assert.AreEqual(1f, ModSettings.ModGain, 0.0001f);

            ModSettings.SetModVolume(50);
            Assert.AreEqual(50, ModSettings.GetModVolume());
            Assert.AreEqual(0.5f, ModSettings.ModGain, 0.0001f);

            ModSettings.SetModVolume(400);
            Assert.AreEqual(ModSettings.CueVolumeMaximum, ModSettings.GetModVolume());

            ModSettings.SetModVolume(-10);
            Assert.AreEqual(ModSettings.CueVolumeMinimum, ModSettings.GetModVolume());

            ModSettings.SetModVolume(ModSettings.ModVolumeDefault);
            Assert.AreEqual(ModSettings.ModVolumeDefault, ModSettings.GetModVolume());
        }

        [TestMethod]
        public void EffectiveSpecFollowsTheDurationSetting()
        {
            ModSettings.SetCueDurationScale(CueLibrary.TerrainWater, 200);

            CueSpec spec = CueLibrary.GetEffectiveSpec(CueLibrary.TerrainWater);

            Assert.AreEqual(80f, spec.Segments[1].StartMs, 0.001f);
            Assert.AreEqual(70f, spec.Segments[1].DurationMs, 0.001f);
        }
    }
}
