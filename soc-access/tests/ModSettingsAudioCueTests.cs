using System.IO;
using BepInEx.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Audio;
using SongsOfConquestAccess.Audio.Synth;

namespace SongsOfConquestAccess.Tests
{
    [TestClass]
    public sealed class ModSettingsAudioCueTests
    {
        private string _configPath;

        [TestInitialize]
        public void BindTemporaryConfig()
        {
            _configPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".cfg");
            ModSettings.Bind(new ConfigFile(_configPath, saveOnInit: false));
        }

        [TestCleanup]
        public void ResetSettings()
        {
            ModSettings.Reset();
            if (File.Exists(_configPath))
            {
                File.Delete(_configPath);
            }
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

        [TestMethod]
        public void ResetCueRestoresDefaults()
        {
            ModSettings.SetCueEnabled(CueLibrary.HexEnemy, false);
            ModSettings.SetCueVolume(CueLibrary.HexEnemy, 10);
            ModSettings.SetCuePitchSemitones(CueLibrary.HexEnemy, 7);
            ModSettings.SetCueDurationScale(CueLibrary.HexEnemy, 200);

            ModSettings.ResetCue(CueLibrary.HexEnemy);

            Assert.IsTrue(ModSettings.GetCueEnabled(CueLibrary.HexEnemy));
            Assert.AreEqual(ModSettings.CueVolumeDefault, ModSettings.GetCueVolume(CueLibrary.HexEnemy));
            Assert.AreEqual(ModSettings.CuePitchSemitonesDefault, ModSettings.GetCuePitchSemitones(CueLibrary.HexEnemy));
            Assert.AreEqual(ModSettings.CueDurationScaleDefault, ModSettings.GetCueDurationScale(CueLibrary.HexEnemy));
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
