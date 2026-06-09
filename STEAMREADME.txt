[h1]JK Metrics Lite[/h1]

JK Metrics Lite is a lightweight Jump King overlay mod for custom maps, blind playthroughs, and speedruns.

It automatically detects area names and records first reach times, stay times, and screen transitions. The mod writes local HTML overlay files and TSV data files that can be displayed in OBS.

[hr]

[h2]OBS Overlays[/h2]

After launching the game with the mod enabled, open OBS and add a Browser Source. Enable local file mode, then select one of the generated HTML files from the JKMetricsLite output folder.

[list]
[*][b]area_name.html[/b] - Shows area names, first reach times, stay graphs, and stay times.
[*][b]area_no.html[/b] - Shows area numbers instead of area names. Area numbers are assigned by first reach order, so hidden or optional areas can change the numbering.
[*][b]area_name_speedrun.html[/b] - A compact speedrun-focused area table using m s ms time format.
[*][b]screen_timeline.html[/b] - Displays screen transitions as a real-time graph.
[/list]

[hr]

[h2]Output Folder[/h2]

By default, the mod creates a JKMetricsLite folder in the same folder as the mod. It also creates JKMetricsLite.env if it does not already exist.

To change the output folder, edit OUTPUT_DIR in JKMetricsLite.env. Leave it empty to use the default location.

[hr]

[h2]Reset Behavior[/h2]

Metrics reset automatically when you start a new game. If you continue a previous game, the last saved metrics are carried over.

You can also reset metrics manually from the pause menu with Reset Metrics.

[hr]

[h2]Source Code[/h2]

[url=https://github.com/eski4869/JKMetricsLite]GitHub Repository[/url]
