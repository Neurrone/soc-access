use sysinfo::System;

pub fn is_game_running() -> bool {
    let system = System::new_all();
    system.processes().values().any(|process| {
        let name = process.name().to_string_lossy().to_ascii_lowercase();
        name == "songsofconquest.exe" || name == "songsofconquest"
    })
}
