import pandas as pd
import numpy as np
from scipy.stats import mannwhitneyu, fisher_exact
import os

# =========================
# CONFIG
# =========================

BIGGER_IS_BETTER = {
    "SafetyMargin_m": True,
}

ABS_IS_BETTER = {
    "VerticalSpeed_mps": True,
}

BOOTSTRAP_ITER = 20000
CI_LEVEL = 0.95
RANDOM_SEED = 42
np.random.seed(RANDOM_SEED)

# =========================
# Helpers
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

def orient_values(col, s):
    s = pd.to_numeric(s, errors="coerce")
    if ABS_IS_BETTER.get(col, False):
        return s.abs()
    return s

def classify_direction(col, med_base, med_adap):
    bigger = BIGGER_IS_BETTER.get(col, False)
    if bigger:
        return "Mejora descriptiva" if med_adap > med_base else ("Empeora" if med_adap < med_base else "Sin cambio")
    else:
        return "Mejora descriptiva" if med_adap < med_base else ("Empeora" if med_adap > med_base else "Sin cambio")

def pct_change(col, base, adap):
    if base == 0 or np.isnan(base) or np.isnan(adap):
        return np.nan
    bigger = BIGGER_IS_BETTER.get(col, False)
    if bigger:
        return (adap - base) / abs(base) * 100
    else:
        return (base - adap) / abs(base) * 100

# =========================
# SELECCIÓN DE ARCHIVO
# =========================

print("\nArchivos disponibles en esta carpeta:\n")
for f in os.listdir():
    if f.endswith(".xlsx"):
        print(" -", f)

file_path = input("\nEscribe el nombre exacto del archivo .xlsx: ").strip()

if not os.path.exists(file_path):
    print("Archivo no encontrado.")
    exit()

df = pd.read_excel(file_path)
df = df[df["Jugador"].notna()]

# =========================
# SELECCIÓN DE GRUPO
# =========================

print("\nSelecciona grupo a analizar:")
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
elif opcion == "4":
    grupo = "TODOS"
else:
    print("Opción inválida. Analizando TODOS.")
    grupo = "TODOS"

print(f"\nGrupo seleccionado: {grupo}")
print("Total jugadores en grupo:", len(df))

# =========================
# SELECCIÓN DE PILAR
# =========================

pillar = input("\nIngresa el nombre del pilar (ej: Landing, Orientation, Jitter): ").strip().upper()

# =========================
# SEPARAR BASE Y ADAP
# =========================

base = df[df["Jugador"].str.contains("_base", na=False)]
adap = df[df["Jugador"].str.contains("_adap", na=False)]

print("\nBase n =", len(base))
print("Adapt n =", len(adap))

# =========================
# ANÁLISIS
# =========================

cols = [c for c in df.columns if c != "Jugador"]

rows = []
bin_rows = []

for col in cols:

    xb = orient_values(col, base[col]).dropna()
    ya = orient_values(col, adap[col]).dropna()

    if len(xb) == 0 or len(ya) == 0:
        continue

    uniques = set(pd.concat([xb, ya]).unique())

    if uniques.issubset({0, 1}):

        b1 = int((xb == 1).sum())
        b0 = int((xb == 0).sum())
        a1 = int((ya == 1).sum())
        a0 = int((ya == 0).sum())

        table = [[b1, b0], [a1, a0]]
        _, p = fisher_exact(table)

        rate_b = b1 / (b1 + b0) if (b1 + b0) else np.nan
        rate_a = a1 / (a1 + a0) if (a1 + a0) else np.nan

        pct = (rate_b - rate_a) / rate_b * 100 if rate_b and not np.isnan(rate_a) else np.nan

        bin_rows.append([col, rate_b, rate_a, pct, p])
        continue

    U, p = mannwhitneyu(xb, ya, alternative="two-sided", method="auto")
    z, r = effect_r_from_u(U, len(xb), len(ya))

    med_b = float(np.median(xb))
    med_a = float(np.median(ya))
    delta = med_a - med_b
    pct = pct_change(col, med_b, med_a)
    direction = classify_direction(col, med_b, med_a)

    lo, hi = bootstrap_ci_delta(xb, ya, iters=BOOTSTRAP_ITER, ci=CI_LEVEL)

    rows.append([col, med_b, med_a, delta, pct, U, p, r, lo, hi, direction])

df_cont = pd.DataFrame(rows, columns=[
    "Metrica", "Mediana_Base", "Mediana_Adap",
    "Delta(Adap-Base)", "%Mejora(+)",
    "U", "p", "effect_r",
    f"CI{int(CI_LEVEL*100)}_lo",
    f"CI{int(CI_LEVEL*100)}_hi",
    "Lectura_UX"
]).sort_values("p")

df_bin = pd.DataFrame(bin_rows, columns=[
    "Metrica", "Tasa_Base(1)", "Tasa_Adap(1)",
    "%Mejora(+)", "p_fisher"
]).sort_values("p_fisher")

print("\n===== CONTINUAS =====")
print(df_cont.to_string(index=False))

print("\n===== BINARIAS =====")
print(df_bin.to_string(index=False))

# =========================
# EXPORT
# =========================

df_cont.to_excel(f"RESULTADOS_{pillar}_{grupo}_CONTINUAS.xlsx", index=False)
df_bin.to_excel(f"RESULTADOS_{pillar}_{grupo}_BINARIAS.xlsx", index=False)

print("\nArchivos exportados correctamente.")