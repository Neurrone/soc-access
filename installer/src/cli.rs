use std::io::{self, Write};
use std::path::PathBuf;

use semver::Version;

use crate::core::detect::{self, GameInstall, GameSource};
use crate::core::github;
use crate::core::install::{self, InstallState};
use crate::core::process;
use crate::core::uninstall;

pub fn run() {
    println!("=== Songs of Conquest Access Installer ===");
    let Some(game) = get_game_install() else {
        println!("No valid Songs of Conquest install selected.");
        return;
    };

    loop {
        let state = install::classify_install(&game.path);
        println!();
        println!("Game directory: {}", game.path.display());
        print_state(&state);
        println!();
        println!("1. Install / Update / Repair latest");
        println!("2. Reinstall latest");
        println!("3. Uninstall");
        println!("4. Exit");

        match prompt("Choose an option: ").as_str() {
            "1" => install_latest(&game, false),
            "2" => install_latest(&game, true),
            "3" => uninstall_managed(&game.path, &state),
            "4" => return,
            _ => println!("Invalid option."),
        }
    }
}

fn get_game_install() -> Option<GameInstall> {
    if let Some(detected) = detect::detect_game() {
        println!("Detected game directory: {}", detected.path.display());
        let answer = prompt("Use this path? (Y/n): ");
        if answer != "n" && answer != "no" {
            return Some(detected);
        }
    }

    let input = prompt("Type the game directory path: ");
    let path = PathBuf::from(input.trim_matches('"'));
    if detect::validate_game_dir(&path) {
        Some(GameInstall {
            path,
            source: GameSource::Manual,
        })
    } else {
        None
    }
}

fn print_state(state: &InstallState) {
    match state {
        InstallState::Fresh => println!("State: not installed"),
        InstallState::Managed(manifest) => {
            println!("State: installed, version {}", manifest.mod_version);
        }
        InstallState::Unmanaged => {
            println!("State: existing manual/unmanaged install detected");
        }
        InstallState::DamagedState(reason) => {
            println!("State: installer state is damaged ({reason})");
        }
    }
}

fn install_latest(game: &GameInstall, force: bool) {
    if process::is_game_running() {
        println!("Close Songs of Conquest before installing.");
        return;
    }

    let state = install::classify_install(&game.path);
    let release = match github::fetch_latest_release() {
        Ok(r) => r,
        Err(e) => {
            println!("Error: {e}");
            return;
        }
    };
    let Some(asset) = github::find_mod_zip(&release) else {
        println!("Error: no SongsOfConquestAccess-vX.Y.Z.zip asset found.");
        return;
    };

    if !force {
        if let (Some(installed), Some(latest)) = (
            install::installed_version(&state),
            asset.version().and_then(|v| Version::parse(&v).ok()),
        ) {
            if installed >= latest {
                println!("Already up to date. Use reinstall to repair this install.");
                return;
            }
        }
    }

    let temp_dir = install::temp_session_dir();
    let zip_path = temp_dir.join(&asset.name);
    let result = (|| {
        github::download_asset(&asset, &zip_path)?;
        if let Some(expected) = asset.sha256_digest() {
            install::verify_sha256(&zip_path, &expected)?;
        }
        install::install_from_zip(&zip_path, &game.path, &game.source, &asset, &state)?;
        Ok::<(), String>(())
    })();
    let _ = std::fs::remove_dir_all(&temp_dir);

    match result {
        Ok(()) => println!("Install complete."),
        Err(e) => println!("Error: {e}"),
    }
}

fn uninstall_managed(game_path: &std::path::Path, state: &InstallState) {
    if process::is_game_running() {
        println!("Close Songs of Conquest before uninstalling.");
        return;
    }
    let InstallState::Managed(manifest) = state else {
        println!("Uninstall is only available for installs managed by this installer.");
        return;
    };
    let answer = prompt("Remove Songs of Conquest Access? (y/N): ");
    if answer != "y" && answer != "yes" {
        return;
    }
    match uninstall::uninstall(game_path, manifest) {
        Ok(()) => println!("Uninstall complete."),
        Err(e) => println!("Error: {e}"),
    }
}

fn prompt(message: &str) -> String {
    print!("{message}");
    let _ = io::stdout().flush();
    let mut input = String::new();
    let _ = io::stdin().read_line(&mut input);
    input.trim().to_lowercase()
}
