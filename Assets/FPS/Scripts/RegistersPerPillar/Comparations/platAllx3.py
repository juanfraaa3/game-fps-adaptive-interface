import os
import pandas as pd
import matplotlib.pyplot as plt
import numpy as np

# =====================================================
# CONFIG GENERAL
# =====================================================

BASE_DIR = os.path.dirname(os.path.abspath(__file__))

GROUPS = {
    "noob": "_noob",
    "regular": "_regular",
    "expert": "_expert"
}

# =====================================================
# DEFINICIÓN DE PILARES Y COLUMNAS
# =====================================================

PILLARS = {

    "orientation": [
        "TotalDuration_s",
        "AverageAngle_deg",
        "TimeLookingAway_s",
        "PercentTimeLookingAway",
        "ReorientationCount",
        "EndedInDeath"
    ],

    "landing": [
        "LandingOffset_m",
        "LandingOffset_Forward_m",
        "LandingOffset_Side_m",
        "SafetyMargin_m",
        "VerticalSpeed_mps",
        "ApproachAngle_deg",
        "MicroCorrections_0_5s",
        "PreLandingHorizontalJitter",
        "PostStabilizationTime_s",
        "PostLandingDrift_m",
        "LandingFailed",
        "PostLandingFall",
        "AbandonoPilar"
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
        "RawCorrectionsCount",
        "AbandonoPilar"
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
        "Cleared",
        "ObstacleTouched",
        "ContactDuration_s",
        "ContactCount",
        "FirstContactTime_s",
        "JetpackActiveTime_s",
        "CrouchTime_s",
        "AirborneTime_s",
        "ContactX",
        "ContactY",
        "ContactZ",
        "AbandonoPillar"
    ],

    "trajectory": [
        "Duration",
        "Efficiency_base",
        "ZigZag_Average",
        "Curvature_Normalized",
        "MaxLateralOffset",
        "LandingError",
        "TotalDistance",
        "VerticalOscillation",
        "EndedInDeath",
        "AbandonoPilar"
    ],

    "jitter": [
        "CurrentJitter",
        "SmoothedJitter",
        "IsHard",
        "JitterPeak",
        "JitterVariance",
        "JitterStdDev",
        "JitterEvents",
        "OvershootCount",
        "JitterArea",
        "JitterDirectionBias",
        "JitterClusters",
        "AbandonoPilar"
    ],

    "generalstats": [
        "Tiempo Absoluto (seg)",
        "Tiempo Desde Anterior (seg)",
        "Muertes entre checkpoints",
        "Muertes totales",
        "Muertes por enemigos",
        "Muertes por vacío",
        "Enemigos eliminados",
        "Disparos Blaster",
        "Acertados Blaster",
        "Fallados Blaster",
        "Precisión Blaster (%)",
        "Disparos Escopeta",
        "Acertados Escopeta",
        "Fallados Escopeta",
        "Precisión Escopeta (%)",
        "Disparos Totales",
        "Acertados Totales",
        "Fallados Totales",
        "Precisión Total (%)"
    ]
}

# =====================================================
# FUNCIÓN DE PLOTEO
# =====================================================

def generate_plots_for_pillar(pillar_name, columns):

    excel_path = os.path.join(
        BASE_DIR,
        f"comparative_{pillar_name}",
        f"comparative_{pillar_name}.xlsx"
    )

    if not os.path.exists(excel_path):
        print(f"[{pillar_name}] No se encontró el archivo.")
        return

    df = pd.read_excel(excel_path)
    df = df[df["Jugador"].notna()]

    output_dir = os.path.join(
        BASE_DIR,
        f"comparative_{pillar_name}",
        pillar_name.capitalize()
    )
    os.makedirs(output_dir, exist_ok=True)

    valid_cols = [c for c in columns if c in df.columns]

    for col in valid_cols:

        fig, axes = plt.subplots(1, 3, figsize=(15, 5))
        fig.suptitle(col)

        global_values = []

        group_data = {}

        # =====================================================
        # PRIMER PASO: recolectar datos para eje Y global
        # =====================================================

        for group_name, group_filter in GROUPS.items():

            df_group = df[df["Jugador"].str.contains(group_filter, na=False)]
            base = df_group[df_group["Jugador"].str.contains("_base", na=False)]
            adap = df_group[df_group["Jugador"].str.contains("_adap", na=False)]

            data_base = pd.to_numeric(base[col], errors="coerce").dropna()
            data_adap = pd.to_numeric(adap[col], errors="coerce").dropna()

            group_data[group_name] = (data_base, data_adap)

            if len(data_base) > 0:
                global_values.extend(data_base.tolist())
            if len(data_adap) > 0:
                global_values.extend(data_adap.tolist())

        if len(global_values) == 0:
            plt.close()
            continue

        global_min = min(global_values)
        global_max = max(global_values)

        # Margen pequeño para estética
        margin = (global_max - global_min) * 0.05
        if margin == 0:
            margin = 0.1

        y_min = global_min - margin
        y_max = global_max + margin

        # Si es proporción
        if global_max <= 1:
            y_min = 0
            y_max = 1

        # =====================================================
        # SEGUNDO PASO: dibujar con mismo eje Y
        # =====================================================

        for idx, group_name in enumerate(GROUPS.keys()):

            ax = axes[idx]
            data_base, data_adap = group_data[group_name]

            ax.set_title(group_name.capitalize())

            if len(data_base) == 0 or len(data_adap) == 0:
                ax.text(0.5, 0.5, "Sin datos", ha='center')
                ax.set_xticks([])
                ax.set_yticks([])
                continue

            if data_base.nunique() <= 1 and data_adap.nunique() <= 1:
                ax.text(0.5, 0.5, "Sin variación", ha='center')
                ax.set_xticks([])
                ax.set_yticks([])
                continue

            ax.boxplot(
                [data_base, data_adap],
                tick_labels=["Base", "Adaptive"],
                showfliers=True
            )

            ax.set_ylim(y_min, y_max)

        plt.tight_layout(rect=[0, 0, 1, 0.95])

        save_path = os.path.join(output_dir, f"{col}.png")
        plt.savefig(save_path, dpi=300)
        plt.close()

        print(f"[{pillar_name}] {col} guardado.")

# =====================================================
# EJECUCIÓN
# =====================================================

for pillar_name, columns in PILLARS.items():
    generate_plots_for_pillar(pillar_name, columns)

print("\n=== Todos los pilares procesados correctamente ===")