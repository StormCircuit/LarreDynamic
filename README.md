# LarreDynamic

Stationeers mod that allows Larre to interact with all Dynamic Things with special handling for Aimee, Dirci, and Atmospheric Filtration devices.

For those:

- Aimee/Dirci - Restricted to interacting with battery and IC10 slot.
- Atmospherics - Now interacts with the gas filter slots and IC10 slot. IC10 slot must be open for interaction to work.

- Version: `1.0.0`

## Build

Open `LarreDynamic.sln` and build the `LarreDynamic` project. The project references the Stationeers managed assemblies and BepInEx files from the configured game installation in `LarreDynamic.csproj`.

After a successful build, the plugin DLL is copied to the Stationeers user mods directory at `Documents\My Games\Stationeers\mods\LarreDynamic`.
