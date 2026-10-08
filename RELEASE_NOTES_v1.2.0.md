# AmaazLoader v1.2.0

AmaazLoader v1.2.0 focuses on Quick Install, automatic pairing, SideStore / LiveContainer support, and a cleaner end-to-end sideloading experience.

## Highlights

- Added Quick Install support for SideStore and LiveContainer.
- Added Stable / Nightly build selection using a segmented control.
- Added official SideStore Nightly download support.
- Added LiveContainer Nightly support using the official LiveContainer + SideStore nightly build.
- Added automatic installed-on-device detection.
- Added cached reuse of verified stable IPA downloads.
- Added automatic pairing generation, injection, and verification after supported installs.
- Added automatic pairing rebuild/retry when the first pairing attempt fails.
- Added a one-click Fix Pairing action.
- Added LiveContainer to the automatic post-install pairing flow.
- Added one-click continuation into sideloading when the device and Apple Account are already ready.
- Added clearer success states such as “installed + paired ✓”.
- Added a dedicated Pair stage to the progress flow:
  Prepare → Authenticate → Sign → Install → Pair → Done.
- Refined the visual style toward AmaazLoader’s black / white / green interface.

## Pairing improvements

The pairing backend was updated to provide reliable device identity and pairing data for supported apps.

A Rust compile issue in the pairing helper was fixed by inserting the UDID directly into the plist dictionary instead of attempting to call `as_dictionary_mut()` on `plist::Dictionary`.

SideStore pairing has been verified working end-to-end.

## Download verification

Installer: `AmaazLoader-v1.2.0-Setup.exe`

SHA-256: `341D26C1514E61D82C18DD4D666C0CB18C9A1CEE74FE025F33A13EF5D27FC78D`

## Version

Application version: **1.2.0**

Installer version: **1.2.0**
