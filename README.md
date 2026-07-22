# JK Metrics Lite

JK Metrics Lite is a lightweight metrics tool and progress tracker for Jump King Nexile maps and custom maps.

It automatically records useful run and activity data while you play.

Main features:

- Automatically detects the current area and screen
- Tracks split times and duration for each area
- Tracks PB progress based on the furthest reached area/screen
- Lets you exclude optional or hidden areas from displayed run metrics
- Generates OBS-ready overlays for blind playthroughs, exploration, and speedruns
- Provides area name, area number, speedrun-style, and real-time progress graph views
- Saves TSV metrics that can be reviewed later or used for custom analysis
- Records long-term totals for heatmaps, monthly charts, or personal recaps

## Output

When the mod runs, it creates a `JKMetricsLite` folder in the same folder as the mod.

Settings are stored in `eski4869.JKMetricsLite.Settings.xml` next to the mod. Restart the game after editing this file.

```xml
<MetricsPreferences>
  <AttemptMetricsEnabled>true</AttemptMetricsEnabled>
  <TotalMetricsEnabled>true</TotalMetricsEnabled>
  <OutputDir>JKMetricsLite</OutputDir>
  <AttemptBackupGenerations>1</AttemptBackupGenerations>
</MetricsPreferences>
```

`AttemptMetricsEnabled` controls per-attempt run metrics such as area splits, duration, PB progress, and progress graph data.

`TotalMetricsEnabled` controls long-term total metrics used by `recap.html`.

`OutputDir` controls where generated files are written. `JKMetricsLite` is a relative path and creates the output folder next to the mod. Absolute paths such as `C:\JumpKingMetrics` are also supported.

Select `Open Output Folder` in the mod menu to open the configured output directory.

`AttemptBackupGenerations` controls how many previous attempt folders are kept under `raw_data/attempts/`. The default is `1`, and values are capped at `10`.

Generated files are organized by purpose:

```text
JKMetricsLite/
├── raw_data/
│   ├── attempts/
│   │   ├── current/
│   │   │   ├── area_progress.tsv
│   │   │   ├── screen_events.tsv
│   │   │   ├── screen_metrics.tsv
│   │   │   └── current_state.tsv
│   │   └── 777/
│   │       ├── area_progress.tsv
│   │       ├── screen_events.tsv
│   │       ├── screen_metrics.tsv
│   │       └── current_state.tsv
│   └── total_metrics.tsv
├── obs/
│   ├── area_name_splits.html
│   ├── area_number_splits.html
│   ├── area_name_splits_speedrun.html
│   ├── area_number_splits_speedrun.html
│   └── progress_graph.html
├── local/
│   └── recap.html
└── error.log
```

- `raw_data` contains simple TSV data for overlays or custom analysis.
- `obs` contains real-time views intended for OBS Browser Sources.
- `local` contains browser views intended to be opened directly.

HTML files are created only when they do not already exist, so local CSS and layout edits are not overwritten. Delete an HTML file and launch the game again to regenerate it.

## Run Metrics

Run metrics are for the current attempt. They are useful for blind custom map playthroughs, general exploration, and speedruns.

| Type | File | Key | Values | Update timing |
| --- | --- | --- | --- | --- |
| Run Progress | `raw_data/attempts/current/area_progress.tsv` | Area | Entry split, landing split, duration, current/excluded/unlocked flags | Rewritten about every 60 frames. |
| Screen Events | `raw_data/attempts/current/screen_events.tsv` | Screen event | First entry and first landing events | Appended when the player first enters or first lands on a new screen. |
| Screen Metrics | `raw_data/attempts/current/screen_metrics.tsv` | Elapsed time | Screen, jumps, and falls snapshot samples | Appended about every 60 frames. |
| Current State | `raw_data/attempts/current/current_state.tsv` | Current attempt | Latest screen, Now/PB progress, and graph revision | Rewritten about every 60 frames. |

The raw TSV files store numeric values such as milliseconds. Formatting for normal or speedrun-style display is handled by the HTML views.
Run metrics are restored from the attempt TSV files when continuing the same attempt.

### TSV Columns

`raw_data/attempts/current/area_progress.tsv`

| Column | Format | Meaning |
| --- | --- | --- |
| `area_name` | Text | Area name. `Unknown` can also appear. `Babe Screen` and `Clear Time` are split-only rows and are not areas. |
| `entry_ms` | Milliseconds | Game time when the area was entered. Used by speedrun views. |
| `landing_ms` | Milliseconds or empty | Game time when the player first landed in the area. Used by non-speedrun views. |
| `duration_ms` | Milliseconds or empty | Time spent in the area. Empty for `Clear Time`. |
| `current` | `0` or `1` | `1` when this is the current area. |
| `excluded` | `0` or `1` | `1` when this area is excluded from bundled metrics views. |
| `unlocked` | `0` or `1` | `1` when the area name can be shown without spoiler masking. |

`raw_data/attempts/current/screen_events.tsv`

| Column | Format | Meaning |
| --- | --- | --- |
| `screen` | Integer | Physical screen number. |
| `area_name` | Text | Area name detected for that screen. |
| `event` | `entry` or `landing` | `entry` when the screen is first entered, `landing` when it is first landed on. |
| `elapsed_ms` | Milliseconds | Game time when the event happened. |

`raw_data/attempts/current/screen_metrics.tsv`

