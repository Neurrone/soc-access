use semver::Version;
use wxdragon::prelude::*;

use crate::core::detect::{self, GameInstall, GameSource};
use crate::core::github::{self, Asset};
use crate::core::install::{self, InstallState};
use crate::core::process;
use crate::core::uninstall;

pub fn run() {
    wxdragon::main(|_app| {
        let frame = Frame::builder()
            .with_title("Songs of Conquest Access Installer")
            .with_size(Size::new(680, 430))
            .build();

        let panel = Panel::builder(&frame).build();
        let main_sizer = BoxSizer::builder(Orientation::Vertical).build();

        let status = StaticText::builder(&panel)
            .with_label("Detecting game directory...")
            .build();

        let path_sizer = BoxSizer::builder(Orientation::Horizontal).build();
        let path_label = StaticText::builder(&panel)
            .with_label("Game directory:")
            .build();
        let path_input = TextCtrl::builder(&panel).build();
        let browse_btn = Button::builder(&panel).with_label("Browse...").build();

        path_sizer.add(&path_label, 0, SizerFlag::All, 4);
        path_sizer.add(&path_input, 1, SizerFlag::Expand | SizerFlag::All, 4);
        path_sizer.add(&browse_btn, 0, SizerFlag::All, 4);

        let log = TextCtrl::builder(&panel)
            .with_style(
                TextCtrlStyle::MultiLine | TextCtrlStyle::ReadOnly | TextCtrlStyle::WordWrap,
            )
            .build();

        let btn_sizer = BoxSizer::builder(Orientation::Horizontal).build();
        let install_btn = Button::builder(&panel).with_label("Install").build();
        let reinstall_btn = Button::builder(&panel).with_label("Reinstall").build();
        let uninstall_btn = Button::builder(&panel).with_label("Uninstall").build();
        let close_btn = Button::builder(&panel).with_label("Close").build();

        btn_sizer.add_stretch_spacer(1);
        btn_sizer.add(&install_btn, 0, SizerFlag::All, 4);
        btn_sizer.add(&reinstall_btn, 0, SizerFlag::All, 4);
        btn_sizer.add(&uninstall_btn, 0, SizerFlag::All, 4);
        btn_sizer.add(&close_btn, 0, SizerFlag::All, 4);

        main_sizer.add(&status, 0, SizerFlag::Expand | SizerFlag::All, 8);
        main_sizer.add_sizer(
            &path_sizer,
            0,
            SizerFlag::Expand | SizerFlag::Left | SizerFlag::Right,
            8,
        );
        main_sizer.add(&log, 1, SizerFlag::Expand | SizerFlag::All, 8);
        main_sizer.add_sizer(&btn_sizer, 0, SizerFlag::Expand | SizerFlag::All, 4);
        panel.set_sizer(main_sizer, true);

        install_btn.enable(false);
        reinstall_btn.enable(false);
        uninstall_btn.enable(false);

        let release = match github::fetch_latest_release() {
            Ok(release) => {
                log_append(&log, "Connected to GitHub.");
                Some(release)
            }
            Err(e) => {
                log_append(&log, &format!("Could not check GitHub releases: {e}"));
                None
            }
        };
        let asset = release.as_ref().and_then(github::find_mod_zip);
        if let Some(asset) = asset.as_ref() {
            log_append(&log, &format!("Latest release asset: {}", asset.name));
        } else {
            log_append(&log, "No SongsOfConquestAccess-vX.Y.Z.zip asset found.");
        }

        if let Some(detected) = detect::detect_game() {
            path_input.set_value(&detected.path.to_string_lossy());
            log_append(
                &log,
                &format!("Detected game directory: {}", detected.path.display()),
            );
        } else {
            status.set_label("Game directory not found. Browse to select it.");
            log_append(&log, "Could not auto-detect the game directory.");
        }

        refresh_state(
            &path_input,
            &status,
            &install_btn,
            &reinstall_btn,
            &uninstall_btn,
            asset.as_ref(),
            &log,
        );

        {
            let frame_c = frame.clone();
            let path_input_c = path_input.clone();
            let status_c = status.clone();
            let install_btn_c = install_btn.clone();
            let reinstall_btn_c = reinstall_btn.clone();
            let uninstall_btn_c = uninstall_btn.clone();
            let log_c = log.clone();
            let asset_c = asset.clone();

            browse_btn.on_click(move |_| {
                let dialog =
                    DirDialog::builder(&frame_c, "Select Songs of Conquest game directory", "")
                        .build();
                if dialog.show_modal() != ID_OK {
                    return;
                }
                let Some(path_str) = dialog.get_path() else {
                    return;
                };
                let path = std::path::PathBuf::from(&path_str);
                if !detect::validate_game_dir(&path) {
                    log_append(
                        &log_c,
                        &format!("Invalid game directory: {}", path.display()),
                    );
                    status_c.set_label("Invalid game directory.");
                    return;
                }
                path_input_c.set_value(&path.to_string_lossy());
                log_append(&log_c, &format!("Game directory: {}", path.display()));
                refresh_state(
                    &path_input_c,
                    &status_c,
                    &install_btn_c,
                    &reinstall_btn_c,
                    &uninstall_btn_c,
                    asset_c.as_ref(),
                    &log_c,
                );
            });
        }

        {
            let frame_c = frame.clone();
            let path_input_c = path_input.clone();
            let status_c = status.clone();
            let install_btn_c = install_btn.clone();
            let reinstall_btn_c = reinstall_btn.clone();
            let uninstall_btn_c = uninstall_btn.clone();
            let log_c = log.clone();
            let asset_c = asset.clone();

            install_btn.on_click(move |_| {
                let Some(asset) = asset_c.as_ref() else {
                    show_logged_error(&frame_c, &log_c, "No downloadable release zip was found.");
                    return;
                };
                let Some(game) = game_from_input(&path_input_c) else {
                    show_logged_error(
                        &frame_c,
                        &log_c,
                        "Select a valid Songs of Conquest game directory.",
                    );
                    return;
                };
                install_asset(&frame_c, &game, asset, false, &log_c);
                refresh_state(
                    &path_input_c,
                    &status_c,
                    &install_btn_c,
                    &reinstall_btn_c,
                    &uninstall_btn_c,
                    Some(asset),
                    &log_c,
                );
            });
        }

        {
            let frame_c = frame.clone();
            let path_input_c = path_input.clone();
            let status_c = status.clone();
            let install_btn_c = install_btn.clone();
            let reinstall_btn_c = reinstall_btn.clone();
            let uninstall_btn_c = uninstall_btn.clone();
            let log_c = log.clone();
            let asset_c = asset.clone();

            reinstall_btn.on_click(move |_| {
                let Some(asset) = asset_c.as_ref() else {
                    show_logged_error(&frame_c, &log_c, "No downloadable release zip was found.");
                    return;
                };
                let Some(game) = game_from_input(&path_input_c) else {
                    show_logged_error(
                        &frame_c,
                        &log_c,
                        "Select a valid Songs of Conquest game directory.",
                    );
                    return;
                };
                install_asset(&frame_c, &game, asset, true, &log_c);
                refresh_state(
                    &path_input_c,
                    &status_c,
                    &install_btn_c,
                    &reinstall_btn_c,
                    &uninstall_btn_c,
                    Some(asset),
                    &log_c,
                );
            });
        }

        {
            let frame_c = frame.clone();
            let path_input_c = path_input.clone();
            let status_c = status.clone();
            let install_btn_c = install_btn.clone();
            let reinstall_btn_c = reinstall_btn.clone();
            let uninstall_btn_c = uninstall_btn.clone();
            let log_c = log.clone();
            let asset_c = asset.clone();

            uninstall_btn.on_click(move |_| {
                let Some(game) = game_from_input(&path_input_c) else {
                    show_logged_error(
                        &frame_c,
                        &log_c,
                        "Select a valid Songs of Conquest game directory.",
                    );
                    return;
                };
                let state = install::classify_install(&game.path);
                let InstallState::Managed(manifest) = state else {
                    show_logged_error(
                        &frame_c,
                        &log_c,
                        "Uninstall is only available for installs managed by this installer.",
                    );
                    return;
                };
                let confirm = MessageDialog::builder(
                    &frame_c,
                    "Remove Songs of Conquest Access from this game directory?",
                    "Confirm Uninstall",
                )
                .with_style(MessageDialogStyle::YesNo | MessageDialogStyle::IconQuestion)
                .build()
                .show_modal();
                if confirm != ID_YES {
                    return;
                }
                if process::is_game_running() {
                    show_logged_error(
                        &frame_c,
                        &log_c,
                        "Close Songs of Conquest before uninstalling.",
                    );
                    return;
                }
                match uninstall::uninstall(&game.path, &manifest) {
                    Ok(()) => {
                        log_append(&log_c, "Uninstall complete.");
                        show_info(&frame_c, "Uninstall complete.");
                    }
                    Err(e) => {
                        show_logged_error(&frame_c, &log_c, &format!("Uninstall failed:\n{e}"))
                    }
                }
                refresh_state(
                    &path_input_c,
                    &status_c,
                    &install_btn_c,
                    &reinstall_btn_c,
                    &uninstall_btn_c,
                    asset_c.as_ref(),
                    &log_c,
                );
            });
        }

        {
            let frame_c = frame.clone();
            close_btn.on_click(move |_| {
                frame_c.close(true);
            });
        }

        frame.show(true);
    })
    .expect("Failed to start installer UI");
}

