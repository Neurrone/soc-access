use std::path::{Path, PathBuf};

pub const GITHUB_API_URL: &str = "https://api.github.com/repos/Neurrone/soc-access/releases/latest";
pub const MOD_ZIP_PREFIX: &str = "SongsOfConquestAccess-v";
pub const MOD_ZIP_SUFFIX: &str = ".zip";
pub const GAME_EXE: &str = "SongsOfConquest.exe";
pub const MANAGED_MARKER: &str = "SongsOfConquest_Data/Managed/Assembly-CSharp.dll";
pub const PLUGIN_REL: &str = "BepInEx/plugins/SongsOfConquest.Access.dll";
pub const MANIFEST_REL: &str = "BepInEx/config/SongsOfConquestAccess/install.json";
pub const BACKUPS_REL: &str = "BepInEx/config/SongsOfConquestAccess/backups";
pub const BEPINEX_CONFIG_REL: &str = "BepInEx/config/BepInEx.cfg";

pub fn manifest_path(game_dir: &Path) -> PathBuf {
    game_dir.join(MANIFEST_REL)
}

pub fn bepinex_config_path(game_dir: &Path) -> PathBuf {
    game_dir.join(BEPINEX_CONFIG_REL)
}

pub fn normalize_rel(path: &str) -> String {
    path.replace('\\', "/").trim_start_matches("./").to_string()
}

pub fn required_loader_files() -> &'static [&'static str] {
    &[
        "winhttp.dll",
        "doorstop_config.ini",
        "BepInEx/core/BepInEx.dll",
        PLUGIN_REL,
    ]
}
