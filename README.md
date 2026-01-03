# Playnite JSON/Zip Exporter

This Playnite extension exports your game library into a structured format compatible with the [GameLibrary Mobile App](https://github.com/Adithya-Jayan/gamelibrary).

Unlike standard JSON exporters, this plugin packages your **metadata** (`library.json`) and your **local media** (Covers, Backgrounds, Icons) into a single `MobileExport.zip` file.

## Features
-   Exports rich metadata (Playtime, Completion Status, Developers, Publishers, etc.).
-   Automatically copies local images into the export.
-   Packages everything into a single, easy-to-transfer Zip file.

## How to Build
1.  Open the project in Visual Studio 2022 or use the CLI:
    ```bash
    dotnet build -c Release
    ```
2.  The output DLL will be located in `bin/Release/net462/playnite-json.dll`.

## Installation
1.  Navigate to your Playnite extensions folder: `%AppData%\Playnite\Extensions`.
2.  Create a folder named `JSON Mobile Exporter`.
3.  Copy `playnite-json.dll` and `extension.yaml` into that folder.
4.  Restart Playnite.

## Usage
Go to **Main Menu > Mobile Export > Export Library for Mobile (Zip)**. The resulting `MobileExport.zip` will be created in your Playnite installation directory.