fn game_from_input(path_input: &TextCtrl) -> Option<GameInstall> {
    let path = std::path::PathBuf::from(path_input.get_value());
    if detect::validate_game_dir(&path) {
        Some(GameInstall {
            path,
            source: GameSource::Manual,
        })
    } else {
        None
    }
}

fn refresh_state(
    path_input: &TextCtrl,
    status: &StaticText,
    install_btn: &Button,
    reinstall_btn: &Button,
    uninstall_btn: &Button,
    asset: Option<&Asset>,
    log: &TextCtrl,
) {
    let Some(game) = game_from_input(path_input) else {
        install_btn.enable(false);
        reinstall_btn.enable(false);
        uninstall_btn.enable(false);
        return;
    };

    let state = install::classify_install(&game.path);
    let has_asset = asset.is_some();

    match &state {
        InstallState::Fresh => {
            status.set_label("Ready to install.");
            install_btn.set_label("Install");
            install_btn.enable(has_asset);
            reinstall_btn.enable(false);
            uninstall_btn.enable(false);
        }
        InstallState::Unmanaged => {
            status.set_label("Manual install detected. Repair is available.");
            install_btn.set_label("Repair");
            install_btn.enable(has_asset);
            reinstall_btn.enable(false);
            uninstall_btn.enable(false);
        }
        InstallState::DamagedState(reason) => {
            status.set_label("Installer state is damaged. Repair is available.");
            log_append(log, &format!("Damaged installer state: {reason}"));
            install_btn.set_label("Repair");
            install_btn.enable(has_asset);
            reinstall_btn.enable(false);
            uninstall_btn.enable(false);
        }
        InstallState::Managed(manifest) => {
            let update_available = match (
                Version::parse(&manifest.mod_version).ok(),
                asset
                    .and_then(|a| a.version())
                    .and_then(|v| Version::parse(&v).ok()),
            ) {
                (Some(installed), Some(latest)) => installed < latest,
                _ => has_asset,
            };
            if update_available {
                status.set_label(&format!(
                    "Update available. Installed: {}",
                    manifest.mod_version
                ));
                install_btn.set_label("Update");
                install_btn.enable(has_asset);
            } else {
                status.set_label(&format!("Up to date. Installed: {}", manifest.mod_version));
                install_btn.set_label("Update");
                install_btn.enable(false);
            }
            reinstall_btn.enable(has_asset);
            uninstall_btn.enable(true);
        }
    }
}

