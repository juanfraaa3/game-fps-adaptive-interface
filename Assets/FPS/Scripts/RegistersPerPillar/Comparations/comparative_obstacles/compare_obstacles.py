import pandas as pd
import numpy as np
from scipy.stats import mannwhitneyu, fisher_exact
import os

# =========================
# CONFIG
# =========================

file_path = "comparative_obstacles.xlsx"

BASE_OUTPUT_DIR = r"C:\Users\Juanes\adaptative_microgame\Assets\FPS\Scripts\RegistersPerPillar\Comparations\comparative_obstacles"

BOOTSTRAP_ITER = 20000
CI_LEVEL = 0.95
np.random.seed(42)

# =========================
# COLUMNAS OBSTACLES
# =========================

OBSTACLE_COLUMNS = [
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
]

# =========================
# MAYOR ES MEJOR
# =========================

BIGGER_IS_BETTER = {
    "Cleared": True,
    "FirstContactTime_s": True
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
    r = abs(z) / np.sqrt(n1 + n2)
    return r

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
    "Obstacles_noobs": "_noob",
    "Obstacles_regulars": "_regular",
    "Obstacles_experts": "_expert",
    "Obstacles_all": None
}

os.makedirs(BASE_OUTPUT_DIR, exist_ok=True)

# =========================
# LOOP GRUPOS
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
    bin_rows = []

    for col in OBSTACLE_COLUMNS:

        if col not in df.columns:
            continue

        xb = pd.to_numeric(base[col], errors="coerce").dropna()
        ya = pd.to_numeric(adap[col], errors="coerce").dropna()

        if len(xb) == 0 or len(ya) == 0:
            continue

        uniques = set(pd.concat([xb, ya]).unique())

        # BINARIAS
        if uniques.issubset({0, 1}):

            b1 = int((xb == 1).sum())
            b0 = int((xb == 0).sum())
            a1 = int((ya == 1).sum())
            a0 = int((ya == 0).sum())

            table = [[b1, b0], [a1, a0]]
            _, p = fisher_exact(table)

            rate_b = b1 / (b1 + b0)
            rate_a = a1 / (a1 + a0)
            pct = (rate_b - rate_a) / rate_b * 100 if rate_b else np.nan

            bin_rows.append([col, rate_b, rate_a, pct, p])
            continue

        # CONTINUAS
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

    df_bin = pd.DataFrame(bin_rows, columns=[
        "Metrica",
        "Tasa_Base(1)",
        "Tasa_Adap(1)",
        "%Mejora(+)",
        "p_fisher"
    ]).sort_values("p_fisher")

    print("\n===== CONTINUAS =====")
    print(df_cont.to_string(index=False))

    print("\n===== BINARIAS =====")
    print(df_bin.to_string(index=False))

    df_cont.to_excel(os.path.join(group_output_dir, "CONTINUAS.xlsx"), index=False)
    df_bin.to_excel(os.path.join(group_output_dir, "BINARIAS.xlsx"), index=False)

print("\nAnalisis OBSTACLES completo guardado por carpetas.")