# JK Metrics Lite

JK Metrics Lite is a Jump King mod that writes lightweight progress metrics for stream overlays and local review.

## Output

When the mod runs, it creates a `JKMetricsLite` folder in the same folder as the mod. The folder contains overlay HTML files, TSV data files, saved state, and `error.log` if a recoverable error is detected.

The mod also creates `JKMetricsLite.env` in the same folder as the mod if it does not already exist. To use a different output folder, edit `OUTPUT_DIR` in that file.

```env
OUTPUT_DIR=C:\Path\To\JKMetricsLite
```

If `OUTPUT_DIR` is empty or `JKMetricsLite.env` is missing, the default folder in the same folder as the mod is used.

Overlay HTML files are created only when they do not already exist, so local edits are not overwritten. To regenerate an overlay HTML file, delete that file and launch the game again.

Overlay HTML files:

```text
area_name.html
area_no.html
area_name_speedrun.html
screen_timeline.html
```

Data files:

```text
area_bar_graph.tsv
screen_bar_graph.tsv
screen_timeline.tsv
progress_status.tsv
metrics_state.tsv
```

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


## Reset Metrics

Metrics are reset automatically when you start a new game. If you continue a previous game, the last saved metrics are carried over.

You can also use the in-game pause menu item `Reset Metrics` to clear the saved metrics manually.

