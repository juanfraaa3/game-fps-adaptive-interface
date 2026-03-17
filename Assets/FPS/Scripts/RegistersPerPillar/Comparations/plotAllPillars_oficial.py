import os
import pandas as pd
import matplotlib.pyplot as plt
import numpy as np

# =====================================================
# CONFIG GENERAL
# =====================================================

BASE_DIR = os.path.dirname(os.path.abspath(__file__))

GROUPS = {
    "noobs": "_noob",
    "regulars": "_regular",
    "experts": "_expert",
    "all": None
}

# =====================================================
# DEFINICIÓN DE PILARES Y COLUMNAS
# (revisadas según lo que ya analizamos)
# =====================================================

PILLARS = {

    "orientation": [
        "AverageAngle_deg",
        "TimeLookingAway_s",
        "PercentTimeLookingAway"
    ],

    "landing": [
        "LandingOffset_m",
        "LandingOffset_Forward_m",
        "LandingOffset_Side_m",
        "SafetyMargin_m",
        "MicroCorrections_0_5s",
        "PreLandingHorizontalJitter",
        "PostStabilizationTime_s",
        "PostLandingDrift_m",
        "LandingFailed",
        "PostLandingFall"
    ],

    "movement": [
        "TimeOnPlatform",
        "RelativePositionStd",
        "RelativePositionMean",
        "RelativePositionMax",
        "RelativePositionMin",
        "SampleCount",
        "EdgeRiskTime",
        "CorrectionPeaks",
        "RawCorrectionsCount"
    ],

    "multitasking": [
        "LapDuration_s",
        "AttemptDuration_s",
        "LapProgress_0_1",
        "DeathOccurred",
        "MeanDeviation_m",
        "SpeedSD_mps",
        "MicroCorrectionsRate_per_s",
        "ObstacleHitRate",
        "AbandonoPilar"
    ],

    "obstacles": [
        "TimeToClear_s",
        "ObstacleTouched",
        "ContactDuration_s",
        "ContactX",
        "ContactY",
        "ContactZ"
    ],

    "trajectory": [
        "Duration",
        "ZigZag_Average",
        "Curvature_Normalized",
        "LandingError",
        "VerticalOscillation"
    ],

    "jitter": [
        "CurrentJitter",
        "SmoothedJitter",
        "JitterPeak",
        "JitterVariance",
        "JitterStdDev",
        "OvershootCount",
        "JitterArea",
        "AbandonoPilar"
    ],

    "generalstats": [
        "Tiempo Desde Anterior (seg)",
        "Muertes entre checkpoints",
        "Muertes totales",
        "Precisión Blaster (%)",
        "Precisión Escopeta (%)",
        "Precisión Total (%)"
    ]
}

# =====================================================
# FUNCIÓN DE PLOTEO
# =====================================================

def generate_plots_for_pillar(pillar_name, columns):

    excel_path = os.path.join(BASE_DIR, f"comparative_{pillar_name}", f"comparative_{pillar_name}.xlsx")

    if not os.path.exists(excel_path):
        print(f"[{pillar_name}] No se encontró el archivo.")
        return

    df = pd.read_excel(excel_path)
    df = df[df["Jugador"].notna()]

    for group_name, group_filter in GROUPS.items():

        if group_filter is not None:
            df_group = df[df["Jugador"].str.contains(group_filter, na=False)]
        else:
            df_group = df.copy()

        base = df_group[df_group["Jugador"].str.contains("_base", na=False)]
        adap = df_group[df_group["Jugador"].str.contains("_adap", na=False)]

        if len(base) == 0 or len(adap) == 0:
            continue

        valid_cols = [c for c in columns if c in df.columns]

        n = len(valid_cols)
        cols_plot = 3
        rows_plot = int(np.ceil(n / cols_plot))

        fig, axes = plt.subplots(rows_plot, cols_plot, figsize=(15, 4 * rows_plot))
        axes = axes.flatten()

        for i, col in enumerate(valid_cols):

            data_base = pd.to_numeric(base[col], errors="coerce").dropna()
            data_adap = pd.to_numeric(adap[col], errors="coerce").dropna()

            if len(data_base) == 0 or len(data_adap) == 0:
                continue

            # Si ambos grupos son constantes
            if data_base.nunique() <= 1 and data_adap.nunique() <= 1:
                axes[i].text(0.5, 0.5, "Sin variación", ha='center')
                axes[i].set_title(col)
                axes[i].set_xticks([])
                axes[i].set_yticks([])
                continue

            axes[i].boxplot(
                [data_base, data_adap],
                tick_labels=["Base", "Adaptive"],
                showfliers=True
            )

            axes[i].set_title(col)

            # Ajuste automático si parece proporción
            if data_base.max() <= 1 and data_adap.max() <= 1:
                axes[i].set_ylim(0, 1)

        for j in range(i + 1, len(axes)):
            fig.delaxes(axes[j])

        plt.tight_layout()

        output_dir = os.path.join(BASE_DIR, f"comparative_{pillar_name}", f"{pillar_name.capitalize()}_{group_name}")
        os.makedirs(output_dir, exist_ok=True)

        output_path = os.path.join(output_dir, f"{pillar_name}_{group_name}_Boxplots.png")
        plt.savefig(output_path, dpi=300)
        plt.close()

        print(f"[{pillar_name} - {group_name}] Guardado.")


# =====================================================
# EJECUCIÓN
# =====================================================

for pillar_name, columns in PILLARS.items():
    generate_plots_for_pillar(pillar_name, columns)

print("\n=== Todos los pilares procesados correctamente ===")