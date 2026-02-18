import os
import pandas as pd

# ========= CONFIG =========
base_path = r"C:\Users\Juanes\adaptative_microgame\Assets\FPS\Scripts\RegistersPerName"
output_folder = r"C:\Users\Juanes\adaptative_microgame\Assets\FPS\Scripts\RegistersPerPillar"

sheets_to_copy = [
    "Orientation",
    "Trajectory",
    "Landing",
    "Multitasking",
    "Jitter",
    "Obstacle",
    "Movement",
    "GeneralStats"
]
# ===========================

for sheet_name in sheets_to_copy:

    output_file = os.path.join(output_folder, f"{sheet_name.replace(' ', '')}.xlsx")

    with pd.ExcelWriter(output_file, engine='openpyxl') as writer:

        for player_folder in os.listdir(base_path):
            player_path = os.path.join(base_path, player_folder)

            if os.path.isdir(player_path):

                for file in os.listdir(player_path):
                    if file.endswith(".xlsx") and not file.startswith("~$"):
                        file_path = os.path.join(player_path, file)

                        try:
                            df = pd.read_excel(file_path, sheet_name=sheet_name)
                            df.to_excel(writer, sheet_name=player_folder[:31], index=False)
                            print(f"Copiada hoja {sheet_name} de {player_folder}")

                        except Exception as e:
                            print(f"No se pudo copiar {sheet_name} de {player_folder}: {e}")

    print(f"Archivo {sheet_name} creado correctamente.\n")

print("Todos los archivos fueron generados.")
