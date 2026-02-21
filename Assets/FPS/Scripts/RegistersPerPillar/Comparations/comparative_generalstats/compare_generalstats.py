import pandas as pd
import numpy as np
from scipy.stats import mannwhitneyu
import os

# =========================
# CONFIG
# =========================

file_path = "comparative_generalstats.xlsx"

BASE_OUTPUT_DIR = r"C:\Users\Juanes\adaptative_microgame\Assets\FPS\Scripts\RegistersPerPillar\Comparations\comparative_generalstats"

BOOTSTRAP_ITER = 20000
CI_LEVEL = 0.95
np.random.seed(42)

# =========================
# COLUMNAS
# =========================

GENERAL_COLUMNS = [
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

# =========================
# MAYOR ES MEJOR
# =========================

BIGGER_IS_BETTER = {
    "Enemigos eliminados": True,
    "Acertados Blaster": True,
    "Acertados Escopeta": True,
    "Acertados Totales": True,
    "Precisión Blaster (%)": True,
    "Precisión Escopeta (%)": True,
    "Precisión Total (%)": True
}

# =========================
# HELPERS
# =========================

def bootstrap_ci_delta(x, y, iters=20000, ci=0.95):
    x = np.asarray(x)
    y = np.asarray(y)
    n1, n2 = len(x), len(y)
    deltas = np.empty(iters)
    for i in range(iters):
        xb = np.random.choice(x, size=n1, replace=True)
        yb = np.random.choice(y, size=n2, replace=True)
        deltas[i] = np.median(yb) - np.median(xb)
    alpha = (1 - ci) / 2
    return np.quantile(deltas, alpha), np.quantile(deltas, 1 - alpha)

def effect_r_from_u(U, n1, n2):
    mean_U = n1 * n2 / 2
    std_U = np.sqrt(n1 * n2 * (n1 + n2 + 1) / 12)
    z = (U - mean_U) / std_U
    return abs(z) / np.sqrt(n1 + n2)

def pct_change(col, base, adap):
    if base == 0:
        return np.nan
    if BIGGER_IS_BETTER.get(col, False):
        return (adap - base) / abs(base) * 100
    else:
        return (base - adap) / abs(base) * 100

def is_improvement(col, med_b, med_a):
    if BIGGER_IS_BETTER.get(col, False):
        return med_a > med_b
    else:
        return med_a < med_b

# =========================
# LOAD DATA
# =========================

df = pd.read_excel(file_path)
df = df[df["Jugador"].notna()]

# =========================
# GRUPOS
# =========================

GROUPS = {
    "GeneralStats_noobs": "_noob",
    "GeneralStats_regulars": "_regular",
    "GeneralStats_experts": "_expert",
    "GeneralStats_all": None
}

os.makedirs(BASE_OUTPUT_DIR, exist_ok=True)

# =========================
# LOOP POR GRUPOS
# =========================

for folder_name, pattern in GROUPS.items():

    print("\n===================================")
    print("Analizando:", folder_name)
    print("===================================")

    group_output_dir = os.path.join(BASE_OUTPUT_DIR, folder_name)
    os.makedirs(group_output_dir, exist_ok=True)

    if pattern:
        df_group = df[df["Jugador"].str.contains(pattern, case=False, na=False)]
    else:
        df_group = df.copy()

    base = df_group[df_group["Jugador"].str.contains("_base", na=False)]
    adap = df_group[df_group["Jugador"].str.contains("_adap", na=False)]

    print("Base n =", len(base))
    print("Adapt n =", len(adap))

    rows = []

    for col in GENERAL_COLUMNS:

        if col not in df.columns:
            continue

        xb = pd.to_numeric(base[col], errors="coerce").dropna()
        ya = pd.to_numeric(adap[col], errors="coerce").dropna()

        if len(xb) == 0 or len(ya) == 0:
            continue

        U, p = mannwhitneyu(xb, ya, alternative="two-sided", method="auto")
        r = effect_r_from_u(U, len(xb), len(ya))

        med_b = float(np.median(xb))
        med_a = float(np.median(ya))
        delta = med_a - med_b
        pct = pct_change(col, med_b, med_a)

        lo, hi = bootstrap_ci_delta(xb, ya)

        if is_improvement(col, med_b, med_a):
            direction = "Mejora descriptiva"
        elif med_a == med_b:
            direction = "Sin cambio"
        else:
            direction = "Empeora"

        rows.append([
            col, med_b, med_a, delta, pct, U, p, r, lo, hi, direction
        ])

    df_cont = pd.DataFrame(rows, columns=[
        "Metrica",
        "Mediana_Base",
        "Mediana_Adap",
        "Delta(Adap-Base)",
        "%Mejora(+)",
        "U",
        "p",
        "effect_r",
        "CI95_lo",
        "CI95_hi",
        "Lectura_UX"
    ]).sort_values("p")

    print("\n===== CONTINUAS =====")
    print(df_cont.to_string(index=False))

    df_cont.to_excel(os.path.join(group_output_dir, "CONTINUAS.xlsx"), index=False)

print("\nAnalisis GENERAL STATS completo guardado por carpetas.")