| Column | Format | Meaning |
| --- | --- | --- |
| `elapsed_ms` | Milliseconds | Game time when the sample was recorded. |
| `screen` | Integer | Current physical screen number. |
| `jumps` | Integer | Total jumps at that sample. |
| `falls` | Integer | Total falls at that sample. |

`raw_data/attempts/current/current_state.tsv`

| Column | Format | Meaning |
| --- | --- | --- |
| `attempt` | Integer or `UNKNOWN` | Current attempt number. |
| `elapsed_ms` | Milliseconds | Current game time. |
| `screen` | Integer | Current physical screen number. |
| `area_name` | Text | Current area name. |
| `current_area_order` | Integer | Current area progress order used by OBS views. |
| `current_screen_order` | Integer | Current screen order inside the current area. |
| `pb_area_order` | Integer | PB area progress order. |
| `pb_screen_order` | Integer | PB screen order inside the PB area. |
| `screen_order_revision` | Integer | Incremented when the progress graph mapping needs a rebuild. |

### OBS Views

Add a Browser Source in OBS, enable local file mode, and select one of the generated HTML files in the `obs` folder.

`obs/area_name_splits.html`

Automatically detects area names and displays PB, Now, first landed split, a relative duration bar, and duration. Use this for blind custom map playthroughs or general exploration.

<img width="350" height="300" alt="image" src="https://github.com/user-attachments/assets/5ba263eb-424e-4c66-8622-7cced2ad0310" />

`obs/area_number_splits.html`

Use this if you want to display area numbers instead of area names. Be aware that this mode numbers areas by first-reach order,
not by the map's internal area order. Hidden or optional areas can throw off the numbering.

<img width="350" height="300" alt="image" src="https://github.com/user-attachments/assets/e29ce420-da61-4183-9861-313fc0f7df46" />

`obs/area_name_splits_speedrun.html`

A speedrun-focused area name split table using `m s ms` time format.

<img width="350" height="300" alt="image" src="https://github.com/user-attachments/assets/78197173-a585-4be7-a0dc-73e0fcbfe789" />

`obs/area_number_splits_speedrun.html`

A speedrun-focused area number split table using `m s ms` time format.


`obs/progress_graph.html`

Displays screen progress as a real-time graph.

<img width="350" height="300" alt="image" src="https://github.com/user-attachments/assets/8ca9df05-ac2a-472f-b7b4-a4ae0f5e8b64" />

In practice, crop the overlay and use only the parts you need. The image below is an example stream layout.

<img width="605" height="348" alt="image" src="https://github.com/user-attachments/assets/10760438-0855-4935-8f05-2f1c7db61d6b" />

## Long-Term Metrics

Long-term metrics are accumulated across play sessions and are not reset with run metrics.

| Type | File | Key | Values | Update timing |
| --- | --- | --- | --- | --- |
| Long-Term Metrics | `raw_data/total_metrics.tsv` | Sample time | Total frames, jumps, and falls | Appended on mod start, about every 3600 frames, and on level end. Duplicate samples may be kept. |

`raw_data/total_metrics.tsv`

| Column | Format | Meaning |
| --- | --- | --- |
| `sampled_at` | ISO timestamp | Local timestamp when the sample was recorded. |
| `total_frames` | Integer | Total game frames. |
| `total_jumps` | Integer | Total jumps. |
| `total_falls` | Integer | Total falls. |

Open `local/recap.html` directly in a browser and select `raw_data/total_metrics.tsv` to view jump-based recaps, including total jumps/falls, active days, streaks, best day/month/weekday/hour, hourly heatmaps, and monthly jumps/falls.

The TSV file is selected manually so the page can work when opened directly in a browser, without a local web server. Browsers usually block direct file loading from nearby files for security reasons.

<img width="661" height="579" alt="image" src="https://github.com/user-attachments/assets/99ae0f5a-647a-4e57-9f21-52c9dc95011c" />

## Area and PB Logic

Areas are detected from the map's `location_settings.xml` data.

If multiple areas match the same screen, the area whose `unlock` value is closest to that screen takes priority. If there is still a tie, the higher `unlock` and then higher `start` value wins. For example, screen 10 is treated as `LOCATION_FALSE_KINGS_KEEP`, not `LOCATION_COLOSSAL_DRAIN`.

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

If you enter an optional or hidden area that you do not want in the displayed run metrics, open the pause menu in that area and enable `Exclude This Area`. The area is ignored for PB and hidden by the bundled area views. Its row remains in `area_progress.tsv` with `excluded` set to `1`, so disabling the option restores the accumulated split time and duration. In an `Unknown` area, the option is shown as checked and cannot be changed because that area is already excluded by definition.

PB means the furthest reached position based on the first-reached area order and the first-reached screen order inside that area.

`entry_ms` is captured when the area is entered and is used by the speedrun area views. `landing_ms` is captured when the player first lands in that area and is used by the non-speedrun area views. Duration is counted separately from the frames processed by JK Metrics Lite. `Babe Screen` records only its entry and landing splits, while `Clear Time` records the final clear time; neither row participates in area order, PB, or duration metrics.


## Reset Metrics

Area, screen, and PB metrics are reset automatically when you start a new game. If you continue a previous game, the last saved metrics are carried over.

When a new game starts, the previous `raw_data/attempts/current/` files are moved to `raw_data/attempts/{attempt}/` before the current attempt data is reset. By default, only the latest previous attempt is kept.

`total_metrics.tsv` is not reset with run metrics. It keeps accumulating long-term stats.
