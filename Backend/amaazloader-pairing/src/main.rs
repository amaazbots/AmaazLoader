use std::{
    io::Cursor,
    path::PathBuf,
    time::Duration,
};

use anyhow::{
    Context,
    Result,
    bail,
};
use idevice::{
    IdeviceService,
    afc::opcode::AfcFopenMode,
    house_arrest::HouseArrestClient,
    installation_proxy::InstallationProxyClient,
    lockdown::LockdownClient,
    provider::{
        IdeviceProvider,
        UsbmuxdProvider,
    },
    remote_pairing::{
        RemotePairingLockdownService,
        RpPairingFile,
    },
    usbmuxd::{
        Connection,
        UsbmuxdAddr,
        UsbmuxdConnection,
    },
};
use plist_macro::{
    plist,
    plist_to_xml_bytes,
};
use tokio::time::sleep;
use uuid::Uuid;

const SIDESTORE_NAME: &str = "SideStore";
const SIDESTORE_PATH: &str =
    "ALTPairingFile.mobiledevicepairing";

const LIVECONTAINER_NAME: &str =
    "LiveContainer";
const LIVECONTAINER_PATH: &str =
    "SideStore/Documents/ALTPairingFile.mobiledevicepairing";

#[derive(Clone, Debug)]
struct DeviceInfo {
    name: String,
    udid: String,
    version: String,
}

#[derive(Clone, Debug)]
struct PairingApp {
    name: String,
    bundle_id: String,
    path: String,
}

#[tokio::main]
async fn main() {
    if let Err(error) =
        run().await
    {
        eprintln!(
            "AMAAZ_PAIRING_ERROR:{}",
            error
        );

        std::process::exit(
            1);
    }
}

async fn run() -> Result<()> {
    let args:
        Vec<String> =
        std::env::args()
            .collect();

    let command =
        args.get(1)
            .map(
                String::as_str)
            .unwrap_or(
                "scan");

    println!(
        "AMAAZ_PAIRING_EVENT:STARTED");

    let mut usbmuxd =
        UsbmuxdConnection::default()
            .await
            .context(
                "Could not connect to usbmuxd.")?;

    let (
        provider,
        device,
    ) =
        detect_usb_device(
            &mut usbmuxd)
            .await?;

    println!(
        "AMAAZ_PAIRING_DEVICE:{}|{}|{}",
        device.name,
        device.udid,
        device.version);

    match command {
        "scan" => {
            let apps =
                scan_supported_apps(
                    &provider)
                    .await?;

            print_apps(
                &apps);

            println!(
                "AMAAZ_PAIRING_EVENT:SCAN_COMPLETE");
        }

        "place-all" => {
            let apps =
                wait_for_apps(
                    &provider,
                    None)
                    .await?;

            let pairing =
                generate_pairing_file(
                    &device,
                    &provider,
                    &mut usbmuxd)
                    .await?;

            place_in_apps(
                pairing,
                &provider,
                apps)
                .await?;

            println!(
                "AMAAZ_PAIRING_EVENT:PLACE_ALL_COMPLETE");
        }

        "place-sidestore" => {
            let apps =
                wait_for_apps(
                    &provider,
                    Some(
                        SIDESTORE_NAME))
                    .await?;

            let pairing =
                generate_pairing_file(
                    &device,
                    &provider,
                    &mut usbmuxd)
                    .await?;

            place_in_apps(
                pairing,
                &provider,
                apps)
                .await?;

            println!(
                "AMAAZ_PAIRING_EVENT:SIDESTORE_COMPLETE");
        }

        "place-livecontainer" => {
            let apps =
                wait_for_apps(
                    &provider,
                    Some(
                        LIVECONTAINER_NAME))
                    .await?;

            let pairing =
                generate_pairing_file(
                    &device,
                    &provider,
                    &mut usbmuxd)
                    .await?;

            place_in_apps(
                pairing,
                &provider,
                apps)
                .await?;

            println!(
                "AMAAZ_PAIRING_EVENT:LIVECONTAINER_COMPLETE");
        }

        "export" => {
            let destination =
                args.get(2)
                    .context(
                        "Export requires a destination path.")?;

            let pairing =
                generate_pairing_file(
                    &device,
                    &provider,
                    &mut usbmuxd)
                    .await?;

            tokio::fs::write(
                destination,
                pairing)
                .await
                .with_context(
                    || {
                        format!(
                            "Could not write pairing file to {}.",
                            destination)
                    })?;

            println!(
                "AMAAZ_PAIRING_EVENT:EXPORT_COMPLETE");
        }

        other => {
            bail!(
                "Unknown pairing command: {other}");
        }
    }

    println!(
        "AMAAZ_PAIRING_EVENT:COMPLETED");

    Ok(())
}

