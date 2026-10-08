# AmaazLoader

AmaazLoader is a modern Windows utility for signing and installing IPA files on iPhone and iPad using your own Apple Account.

## Features

- Automatic IPA signing with your own Apple Account
- Direct USB installation
- Apple Account authentication
- Two-factor authentication support
- Automatic iPhone and iPad detection
- Apple Developer team detection
- IPA metadata display
- Real-time Prepare → Authenticate → Sign → Install → Pair → Done progress
- Quick Install for SideStore and LiveContainer
- Stable / Nightly build selection
- Automatic SideStore / LiveContainer pairing setup
- Pairing verification and automatic repair retry
- One-click Fix Pairing action
- Installed-on-device detection
- Verified stable-download caching for faster reinstalls
- Native Windows installer
- Portable signing backend and bundled device communication tools

## Requirements

- Windows 10 or Windows 11
- 64-bit Windows
- iPhone or iPad connected by USB
- Apple Account
- Internet connection for Apple authentication, downloads, and signing services

## v1.2.0

AmaazLoader v1.2.0 is the current public release.

This update expands AmaazLoader from a basic IPA sideloader into a more complete sideloading suite, with Quick Install, automatic device pairing, SideStore and LiveContainer support, Stable / Nightly channels, pairing repair, and a more polished installation workflow.

See `RELEASE_NOTES_v1.2.0.md` for the full change summary.

Installer: `AmaazLoader-v1.2.0-Setup.exe`

SHA-256: `341D26C1514E61D82C18DD4D666C0CB18C9A1CEE74FE025F33A13EF5D27FC78D`

## How it works

### Standard IPA install

1. Connect your iPhone or iPad to your Windows PC over USB.
2. Select or drop an IPA file into AmaazLoader.
3. Connect your Apple Account.
4. Complete Apple verification if requested.
5. AmaazLoader signs the IPA and installs it directly to the connected device.

### Quick Install

1. Connect your device and Apple Account.
2. Choose Stable or Nightly.
3. Select SideStore or LiveContainer.
4. AmaazLoader downloads the app, signs it, installs it, generates pairing data, imports it, and verifies the result.
5. If pairing validation fails, AmaazLoader automatically attempts a fresh pairing rebuild before giving up.

## Privacy

AmaazLoader does not intentionally store your Apple Account password or two-factor authentication code. Credentials are supplied when required during the Apple authentication and signing flow.

## Website

https://amaazbots.github.io/AmaazLoader-Website/

## Disclaimer

AmaazLoader is an independent project and is not affiliated with Apple Inc.

Only sideload apps that you have the right to install and use.

© 2026 Amaazbots
