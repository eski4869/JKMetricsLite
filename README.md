# JK Metrics Lite

JK Metrics Lite is a lightweight metrics tool and progress tracker for Jump King Nexile maps and custom maps.

It automatically records useful run and activity data while you play.

Main features:

- Automatically detects the current area and screen
- Tracks split times and duration for each area
- Tracks PB progress based on the furthest reached area/screen
- Lets you exclude optional or hidden areas from displayed run metrics
- Generates OBS-ready overlays for blind playthroughs, exploration, and speedruns
- Provides area name, area number, speedrun-style, and real-time screen graph views
- Saves TSV metrics that can be reviewed later or used for custom analysis
- Records long-term totals for heatmaps, monthly charts, or personal recaps

## Output

When the mod runs, it creates a `JKMetricsLite` folder in the same folder as the mod.

The mod also creates `JKMetricsLite.env` in the same folder as the mod if it does not already exist. To use a different output folder, edit `OUTPUT_DIR` in that file.

```env
OUTPUT_DIR=C:\Path\To\JKMetricsLite
```

If `OUTPUT_DIR` is empty or `JKMetricsLite.env` is missing, the default folder in the same folder as the mod is used.

Generated files are organized by purpose:

```text
JKMetricsLite/
|-- raw_data/
|   |-- area_metrics.tsv
|   |-- screen_timeline.tsv
|   |-- screen_order.tsv
|   |-- current_progress.tsv
|   `-- total_stats.tsv
|-- obs/
|   |-- area_name.html
|   |-- area_no.html
|   |-- area_name_speedrun.html
|   `-- screen_graph.html
|-- local/
|   `-- recap.html
`-- error.log
```

- `raw_data` contains simple TSV data for overlays or custom analysis.
- `obs` contains real-time views intended for OBS Browser Sources.
- `local` contains browser views intended to be opened directly.

HTML files are created only when they do not already exist, so local CSS and layout edits are not overwritten. Delete an HTML file and launch the game again to regenerate it.

## Run Metrics

Run metrics are for the current attempt. They are useful for blind custom map playthroughs, general exploration, and speedruns.

| Type | File | Key | Values | Update timing |
| --- | --- | --- | --- | --- |
| Run Metrics | `raw_data/area_metrics.tsv` | Area | Split time, duration, current and excluded flags | Rewritten about every 60 frames. |
| Run Metrics | `raw_data/screen_timeline.tsv` | Elapsed time | Screen | Appended about every 60 frames. Reset with new metrics. |
| Run Metrics | `raw_data/screen_order.tsv` | Area | First-reached screen order | When the reached-screen order changes, checked about every 3600 frames and on exit. |
| Current State | `raw_data/current_progress.tsv` | Current attempt | Attempt, elapsed time, current screen, and Current/PB progress | Rewritten about every 60 frames. |

The raw TSV files store numeric values such as milliseconds. Formatting for normal or speedrun-style display is handled by the HTML views.
Run metrics are restored from `area_metrics.tsv`, `screen_order.tsv`, and `current_progress.tsv` when continuing the same attempt.

### OBS Views

Add a Browser Source in OBS, enable local file mode, and select one of the generated HTML files in the `obs` folder.

`obs/area_name.html`

Automatically detects area names and displays Current, PB, split time, a relative duration bar, and duration. Use this for blind custom map playthroughs or general exploration.

<img width="350" height="300" alt="image" src="https://github.com/user-attachments/assets/5ba263eb-424e-4c66-8622-7cced2ad0310" />

`obs/area_no.html`

Use this if you want to display area numbers instead of area names. Be aware that this mode numbers areas by first-reach order,
not by the map's internal area order. Hidden or optional areas can throw off the numbering.

<img width="350" height="300" alt="image" src="https://github.com/user-attachments/assets/e29ce420-da61-4183-9861-313fc0f7df46" />

`obs/area_name_speedrun.html`

A speedrun-focused area name table. Extra columns are removed, and times are shown in `m s ms` format.

<img width="350" height="300" alt="image" src="https://github.com/user-attachments/assets/78197173-a585-4be7-a0dc-73e0fcbfe789" />


`obs/screen_graph.html`

Displays screen transitions as a real-time graph.

<img width="350" height="300" alt="image" src="https://github.com/user-attachments/assets/8ca9df05-ac2a-472f-b7b4-a4ae0f5e8b64" />

In practice, crop the overlay and use only the parts you need. The image below is an example stream layout.

<img width="605" height="348" alt="image" src="https://github.com/user-attachments/assets/10760438-0855-4935-8f05-2f1c7db61d6b" />

## Long-Term Metrics

Long-term metrics are accumulated across play sessions and are not reset with run metrics.

| Type | File | Key | Values | Update timing |
| --- | --- | --- | --- | --- |
| Long-Term Metrics | `raw_data/total_stats.tsv` | Sample time | Total frames, jumps, and falls | Appended on mod start, about every 3600 frames, and on level end. Duplicate samples may be kept. |

Open `local/recap.html` directly in a browser and select `raw_data/total_stats.tsv` to view jump-based recaps, including total jumps/falls, active days, streaks, best day/month/weekday/hour, hourly heatmaps, and monthly jumps.

The TSV file is selected manually so the page can work when opened directly in a browser, without a local web server. Browsers usually block direct file loading from nearby files for security reasons.

<img width="661" height="579" alt="image" src="https://github.com/user-attachments/assets/99ae0f5a-647a-4e57-9f21-52c9dc95011c" />


## Area and PB Logic

Areas are detected from the map's `location_settings.xml` data.

If multiple areas match the same screen, the area with the highest `start` value takes priority. For example, screen 10 is treated as `LOCATION_FALSE_KINGS_KEEP`, not `LOCATION_COLOSSAL_DRAIN`.

```xml
<Location>
  <start>6</start>
  <end>10</end>
  <unlock>6</unlock>
  <name>LOCATION_COLOSSAL_DRAIN</name>
</Location>

<Location>
  <start>10</start>
  <end>14</end>
  <unlock>11</unlock>
  <name>LOCATION_FALSE_KINGS_KEEP</name>
</Location>
```

Screens that do not belong to any defined area are ignored for PB, split times, and duration totals. For example, screen 131 is not included in either area below.

```xml
<Location>
  <start>124</start>
  <end>130</end>
  <unlock>124</unlock>
  <name>LOCATION_HOUSE_OF_NINE_LIVES</name>
</Location>

<Location>
  <start>132</start>
  <end>138</end>
  <unlock>132</unlock>
  <name>LOCATION_THE_PHANTOM_TOWER</name>
</Location>
```

Area numbers are assigned by first reach order, not by screen order. This avoids revealing the map's intended area order during blind play, but hidden or optional areas can change the numbering if you enter them early.

If you enter an optional or hidden area that you do not want in the displayed run metrics, open the pause menu in that area and enable `Exclude This Area`. The area is ignored for PB and hidden by the bundled area views. Its row remains in `area_metrics.tsv` with `is_excluded` set to `1`, so disabling the option restores the accumulated split time and duration. In an `Unknown` area, the option is shown as checked and cannot be changed because that area is already excluded by definition.

PB means the furthest reached position based on the first-reached area order and the first-reached screen order inside that area.

Split times are captured from the game's run timer. Duration is counted separately from the frames processed by JK Metrics Lite.


## Reset Metrics

Area, screen, and PB metrics are reset automatically when you start a new game. If you continue a previous game, the last saved metrics are carried over.

`total_stats.tsv` is not reset with run metrics. It keeps accumulating long-term stats.

