# Fonts

KwekDrop uses **Thmanyah Sans** for the Arabic interface (Light, Regular, Medium, Bold, Black).

The font files are **not included in this repository**: the Thmanyah Font License does not allow
redistributing or hosting the font files. To build the app with the Arabic font:

1. Download Thmanyah Sans from the official site: https://font.thmanyah.com
2. Copy these five files into this folder:
   `thmanyah-sans-Light.ttf`, `thmanyah-sans-Regular.ttf`, `thmanyah-sans-Medium.ttf`,
   `thmanyah-sans-Bold.ttf`, `thmanyah-sans-Black.ttf`.

Without them the project still builds and runs; the interface then falls back to Segoe UI Variable / Segoe UI.
The license allows embedding the font in a compiled application, so the built app embeds the files as resources.
