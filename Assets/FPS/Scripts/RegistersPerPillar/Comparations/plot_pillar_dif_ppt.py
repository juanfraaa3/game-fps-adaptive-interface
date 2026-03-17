import os
import pandas as pd
import numpy as np
import matplotlib.pyplot as plt

BASE_DIR = os.path.dirname(os.path.abspath(__file__))

OUTPUT_DIR = os.path.join(BASE_DIR, "Impact_By_Pillar")
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

    excel_path = os.path.join(
        BASE_DIR,
        f"comparative_{pillar}",
        f"comparative_{pillar}.xlsx"
    )

    if not os.path.exists(excel_path):
        continue

    df = pd.read_excel(excel_path)

    level_results = []

    for group_name, group_filter in GROUPS.items():

        df_group = df[df["Jugador"].str.contains(group_filter, na=False)]

        base = df_group[df_group["Jugador"].str.contains("_base", na=False)]
        adap = df_group[df_group["Jugador"].str.contains("_adap", na=False)]

        impacts = []

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

            impacts.append(norm_adap - norm_base)

        if len(impacts) > 0:
            level_results.append(np.mean(impacts))
        else:
            level_results.append(0)

    # gráfico
    plt.figure(figsize=(6,5))

    plt.bar(GROUPS.keys(), level_results)

    plt.axhline(0, linestyle="--")

    plt.title(f"Impacto adaptativo – {pillar}")
    plt.ylabel("Δ Adaptive - Base (normalizado)")
    plt.xlabel("Nivel de experiencia")

    output = os.path.join(OUTPUT_DIR, f"impact_{pillar}.png")

    plt.tight_layout()
    plt.savefig(output, dpi=300)
    plt.close()

print("Gráficos generados en Impact_By_Pillar")