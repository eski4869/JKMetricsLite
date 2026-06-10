# JK Metrics Lite

JK Metrics Lite is a Jump King mod that automatically detects area names and records first reach times, stay times, and screen transitions.

It generates local HTML overlays and TSV data files so the metrics can be displayed in OBS during custom map playthroughs, blind runs, or speedruns.

## Output

When the mod runs, it creates a `JKMetricsLite` folder in the same folder as the mod. The folder contains overlay HTML files, TSV data files, saved state, and `error.log` if a recoverable error is detected.

The mod also creates `JKMetricsLite.env` in the same folder as the mod if it does not already exist. To use a different output folder, edit `OUTPUT_DIR` in that file.

```env
OUTPUT_DIR=C:\Path\To\JKMetricsLite
```

If `OUTPUT_DIR` is empty or `JKMetricsLite.env` is missing, the default folder in the same folder as the mod is used.

Overlay HTML files are created only when they do not already exist, so local edits are not overwritten. To regenerate an overlay HTML file, delete that file and launch the game again.

Generated HTML files:

| File | Purpose | Update timing |
| --- | --- | --- |
| `area_name.html` | OBS overlay for area names, first reach times, stay graphs, and stay times. | Created only if missing when the mod starts. Existing files are not overwritten. |
| `area_no.html` | OBS overlay for area numbers instead of area names. | Created only if missing when the mod starts. Existing files are not overwritten. |
| `area_name_speedrun.html` | Compact OBS overlay for speedrun-style area timing. | Created only if missing when the mod starts. Existing files are not overwritten. |
| `screen_timeline.html` | OBS overlay for the real-time screen transition graph. | Created only if missing when the mod starts. Existing files are not overwritten. |
| `jump_activity.html` | Browser view for yearly jump activity from `jump_activity.tsv`. | Created only if missing when the mod starts. Existing files are not overwritten. |

Generated data files:

| File | Purpose | Write mode | Update timing |
| --- | --- | --- | --- |
| `area_bar_graph.tsv` | Area first reach, stay graph, stay time, and current area flag. | Overwritten. | About every 60 frames, when metrics are reset, and when the mod flushes data on level unload, level end, or game exit. |
| `screen_bar_graph.tsv` | Screen stay time and current screen flag. | Overwritten. | About every 60 frames, when metrics are reset, and when the mod flushes data on level unload, level end, or game exit. |
| `screen_timeline.tsv` | Time-series screen transition samples for the timeline graph. | Appended. | About every 60 frames and when the mod flushes data on level end or game exit. Reset when starting a new game or using Reset Metrics. |
| `progress_status.tsv` | Small status file used by OBS overlays for PB display. | Overwritten. | About every 60 frames, when metrics are reset, and when the mod flushes data on level unload, level end, or game exit. |
| `metrics_state.tsv` | Saved state used to continue metrics when resuming the same game. | Overwritten. | Saved with the regular overlay data and when the mod flushes data on level unload, level end, or game exit. |
| `jump_activity.tsv` | Timestamped total frames, jumps, and falls for jump activity charts. | Appended. Duplicate total values are skipped. | When the mod starts, about every 3600 frames, and when the mod flushes data on level unload, level end, or game exit. |
| `error.log` | Recoverable error details for troubleshooting. | Appended. | Only when a recoverable error is detected. |

## OBS Setup

Add a Browser Source in OBS, enable local file mode, and select one of the generated HTML files in the `JKMetricsLite` output folder.

`area_name.html`

Automatically detects area names and displays first reach time, stay graph, and stay time. Use this for blind custom map playthroughs or general exploration.

<img width="350" height="300" alt="image" src="https://github.com/user-attachments/assets/5ba263eb-424e-4c66-8622-7cced2ad0310" />

`area_no.html`

Use this if you want to display area numbers instead of area names. Be aware that this mode numbers areas by first-reach order,
not by the map's internal area order. Hidden or optional areas can throw off the numbering.

<img width="350" height="300" alt="image" src="https://github.com/user-attachments/assets/e29ce420-da61-4183-9861-313fc0f7df46" />

`area_name_speedrun.html`

A speedrun-focused area name table. Extra columns are removed, and times are shown in `m s ms` format.

<img width="350" height="300" alt="image" src="https://github.com/user-attachments/assets/78197173-a585-4be7-a0dc-73e0fcbfe789" />


`screen_timeline.html`

Displays screen transitions as a real-time graph.

<img width="350" height="300" alt="image" src="https://github.com/user-attachments/assets/8ca9df05-ac2a-472f-b7b4-a4ae0f5e8b64" />

In practice, crop the overlay and use only the parts you need. The image below is an example stream layout.

<img width="605" height="348" alt="image" src="https://github.com/user-attachments/assets/10760438-0855-4935-8f05-2f1c7db61d6b" />

## Jump Activity

`jump_activity.html` displays yearly jump activity from `jump_activity.tsv`. Open it directly in a browser and select the TSV file to view hourly jump heatmaps and monthly jumps.


## Reset Metrics

Metrics are reset automatically when you start a new game. If you continue a previous game, the last saved metrics are carried over.

You can also use the in-game pause menu item `Reset Metrics` to clear the saved metrics manually.

