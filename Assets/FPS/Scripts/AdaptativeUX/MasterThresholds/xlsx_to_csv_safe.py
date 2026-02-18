import pandas as pd
import os
import sys

def convert_xlsx_to_csv(input_path, output_path=None):
    if not os.path.exists(input_path):
        print("❌ Archivo no encontrado:", input_path)
        return

    try:
        # Leer excel EXACTO sin modificar tipos
        df = pd.read_excel(
            input_path,
            engine="openpyxl",
            dtype=str  # 🔥 IMPORTANTÍSIMO: no alterar precisión
        )

        # Si no se define salida, usar mismo nombre
        if output_path is None:
            base = os.path.splitext(input_path)[0]
            output_path = base + ".csv"

        # Exportar con ; y sin índice
        df.to_csv(
            output_path,
            sep=";",
            index=False,
            encoding="utf-8"
        )

        print("Conversión exitosa")
        print("CSV guardado en:", output_path)

    except Exception as e:
        print("Error durante conversión:", str(e))


if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Uso:")
        print("python xlsx_to_csv_safe.py archivo.xlsx")
    else:
        input_file = sys.argv[1]
        convert_xlsx_to_csv(input_file)
