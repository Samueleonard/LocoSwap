# CHANGELOG 1.7.0.0 (03-09-2026)
* Ported to .NET 10 (from .NET Framework 4.8)
* Settings now stored as `%AppData%\LocoSwap\settings.json`; existing settings are imported automatically on first run
* Removed the abandoned DotNetZip and WindowsAPICodePack dependencies
* Faster startup on large Train Simulator installs (route and scenario scanning moved off the UI thread, `.ap` archive listings cached)
* "Check all scenario consists when selecting route" is much faster: scenarios are checked in parallel, results are cached between selections (and re-used until a scenario changes), and each status dot appears as soon as its own check finishes instead of the list freezing until all are done
* Fixed: Quick Drive / template scenarios (which have no editable consist) were wrongly listed and crashed the app when opened; they are now correctly skipped
* Crash dialog with log location instead of silent exit on unhandled errors
* Window size/position and last-selected route remembered between sessions
* Bulk swap operations report how many vehicles were replaced
* Completed the German translation (was ~30 strings behind) and filled small gaps in French and Italian
* Added Spanish and Polish translations
* Changing the UI language now takes effect immediately - the main window and every open window retranslate on the spot instead of only applying to windows opened afterwards

# CHANGELOG 1.6.1.0 (05-01-2024)
* When selecting a missing vehicle, automatically focus the closest matching asset folder
* Add Dutch translation
* Improve Russian translation
* Add titles to `Change number` and `List all vehicles` windows

# CHANGELOG 1.6.0.0 (30-12-2023)
* Added ability to archive routes (AutoArchive)
* Added option to have symbols to show if scenarios are missing any stock on scenario list page
* Presets filter now also considers XML paths
* Enlarged `vehicle to be replaced` thumbnail
* Updated Russian translation
* Added an `Open manual` button
* Vehicle number now preserved if the new vehicle has no numbering list
* Moved the player train icon in front of the consist name

# CHANGELOG 1.5.1.0 (25-02-2023)
* Fix crash when listing scenarios on routes with no `Scenarios` dir
* Handle malformed `ScenarioProperties.xml`

# CHANGELOG 1.5.0.0 (22-02-2023)
* Vehicule number now preserved by default if involved in couple/uncouple operations
* Scenarios inside .ap files can now be viewed and edited
* Filters now search on input words individually
* Option to hide played scenarios added
* Fix Assets directory tree scrolling to previously selected item
* Scenario completion status now shown as `?` when SDBCache.bin is erroneous
* Italian language added
* Case now ignored when matching rolling stock paths

# CHANGELOG 1.4.0.0 (25-01-2023)
* Length of missing and available vehicles displayed
* Filters added for routes and scenarios lists
* Scenarios completion status now displayed
* Scenario author now displayed
* French language added
* Free roam and Quick drive scenarios now identified as such in the `Player train` field

# CHANGELOG 1.3.0.0 (21-12-2022)
* Option to apply rules to all stocks added
* The [LoSw] suffix is now a setting and can be modified or disabled
* Scenarios list now shows scenario informations (time of day, season, duration)
* Replacement rules and scanned vehicles can now be filtered with a text field
* App now has an icon :)
* Various quality of life improvements

# CHANGELOG 1.2.0.0 (23-11-2022)
* Automatically create replacement rules every time you hit Replace all (can be disabled with a ticking box)
* Vehicles missing but with existing replacement rules will now show with yellow dots, and an Apply all rules button is provided to apply all your rules at once
* Drastically speed up the scanning of .ap files by first looking for a RailVehicles folder (and only looking in it if there is one)
* Scanned vehicles list does not empty anymore when scanning a new folder
* Fixed some portals or waypoints breaking (those containing line returns, `Tonbridge Dn Fast` on the Chatham is your typical suspect)
* Edited scenarios now appended with a [LoSw] suffix
* Fixed crash when wagon blueprint does not feature a cCargoComponent
