import pandas as pd
import numpy as np
from scipy.stats import mannwhitneyu, fisher_exact
import os

# =========================
# CONFIG
# =========================

BOOTSTRAP_ITER = 20000
CI_LEVEL = 0.95
RANDOM_SEED = 42
np.random.seed(RANDOM_SEED)

# =========================
# MÉTRICAS EXACTAS DE LOS BOXPLOTS
# =========================

GRAPH_METRICS = [

    # GENERAL STATS
    "Tiempo Desde Anterior (seg)",
    "Muertes entre checkpoints",
    "Muertes totales",
    "Precisión Blaster (%)",
    "Precisión Escopeta (%)",
    "Precisión Total (%)",

    # MULTITASKING
    "LapDuration_s",
    "AttemptDuration_s",
    "LapProgress_0_1",
    "DeathOccurred",
    "MeanDeviation_m",
    "SpeedSD_mps",
    "MicroCorrectionsRate_per_s",
    "ObstacleHitRate",
    "AbandonoPilar",

    # JITTER
    "CurrentJitter",
    "SmoothedJitter",
    "JitterPeak",
    "JitterVariance",
    "JitterStdDev",
    "OvershootCount",
    "JitterArea",

    # OBSTACLES
    "TimeToClear_s",
    "ObstacleTouched",
    "ContactDuration_s",
    "ContactX",
    "ContactY",
    "ContactZ",

    # MOVEMENT
    "TimeOnPlatform",
    "RelativePositionStd",
    "RelativePositionMean",
    "RelativePositionMax",
    "RelativePositionMin",
    "SampleCount",
    "EdgeRiskTime",
    "CorrectionPeaks",
    "RawCorrectionsCount",

    # LANDING
    "LandingOffset_m",
    "LandingOffset_Forward_m",
    "LandingOffset_Side_m",
    "SafetyMargin_m",
    "MicroCorrections_0_5s",
    "PreLandingHorizontalJitter",
    "PostStabilizationTime_s",
    "PostLandingDrift_m",
    "LandingFailed",
    "PostLandingFall",

    # TRAJECTORY
    "Duration",
    "ZigZag_Average",
    "Curvature_Normalized",
    "LandingError",
    "VerticalOscillation",

    # ORIENTATION
    "AverageAngle_deg",
    "TimeLookingAway_s",
    "PercentTimeLookingAway",
]

# =========================
# HELPERS
# =========================

def bootstrap_ci_delta(x, y, iters=20000, ci=0.95, stat=np.median):
    x = np.asarray(x)
    y = np.asarray(y)
    n1, n2 = len(x), len(y)
    deltas = np.empty(iters)
    for i in range(iters):
        xb = np.random.choice(x, size=n1, replace=True)
        yb = np.random.choice(y, size=n2, replace=True)
        deltas[i] = stat(yb) - stat(xb)
    alpha = (1 - ci) / 2
    lo = np.quantile(deltas, alpha)
    hi = np.quantile(deltas, 1 - alpha)
    return lo, hi

def effect_r_from_u(U, n1, n2):
    mean_U = n1 * n2 / 2
    std_U = np.sqrt(n1 * n2 * (n1 + n2 + 1) / 12)
    z = (U - mean_U) / std_U
    r = abs(z) / np.sqrt(n1 + n2)
    return z, r

# =========================
# SELECCIÓN DE ARCHIVO
# =========================

print("\nArchivos disponibles:\n")
for f in os.listdir():
    if f.endswith(".xlsx"):
        print(" -", f)

file_path = input("\nEscribe el nombre exacto del archivo .xlsx: ").strip()

if not os.path.exists(file_path):
    print("Archivo no encontrado.")
    exit()

df = pd.read_excel(file_path)

if "Jugador" not in df.columns:
    print("ERROR: El archivo debe tener una columna llamada 'Jugador'")
    exit()

df = df[df["Jugador"].notna()]

# =========================
# SELECCIÓN DE GRUPO
# =========================

print("\nSelecciona grupo:")
print("1 - Noobs")
print("2 - Regulares")
print("3 - Expertos")
print("4 - Todos")

opcion = input("Ingresa opción (1/2/3/4): ").strip()

if opcion == "1":
    df = df[df["Jugador"].str.contains("_noob", case=False, na=False)]
    grupo = "NOOBS"
elif opcion == "2":
    df = df[df["Jugador"].str.contains("_regular", case=False, na=False)]
    grupo = "REGULARES"
elif opcion == "3":
    df = df[df["Jugador"].str.contains("_expert", case=False, na=False)]
    grupo = "EXPERTOS"
else:
    grupo = "TODOS"

base = df[df["Jugador"].str.contains("_base", na=False)]
adap = df[df["Jugador"].str.contains("_adap", na=False)]

print(f"\nGrupo seleccionado: {grupo}")
print("Base n =", len(base))
print("Adapt n =", len(adap))

# =========================
# FILTRAR MÉTRICAS EXISTENTES
# =========================

cols = [m for m in GRAPH_METRICS if m in df.columns]

print("\nMétricas encontradas en archivo:")
for c in cols:
    print(" -", c)

# =========================
# ANÁLISIS
# =========================

rows = []
bin_rows = []

for col in cols:

    xb = pd.to_numeric(base[col], errors="coerce").dropna()
    ya = pd.to_numeric(adap[col], errors="coerce").dropna()

    if len(xb) == 0 or len(ya) == 0:
        continue

    uniques = set(pd.concat([xb, ya]).unique())

    # Binarias
    if uniques.issubset({0, 1}):

        b1 = int((xb == 1).sum())
        b0 = int((xb == 0).sum())
        a1 = int((ya == 1).sum())
        a0 = int((ya == 0).sum())

        table = [[b1, b0], [a1, a0]]
        _, p = fisher_exact(table)

        rate_b = b1 / (b1 + b0) if (b1 + b0) else np.nan
        rate_a = a1 / (a1 + a0) if (a1 + a0) else np.nan

        bin_rows.append([col, rate_b, rate_a, p])
        continue

    # Continuas
    U, p = mannwhitneyu(xb, ya, alternative="two-sided")
    z, r = effect_r_from_u(U, len(xb), len(ya))

    med_b = float(np.median(xb))
    med_a = float(np.median(ya))
    delta = med_a - med_b

    lo, hi = bootstrap_ci_delta(xb, ya, iters=BOOTSTRAP_ITER, ci=CI_LEVEL)

    rows.append([col, med_b, med_a, delta, U, p, r, lo, hi])

# =========================
# EXPORTAR
# =========================

df_cont = pd.DataFrame(rows, columns=[
    "Metrica",
    "Mediana_Base",
    "Mediana_Adap",
    "Delta(Adap-Base)",
    "U",
    "p",
    "effect_r",
    f"CI{int(CI_LEVEL*100)}_lo",
    f"CI{int(CI_LEVEL*100)}_hi"
]).sort_values("p")

df_bin = pd.DataFrame(bin_rows, columns=[
    "Metrica",
    "Tasa_Base(1)",
    "Tasa_Adap(1)",
    "p_fisher"
]).sort_values("p_fisher")

df_cont.to_excel(f"RESULTADOS_COMPLETOS_{grupo}_CONTINUAS.xlsx", index=False)
df_bin.to_excel(f"RESULTADOS_COMPLETOS_{grupo}_BINARIAS.xlsx", index=False)

print("\nAnálisis terminado.")
print("Archivos exportados correctamente.")