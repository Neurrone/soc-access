use std::collections::HashSet;
use std::fs;
use std::path::{Path, PathBuf};

use regex::Regex;

use super::paths::{GAME_EXE, MANAGED_MARKER};

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum GameSource {
    Steam,
    Gog,
    Manual,
}

impl GameSource {
    pub fn as_manifest_str(&self) -> &'static str {
        match self {
            GameSource::Steam => "steam",
            GameSource::Gog => "gog",
            GameSource::Manual => "manual",
        }
    }
}

#[derive(Debug, Clone)]
pub struct GameInstall {
    pub path: PathBuf,
    pub source: GameSource,
}

pub fn detect_game() -> Option<GameInstall> {
    let candidates = game_candidates();
    for candidate in candidates {
        if validate_game_dir(&candidate.path) {
            return Some(candidate);
        }
    }
    None
}

pub fn validate_game_dir(path: &Path) -> bool {
    path.join(GAME_EXE).exists() && path.join(MANAGED_MARKER).exists()
}

pub fn game_candidates() -> Vec<GameInstall> {
    let mut result = Vec::new();
    let mut seen = HashSet::new();

    for var in ["SOC_GAME_DIR", "SONGS_OF_CONQUEST_DIR"] {
        if let Ok(value) = std::env::var(var) {
            add_candidate(
                &mut result,
                &mut seen,
                PathBuf::from(value),
                GameSource::Manual,
            );
        }
    }

    for path in steam_candidates() {
        add_candidate(&mut result, &mut seen, path, GameSource::Steam);
    }

    for path in gog_candidates() {
        add_candidate(&mut result, &mut seen, path, GameSource::Gog);
    }

    result
}

fn add_candidate(
    result: &mut Vec<GameInstall>,
    seen: &mut HashSet<String>,
    path: PathBuf,
    source: GameSource,
) {
    let normalized = path.to_string_lossy().trim().trim_matches('"').to_string();
    if normalized.is_empty() {
        return;
    }
    let key = normalized.to_lowercase();
    if seen.insert(key) {
        result.push(GameInstall {
            path: PathBuf::from(normalized),
            source,
        });
    }
}

pub fn parse_steam_library_paths(content: &str) -> Vec<PathBuf> {
    let re = Regex::new(r#""path"\s+"([^"]+)""#).unwrap();
    re.captures_iter(content)
        .map(|cap| PathBuf::from(cap[1].replace("\\\\", "\\")))
        .collect()
}

fn steam_candidates() -> Vec<PathBuf> {
    let mut candidates = Vec::new();

    for steam_root in steam_roots() {
        candidates.push(
            steam_root
                .join("steamapps")
                .join("common")
                .join("SongsOfConquest"),
        );
        let vdf = steam_root.join("steamapps").join("libraryfolders.vdf");
        if let Ok(content) = fs::read_to_string(vdf) {
            for lib in parse_steam_library_paths(&content) {
                candidates.push(lib.join("steamapps").join("common").join("SongsOfConquest"));
            }
        }
    }

    candidates
}

fn steam_roots() -> Vec<PathBuf> {
    let mut roots = Vec::new();

    #[cfg(windows)]
    {
        use winreg::RegKey;
        use winreg::enums::{HKEY_CURRENT_USER, HKEY_LOCAL_MACHINE};

        if let Ok(key) = RegKey::predef(HKEY_CURRENT_USER).open_subkey("Software\\Valve\\Steam") {
            if let Ok(path) = key.get_value::<String, _>("SteamPath") {
                roots.push(PathBuf::from(path.replace('/', "\\")));
            }
        }
        if let Ok(key) =
            RegKey::predef(HKEY_LOCAL_MACHINE).open_subkey("SOFTWARE\\WOW6432Node\\Valve\\Steam")
        {
            if let Ok(path) = key.get_value::<String, _>("InstallPath") {
                roots.push(PathBuf::from(path));
            }
        }
    }

    roots.push(PathBuf::from("C:\\Program Files (x86)\\Steam"));
    roots
}

fn gog_candidates() -> Vec<PathBuf> {
    let mut candidates = Vec::new();

    #[cfg(windows)]
    {
        candidates.extend(gog_registry_candidates());
    }

    candidates.push(PathBuf::from("C:\\GOG Games\\Songs of Conquest"));
    candidates.push(PathBuf::from(
        "C:\\Program Files (x86)\\GOG Galaxy\\Games\\Songs of Conquest",
    ));
    candidates.push(PathBuf::from(
        "C:\\Program Files\\GOG Galaxy\\Games\\Songs of Conquest",
    ));

    candidates
}

#[cfg(windows)]
fn gog_registry_candidates() -> Vec<PathBuf> {
    use winreg::RegKey;
    use winreg::enums::{HKEY_CURRENT_USER, HKEY_LOCAL_MACHINE};

    let mut candidates = Vec::new();
    for hive in [HKEY_LOCAL_MACHINE, HKEY_CURRENT_USER] {
        let root = RegKey::predef(hive);
        for subkey in [
            "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall",
            "SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall",
        ] {
            if let Ok(key) = root.open_subkey(subkey) {
                for name in key.enum_keys().flatten() {
                    if let Ok(app) = key.open_subkey(name) {
                        let display = app
                            .get_value::<String, _>("DisplayName")
                            .unwrap_or_default();
                        if !display.to_lowercase().contains("songs of conquest") {
                            continue;
                        }
                        if let Ok(path) = app.get_value::<String, _>("InstallLocation") {
                            candidates.push(PathBuf::from(path));
                        }
                        if let Ok(path) = app.get_value::<String, _>("InstallDir") {
                            candidates.push(PathBuf::from(path));
                        }
                    }
                }
            }
        }
    }
    candidates
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn parses_steam_library_paths() {
        let content = r#"
        "0" { "path" "C:\\Program Files (x86)\\Steam" }
        "1" { "path" "D:\\SteamLibrary" }
        "#;
        let paths = parse_steam_library_paths(content);
        assert_eq!(paths.len(), 2);
        assert_eq!(paths[1], PathBuf::from("D:\\SteamLibrary"));
    }

    #[test]
    fn validates_game_dir_markers() {
        let dir = tempfile::tempdir().unwrap();
        fs::write(dir.path().join(GAME_EXE), "").unwrap();
        fs::create_dir_all(dir.path().join("SongsOfConquest_Data").join("Managed")).unwrap();
        fs::write(dir.path().join(MANAGED_MARKER), "").unwrap();
        assert!(validate_game_dir(dir.path()));
    }
}
