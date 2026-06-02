use regex::Regex;
use reqwest::blocking::Client;
use serde::Deserialize;

use super::paths::{GITHUB_API_URL, MOD_ZIP_PREFIX, MOD_ZIP_SUFFIX};

#[derive(Debug, Clone, Deserialize)]
pub struct ReleaseInfo {
    #[serde(rename = "tag_name")]
    pub _tag_name: String,
    #[serde(default)]
    pub assets: Vec<Asset>,
}

#[derive(Debug, Clone, Deserialize)]
pub struct Asset {
    pub name: String,
    pub browser_download_url: String,
    #[serde(default)]
    pub digest: Option<String>,
}

impl Asset {
    pub fn sha256_digest(&self) -> Option<String> {
        let digest = self.digest.as_deref()?.trim();
        let (algo, hex) = digest.split_once(':')?;
        if !algo.eq_ignore_ascii_case("sha256") {
            return None;
        }
        Some(hex.trim().to_lowercase())
    }

    pub fn version(&self) -> Option<String> {
        parse_mod_zip_version(&self.name)
    }
}

pub fn fetch_latest_release() -> Result<ReleaseInfo, String> {
    let client = Client::builder()
        .user_agent("SongsOfConquestAccessInstaller")
        .timeout(std::time::Duration::from_secs(30))
        .build()
        .map_err(|e| format!("Failed to create HTTP client: {e}"))?;

    let response = client
        .get(GITHUB_API_URL)
        .send()
        .map_err(|e| format!("Failed to connect to GitHub: {e}"))?;

    if !response.status().is_success() {
        return Err(format!("GitHub API returned {}", response.status()));
    }

    response
        .json::<ReleaseInfo>()
        .map_err(|e| format!("Failed to parse GitHub release: {e}"))
}

pub fn find_mod_zip(release: &ReleaseInfo) -> Option<Asset> {
    release
        .assets
        .iter()
        .find(|asset| parse_mod_zip_version(&asset.name).is_some())
        .cloned()
}

pub fn parse_mod_zip_version(name: &str) -> Option<String> {
    let escaped_prefix = regex::escape(MOD_ZIP_PREFIX);
    let escaped_suffix = regex::escape(MOD_ZIP_SUFFIX);
    let pattern = format!(
        r"^{}(?P<version>\d+\.\d+\.\d+){}$",
        escaped_prefix, escaped_suffix
    );
    let re = Regex::new(&pattern).unwrap();
    re.captures(name)
        .and_then(|caps| caps.name("version").map(|m| m.as_str().to_string()))
}

pub fn download_asset(asset: &Asset, dest: &std::path::Path) -> Result<(), String> {
    let client = Client::builder()
        .user_agent("SongsOfConquestAccessInstaller")
        .timeout(std::time::Duration::from_secs(120))
        .build()
        .map_err(|e| format!("Failed to create HTTP client: {e}"))?;

    let mut response = client
        .get(&asset.browser_download_url)
        .send()
        .map_err(|e| format!("Download failed: {e}"))?;

    if !response.status().is_success() {
        return Err(format!("Download returned {}", response.status()));
    }

    if let Some(parent) = dest.parent() {
        std::fs::create_dir_all(parent).map_err(|e| format!("Failed to create temp dir: {e}"))?;
    }
    let mut file =
        std::fs::File::create(dest).map_err(|e| format!("Failed to create downloaded zip: {e}"))?;
    std::io::copy(&mut response, &mut file).map_err(|e| format!("Failed to save zip: {e}"))?;
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn parses_mod_zip_version() {
        assert_eq!(
            parse_mod_zip_version("SongsOfConquestAccess-v0.6.3.zip").as_deref(),
            Some("0.6.3")
        );
        assert!(parse_mod_zip_version("source.zip").is_none());
    }

    #[test]
    fn extracts_sha256_digest() {
        let asset = Asset {
            name: "x.zip".to_string(),
            browser_download_url: "https://example.invalid/x.zip".to_string(),
            digest: Some("sha256:ABCDEF".to_string()),
        };
        assert_eq!(asset.sha256_digest().as_deref(), Some("abcdef"));
    }
}