fn install_asset(
    parent: &impl WxWidget,
    game: &GameInstall,
    asset: &Asset,
    force: bool,
    log: &TextCtrl,
) {
    if process::is_game_running() {
        show_logged_error(parent, log, "Close Songs of Conquest before installing.");
        return;
    }

    let state = install::classify_install(&game.path);
    if !force {
        if let (Some(installed), Some(latest)) = (
            install::installed_version(&state),
            asset.version().and_then(|v| Version::parse(&v).ok()),
        ) {
            if installed >= latest {
                log_append(
                    log,
                    "Already up to date. Reinstall can repair damaged files.",
                );
                show_info(
                    parent,
                    "Already up to date. Reinstall can repair damaged files.",
                );
                return;
            }
        }
    }

    let temp_dir = install::temp_session_dir();
    let zip_path = temp_dir.join(&asset.name);
    log_append(log, &format!("Downloading {}...", asset.name));

    let result = (|| {
        github::download_asset(asset, &zip_path)?;
        if let Some(expected) = asset.sha256_digest() {
            log_append(log, "Verifying download...");
            install::verify_sha256(&zip_path, &expected)?;
        }
        log_append(log, "Installing...");
        install::install_from_zip(&zip_path, &game.path, &game.source, asset, &state)?;
        Ok::<(), String>(())
    })();
    let _ = std::fs::remove_dir_all(&temp_dir);

    match result {
        Ok(()) => {
            log_append(log, "Install complete.");
            show_info(parent, "Install complete.");
        }
        Err(e) => show_logged_error(parent, log, &format!("Install failed:\n{e}")),
    }
}

fn log_append(log: &TextCtrl, message: &str) {
    let current = log.get_value();
    if current.is_empty() {
        log.set_value(message);
    } else {
        log.set_value(&format!("{current}\n{message}"));
    }
}

fn show_info(parent: &impl WxWidget, message: &str) {
    MessageDialog::builder(parent, message, "Songs of Conquest Access Installer")
        .with_style(MessageDialogStyle::OK | MessageDialogStyle::IconInformation)
        .build()
        .show_modal();
}

fn show_error(parent: &impl WxWidget, message: &str) {
    MessageDialog::builder(parent, message, "Songs of Conquest Access Installer")
        .with_style(MessageDialogStyle::OK | MessageDialogStyle::IconError)
        .build()
        .show_modal();
}

fn show_logged_error(parent: &impl WxWidget, log: &TextCtrl, message: &str) {
    log_append(log, message);
    show_error(parent, message);
}
