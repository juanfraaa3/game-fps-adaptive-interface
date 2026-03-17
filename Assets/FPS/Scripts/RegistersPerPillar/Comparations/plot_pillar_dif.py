import os
import pandas as pd
import numpy as np
import matplotlib.pyplot as plt

BASE_DIR = os.path.dirname(os.path.abspath(__file__))

OUTPUT_DIR = os.path.join(BASE_DIR, "Radar_By_Pillar")
os.makedirs(OUTPUT_DIR, exist_ok=True)

GROUPS = {
    "Noobs": "_noob",
    "Regulars": "_regular",
    "Experts": "_expert"
}

PILLARS = {

    "orientation": [
        "AverageAngle_deg",
        "TimeLookingAway_s",
        "PercentTimeLookingAway"
    ],

    "trajectory": [
        "Duration",
        "ZigZag_Average",
        "Curvature_Normalized",
        "LandingError",
        "VerticalOscillation"
    ],

    "landing": [
        "LandingOffset_m",
        "SafetyMargin_m",
        "MicroCorrections_0_5s",
        "PostStabilizationTime_s",
        "PostLandingDrift_m"
    ],

    "movement": [
        "TimeOnPlatform",
        "RelativePositionStd",
        "EdgeRiskTime",
        "CorrectionPeaks"
    ],

    "multitasking": [
        "LapDuration_s",
        "LapProgress_0_1",
        "DeathOccurred",
        "ObstacleHitRate"
    ],

    "obstacles": [
        "TimeToClear_s",
        "ObstacleTouched",
        "ContactDuration_s"
    ],

    "jitter": [
        "CurrentJitter",
        "JitterPeak",
        "JitterVariance",
        "OvershootCount"
    ],

    "generalstats": [
        "Tiempo Desde Anterior (seg)",
        "Muertes totales",
        "Precisión Total (%)"
    ]
}

LOWER_IS_BETTER = [
    "AverageAngle_deg","TimeLookingAway_s","PercentTimeLookingAway",
    "Duration","ZigZag_Average","Curvature_Normalized","LandingError","VerticalOscillation",
    "LandingOffset_m","MicroCorrections_0_5s","PostStabilizationTime_s",
    "PostLandingDrift_m",
    "RelativePositionStd","EdgeRiskTime","CorrectionPeaks",
    "LapDuration_s","DeathOccurred","ObstacleHitRate",
    "TimeToClear_s","ObstacleTouched","ContactDuration_s",
    "CurrentJitter","JitterPeak","JitterVariance","OvershootCount",
    "Tiempo Desde Anterior (seg)","Muertes totales"
]

def normalize_series(series):
    if series.max() == series.min():
        return np.zeros(len(series))
    return (series - series.min()) / (series.max() - series.min())

for pillar, columns in PILLARS.items():

    excel_path = os.path.join(BASE_DIR,
                              f"comparative_{pillar}",
                              f"comparative_{pillar}.xlsx")

    if not os.path.exists(excel_path):
        continue

    df = pd.read_excel(excel_path)

    fig, axes = plt.subplots(1, 3, figsize=(18,6), subplot_kw=dict(polar=True))
    fig.suptitle(f"{pillar.capitalize()} - Comparación Base vs Adaptive", fontsize=16)

    for ax, (group_name, group_filter) in zip(axes, GROUPS.items()):

        df_group = df[df["Jugador"].str.contains(group_filter, na=False)]

        base = df_group[df_group["Jugador"].str.contains("_base", na=False)]
        adap = df_group[df_group["Jugador"].str.contains("_adap", na=False)]

        metrics = []
        base_vals = []
        adap_vals = []

        for col in columns:

            if col not in df.columns:
                continue

            b = pd.to_numeric(base[col], errors="coerce").dropna()
            a = pd.to_numeric(adap[col], errors="coerce").dropna()

            if len(b) == 0 or len(a) == 0:
                continue

            combined = pd.concat([b, a])
            norm = normalize_series(combined)

            norm_base = norm[:len(b)].median()
            norm_adap = norm[len(b):].median()

            if col in LOWER_IS_BETTER:
                norm_base = 1 - norm_base
                norm_adap = 1 - norm_adap

            metrics.append(col)
            base_vals.append(norm_base)
            adap_vals.append(norm_adap)

        if len(metrics) == 0:
            continue

        angles = np.linspace(0, 2*np.pi, len(metrics), endpoint=False).tolist()
        angles += angles[:1]

        base_vals += base_vals[:1]
        adap_vals += adap_vals[:1]

        ax.plot(angles, base_vals, label="Base")
        ax.fill(angles, base_vals, alpha=0.2)

        ax.plot(angles, adap_vals, label="Adaptive")
        ax.fill(angles, adap_vals, alpha=0.2)

        ax.set_xticks(angles[:-1])
        ax.set_xticklabels(metrics, fontsize=8)
        ax.set_ylim(0,1)
        ax.set_title(group_name)

    axes[0].legend(loc="upper right")

    plt.tight_layout()
    plt.subplots_adjust(top=0.85)

    output_path = os.path.join(OUTPUT_DIR, f"{pillar}_radar.png")
    plt.savefig(output_path, dpi=300)
    plt.close()

print("\n=== Radar plots guardados correctamente en Radar_By_Pillar ===")