import os
import pandas as pd
import numpy as np

# =====================================================
# CONFIG
# =====================================================

BASE_DIR = os.path.dirname(os.path.abspath(__file__))

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

# =====================================================
# NORMALIZACIÓN
# =====================================================

def normalize_series(series):
    if series.max() == series.min():
        return np.zeros(len(series))
    return (series - series.min()) / (series.max() - series.min())

# =====================================================
# CÁLCULO DE IMPACTO
# =====================================================

impact_rows = []

for pillar, columns in PILLARS.items():

    excel_path = os.path.join(
        BASE_DIR,
        f"comparative_{pillar}",
        f"comparative_{pillar}.xlsx"
    )

    if not os.path.exists(excel_path):
        print("No encontrado:", excel_path)
        continue

    df = pd.read_excel(excel_path)

    row = {"Pillar": pillar}

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

            impact = norm_adap - norm_base

            impacts.append(impact)

        if len(impacts) > 0:
            row[group_name] = np.mean(impacts)
        else:
            row[group_name] = np.nan

    impact_rows.append(row)

# =====================================================
# TABLA FINAL
# =====================================================

impact_df = pd.DataFrame(impact_rows)

impact_df = impact_df.set_index("Pillar")

print("\nImpacto adaptativo por pilar y nivel:\n")
print(impact_df)

# =====================================================
# EXPORTAR
# =====================================================

output_file = os.path.join(BASE_DIR, "impact_by_pillar_and_level.xlsx")

impact_df.to_excel(output_file)

print("\nTabla exportada a:")
print(output_file)