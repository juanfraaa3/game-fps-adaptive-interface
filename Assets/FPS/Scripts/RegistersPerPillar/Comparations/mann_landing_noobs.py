import pandas as pd
import numpy as np
from scipy.stats import mannwhitneyu, fisher_exact


pd.set_option("display.max_columns", None)
pd.set_option("display.width", None)
pd.set_option("display.max_colwidth", None)
# =====================================
# CONFIGURACIÓN
# =====================================

file_path = "comparative_landing.xlsx"  # <-- CAMBIA POR TU ARCHIVO
sheet_name = 0

# =====================================
# CARGAR Y LIMPIAR
# =====================================

df = pd.read_excel(file_path, sheet_name=sheet_name)

# Eliminar filas vacías
df = df[df["Jugador"].notna()]

# Filtrar solo noobs (si quieres todos, comenta esta línea)
df = df[df["Jugador"].str.contains("noob", case=False, na=False)]

# Separar grupos
base = df[df["Jugador"].str.contains("_base", na=False)]
adap = df[df["Jugador"].str.contains("_adap", na=False)]

print("Base n =", len(base))
print("Adapt n =", len(adap))
print("\n")

# =====================================
# DETECTAR COLUMNAS NUMÉRICAS
# =====================================

columnas = [c for c in df.columns if c != "Jugador"]

resultados_cont = []
resultados_bin = []

for col in columnas:

    # Convertir a numérico
    x = pd.to_numeric(base[col], errors="coerce").dropna()
    y = pd.to_numeric(adap[col], errors="coerce").dropna()

    if len(x) == 0 or len(y) == 0:
        continue

    # Detectar si es binaria (solo 0 y 1)
    valores_unicos = set(pd.concat([x, y]).unique())

    if valores_unicos.issubset({0, 1}):

        # =========================
        # FISHER EXACT
        # =========================

        base_1 = (x == 1).sum()
        base_0 = (x == 0).sum()

        adap_1 = (y == 1).sum()
        adap_0 = (y == 0).sum()

        tabla = [[base_1, base_0],
                 [adap_1, adap_0]]

        try:
            _, p = fisher_exact(tabla)
            resultados_bin.append([col, p])
        except:
            resultados_bin.append([col, "Error"])

    else:

        # =========================
        # MANN-WHITNEY
        # =========================

        U, p = mannwhitneyu(x, y, alternative="two-sided")

        n1 = len(x)
        n2 = len(y)

        mean_U = n1 * n2 / 2
        std_U = np.sqrt(n1 * n2 * (n1 + n2 + 1) / 12)

        z = (U - mean_U) / std_U
        r = abs(z) / np.sqrt(n1 + n2)

        resultados_cont.append([
            col,
            np.median(x),
            np.median(y),
            U,
            p,
            r
        ])

# =====================================
# MOSTRAR RESULTADOS
# =====================================

df_cont = pd.DataFrame(
    resultados_cont,
    columns=[
        "Metrica",
        "Mediana_Base",
        "Mediana_Adap",
        "U",
        "p_value",
        "effect_r"
    ]
)

df_bin = pd.DataFrame(
    resultados_bin,
    columns=["Metrica", "p_value_Fisher"]
)

print("===== RESULTADOS CONTINUAS (MANN-WHITNEY) =====")
print(df_cont.sort_values("p_value"))
print("\n")

print("===== RESULTADOS BINARIAS (FISHER) =====")
print(df_bin)