async fn detect_usb_device(
    usbmuxd:
        &mut UsbmuxdConnection,
) -> Result<(
    UsbmuxdProvider,
    DeviceInfo,
)> {
    let devices =
        usbmuxd
            .get_devices()
            .await
            .context(
                "Could not list connected iOS devices.")?;

    let device =
        devices
            .into_iter()
            .find(
                |device| {
                    matches!(
                        device.connection_type,
                        Connection::Usb)
                })
            .context(
                "No USB-connected iPhone or iPad was found.")?;

    let usbmuxd_addr =
        UsbmuxdAddr::from_env_var()
            .context(
                "Invalid usbmuxd address.")?;

    let provider =
        device.to_provider(
            usbmuxd_addr,
            "AmaazLoader");

    let mut lockdown =
        LockdownClient::connect(
            &provider)
            .await
            .context(
                "Could not connect to the device lockdown service.")?;

    let name =
        lockdown
            .get_value(
                Some(
                    "DeviceName"),
                None)
            .await
            .context(
                "Could not read the device name.")?
            .as_string()
            .context(
                "Device name was not a string.")?
            .to_string();

    let version =
        lockdown
            .get_value(
                Some(
                    "ProductVersion"),
                None)
            .await
            .context(
                "Could not read the iOS version.")?
            .as_string()
            .context(
                "iOS version was not a string.")?
            .to_string();

    Ok((
        provider,
        DeviceInfo {
            name,
            udid:
                device.udid,
            version,
        },
    ))
}

async fn generate_pairing_file(
    device:
        &DeviceInfo,
    provider:
        &dyn IdeviceProvider,
    usbmuxd:
        &mut UsbmuxdConnection,
) -> Result<Vec<u8>> {
    println!(
        "AMAAZ_PAIRING_EVENT:GENERATING");

    let mut pair_record =
        usbmuxd
            .get_pair_record(
                &device.udid)
            .await
            .context(
                "Could not read the device pairing record. Unlock the device and trust this computer.")?;

    pair_record.udid =
        Some(
            device.udid.clone());

    let mut lockdown =
        LockdownClient::connect(
            provider)
            .await
            .context(
                "Could not connect to lockdown while generating the pairing file.")?;

    lockdown
        .start_session(
            &pair_record)
        .await
        .context(
            "Could not start a lockdown session. Reconnect the device and accept the Trust prompt.")?;

    lockdown
        .set_value(
            "EnableWifiDebugging",
            true.into(),
            Some(
                "com.apple.mobile.wireless_lockdown"))
        .await
        .context(
            "Could not enable wireless lockdown support.")?;

    let lockdown_bytes =
        pair_record
            .serialize()
            .context(
                "Could not serialize the device pairing record.")?;

    let lockdown_plist =
        plist::Value::from_reader_xml(
            Cursor::new(
                lockdown_bytes))
            .context(
                "Could not parse the lockdown pairing record.")?;

    if ios_version_below(
        &device.version,
        17,
        4)
    {
        let dictionary =
            lockdown_plist
                .as_dictionary()
                .cloned()
                .context(
                    "The lockdown pairing record was not a dictionary.")?;

        println!(
            "AMAAZ_PAIRING_EVENT:PAIRING_READY");

        return Ok(
            plist_to_xml_bytes(
                &dictionary));
    }

    println!(
        "AMAAZ_PAIRING_EVENT:REMOTE_PAIRING");

    let host_id =
        Uuid::new_v4()
            .simple()
            .to_string();

    let hostname =
        format!(
            "amaazloader-{}",
            &host_id[..6]);

    let service =
        RemotePairingLockdownService::connect(
            provider)
            .await
            .context(
                "Could not connect to the remote pairing service.")?;

    let mut client =
        service
            .into_client(
                &hostname)
            .context(
                "The remote pairing service did not provide a socket.")?;

    let mut remote_pairing =
        RpPairingFile::generate(
            &hostname);

    client
        .connect(
            &mut remote_pairing,
            async || {
                "000000".to_string()
            })
        .await
        .context(
            "Could not complete remote pairing.")?;

    let remote_pairing_bytes =
        remote_pairing
            .to_bytes();

    let remote_pairing_plist =
        plist::Value::from_reader_xml(
            Cursor::new(
                remote_pairing_bytes))
            .context(
                "Could not parse the remote pairing data.")?;

    let combined =
        plist!(
            dict {
                :< lockdown_plist,
                :< remote_pairing_plist,
            });

    println!(
        "AMAAZ_PAIRING_EVENT:PAIRING_READY");

    Ok(
        plist_to_xml_bytes(
            &combined))
}

