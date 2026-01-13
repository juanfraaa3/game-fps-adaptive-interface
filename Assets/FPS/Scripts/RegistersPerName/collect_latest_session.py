import os
from datetime import datetime
import pandas as pd
import re

# =====================================================
# CONFIGURACION GENERAL
# =====================================================

BASE_PATH = r"C:\Users\Juanes\adaptative_microgame\Assets\FPS\Scripts"

SYSTEM_CONFIG = {
    "Orientation": {"path": "JetpackSystem/JetpackLogs", "decimal": "."},
    "Trajectory": {"path": "JetpackSystem/Trajectory", "decimal": "."},
    "Landing": {"path": "LandingSystem", "decimal": "."},
    "Multitasking": {"path": "MultitaskSystem", "decimal": "."},
    "Jitter": {"path": "JitterSystem/JitterLogs", "decimal": ","}
}

MOVING_REGISTERS_PATH = r"MovingSystem\Registers"
ANALYTICS_REGISTERS_PATH = r"Analytics\Registers"

PARTICIPANTS_BASE_FOLDER = r"C:\Users\Juanes\adaptative_microgame\Assets\FPS\Scripts\RegistersPerName"

# =====================================================
# UTILIDADES
# =====================================================

decimal_regex = re.compile(r"^-?\d+([,.]\d+)?$")

def normalize_decimal_string(value, decimal_sep):
    if not isinstance(value, str):
        return value
    v = value.strip()
    if not decimal_regex.match(v):
        return value
    return v.replace(",", ".") if decimal_sep == "," else v


def read_csv_preserve_everything(csv_path, decimal_sep):
    with open(csv_path, "r", encoding="utf-8", errors="ignore") as f:
        lines = [line.rstrip("\n") for line in f if line.strip()]

    header = lines[0].split(";")
    base_cols = len(header)

    parsed = []
    max_cols = base_cols
    for line in lines[1:]:
        parts = line.split(";")
        parsed.append(parts)
        max_cols = max(max_cols, len(parts))

    full_header = header[:]
    for i in range(base_cols, max_cols):
        full_header.append(f"ExtraCol_{i}")

    rows = []
    for parts in parsed:
        row = parts[:]
        while len(row) < max_cols:
            row.append(None)
        rows.append(row)

    df = pd.DataFrame(rows, columns=full_header)

    for col in df.columns:
        df[col] = df[col].apply(lambda v: normalize_decimal_string(v, decimal_sep))

    return df


def get_latest_n_csv(folder, n=1):
    if not os.path.exists(folder):
        return []

    csvs = [
        os.path.join(folder, f)
        for f in os.listdir(folder)
        if f.lower().endswith(".csv")
    ]

    csvs.sort(key=os.path.getmtime, reverse=True)
    return csvs[:n]

# =====================================================
# EJECUCION
# =====================================================

participant_name = input("Nombre del participante: ").strip()
while not participant_name:
    participant_name = input("Nombre invalido. Ingresa nuevamente: ").strip()

participant_folder = os.path.join(PARTICIPANTS_BASE_FOLDER, participant_name)
os.makedirs(participant_folder, exist_ok=True)

timestamp = datetime.now().strftime("%Y-%m-%d_%H-%M-%S")
output_file = os.path.join(
    participant_folder,
    f"{participant_name}_Session_{timestamp}.xlsx"
)

print("\nConsolidando CSVs (sesión completa)...\n")

with pd.ExcelWriter(output_file, engine="openpyxl") as writer:

    # ---------- SISTEMAS NORMALES ----------
    for sheet, cfg in SYSTEM_CONFIG.items():
        folder = os.path.join(BASE_PATH, cfg["path"])
        latest = get_latest_n_csv(folder, n=1)

        if not latest:
            print("[WARN] No CSV en:", folder)
            continue

        csv_path = latest[0]
        print("[OK]", sheet, "->", os.path.basename(csv_path))

        df = read_csv_preserve_everything(csv_path, cfg["decimal"])
        df.to_excel(writer, sheet_name=sheet, index=False)

    # ---------- MOVING SYSTEM (ULTIMOS 2, CLASIFICADOS) ----------
    moving_folder = os.path.join(BASE_PATH, MOVING_REGISTERS_PATH)
    last_two = get_latest_n_csv(moving_folder, n=2)

    obstacle_df = None
    movement_df = None

    for csv in last_two:
        name = os.path.basename(csv)

        if "ObstacleGateMetrics" in name:
            obstacle_df = read_csv_preserve_everything(csv, ".")
            print("[OK] Obstacle ->", name)

        elif "MovementMetrics" in name:
            movement_df = read_csv_preserve_everything(csv, ",")
            print("[OK] Movement ->", name)

        else:
            print("[WARN] CSV no reconocido:", name)

    if obstacle_df is not None:
        obstacle_df.to_excel(writer, sheet_name="Obstacle", index=False)

    if movement_df is not None:
        movement_df.to_excel(writer, sheet_name="Movement", index=False)

    # ---------- ANALYTICS / GENERAL STATS ----------
    analytics_folder = os.path.join(BASE_PATH, ANALYTICS_REGISTERS_PATH)
    latest_analytics = get_latest_n_csv(analytics_folder, n=1)

    if latest_analytics:
        csv_path = latest_analytics[0]
        print("[OK] General Stats ->", os.path.basename(csv_path))

        df = read_csv_preserve_everything(csv_path, ".")
        df.to_excel(writer, sheet_name="General Stats", index=False)
    else:
        print("[WARN] No CSV en:", analytics_folder)

print("\nSesion creada correctamente:")
print(output_file)