async fn scan_supported_apps(
    provider:
        &dyn IdeviceProvider,
) -> Result<Vec<PairingApp>> {
    let mut proxy =
        InstallationProxyClient::connect(
            provider)
            .await
            .context(
                "Could not connect to the installation proxy.")?;

    let installed =
        proxy
            .get_apps(
                Some(
                    "User"),
                None)
            .await
            .context(
                "Could not read installed apps from the device.")?;

    let mut result =
        Vec::new();

    for (
        bundle_id,
        app,
    ) in installed
    {
        let display_name =
            app.as_dictionary()
                .and_then(
                    |dictionary| {
                        dictionary
                            .get(
                                "CFBundleDisplayName")
                            .and_then(
                                |value| {
                                    value.as_string()
                                })
                    })
                .unwrap_or(
                    "");

        if display_name ==
            SIDESTORE_NAME
        {
            result.push(
                PairingApp {
                    name:
                        SIDESTORE_NAME.to_string(),
                    bundle_id,
                    path:
                        SIDESTORE_PATH.to_string(),
                });
        }
        else if display_name ==
            LIVECONTAINER_NAME
        {
            result.push(
                PairingApp {
                    name:
                        LIVECONTAINER_NAME.to_string(),
                    bundle_id,
                    path:
                        LIVECONTAINER_PATH.to_string(),
                });
        }
    }

    Ok(
        result)
}

async fn wait_for_apps(
    provider:
        &dyn IdeviceProvider,
    target:
        Option<&str>,
) -> Result<Vec<PairingApp>> {
    for attempt in 0..8 {
        let mut apps =
            scan_supported_apps(
                provider)
                .await?;

        if let Some(
            target_name) =
            target
        {
            apps.retain(
                |app| {
                    app.name ==
                        target_name
                });
        }

        if !apps.is_empty()
        {
            print_apps(
                &apps);

            return Ok(
                apps);
        }

        if attempt < 7
        {
            sleep(
                Duration::from_millis(
                    750))
                .await;
        }
    }

    match target {
        Some(
            SIDESTORE_NAME) =>
        {
            bail!(
                "SideStore was not found on the connected device.")
        }

        Some(
            LIVECONTAINER_NAME) =>
        {
            bail!(
                "LiveContainer was not found on the connected device.")
        }

        _ => {
            bail!(
                "No supported installed apps were found. Install SideStore or LiveContainer first.")
        }
    }
}

async fn place_in_apps(
    pairing:
        Vec<u8>,
    provider:
        &dyn IdeviceProvider,
    apps:
        Vec<PairingApp>,
) -> Result<()> {
    for app in apps {
        println!(
            "AMAAZ_PAIRING_EVENT:PLACING:{}",
            app.name);

        place_file(
            pairing.clone(),
            provider,
            app.bundle_id.clone(),
            app.path.clone())
            .await
            .with_context(
                || {
                    format!(
                        "Could not place the pairing file inside {}.",
                        app.name)
                })?;

        println!(
            "AMAAZ_PAIRING_EVENT:PLACED:{}",
            app.name);
    }

    Ok(())
}

async fn place_file(
    pairing:
        Vec<u8>,
    provider:
        &dyn IdeviceProvider,
    bundle_id:
        String,
    path:
        String,
) -> Result<()> {
    let house_arrest =
        HouseArrestClient::connect(
            provider)
            .await
            .context(
                "Could not connect to House Arrest.")?;

    let mut afc =
        house_arrest
            .vend_documents(
                bundle_id)
            .await
            .context(
                "Could not access the app's Documents directory.")?;

    let parent =
        path.rsplit_once(
            '/')
            .map(
                |parts| {
                    parts.0
                })
            .unwrap_or(
                "");

    if !parent.is_empty()
    {
        let mut current =
            String::from(
                "/Documents");

        for segment in
            parent.split(
                '/')
        {
            if segment.is_empty()
            {
                continue;
            }

            current.push(
                '/');

            current.push_str(
                segment);

            let _ =
                afc.mk_dir(
                    &current)
                    .await;
        }
    }

    let remote_path =
        format!(
            "/Documents/{}",
            path);

    let mut file =
        afc.open(
            remote_path,
            AfcFopenMode::Wr)
            .await
            .context(
                "Could not create the pairing file in the app container.")?;

    file.write_entire(
        &pairing)
        .await
        .context(
            "Could not write the pairing file.")?;

    file.close()
        .await
        .context(
            "Could not close the pairing file.")?;

    Ok(())
}

fn print_apps(
    apps:
        &[PairingApp],
) {
    for app in apps {
        println!(
            "AMAAZ_PAIRING_APP:{}|{}|{}",
            app.name,
            app.bundle_id,
            app.path);
    }
}

fn parse_version_component(
    value:
        Option<&str>,
) -> u32 {
    value
        .and_then(
            |segment| {
                let digits:
                    String =
                    segment
                        .chars()
                        .take_while(
                            |character| {
                                character
                                    .is_ascii_digit()
                            })
                        .collect();

                if digits.is_empty()
                {
                    None
                }
                else
                {
                    digits
                        .parse()
                        .ok()
                }
            })
        .unwrap_or(
            0)
}

fn ios_version_below(
    version:
        &str,
    target_major:
        u32,
    target_minor:
        u32,
) -> bool {
    let mut parts =
        version.split(
            '.');

    let major =
        parse_version_component(
            parts.next());

    let minor =
        parse_version_component(
            parts.next());

    (
        major,
        minor,
    ) <
    (
        target_major,
        target_minor,
    )
}
