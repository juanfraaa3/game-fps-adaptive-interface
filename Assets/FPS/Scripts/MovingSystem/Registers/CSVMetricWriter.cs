using System;
using System.IO;
using UnityEngine;
using System.Globalization;

public static class CSVMetricWriter
{
    private static readonly string BasePath =
        "Assets/FPS/Scripts/MovingSystem/Registers/";

    private static string sessionTimestamp;

    // Se llama una sola vez por sesión
    public static void InitializeSession()
    {
        if (!string.IsNullOrEmpty(sessionTimestamp))
            return;

        sessionTimestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
    }

    public static void WriteLine(string baseFileName, string header, string line)
    {
        InitializeSession();

        string fileName = $"{baseFileName}_{sessionTimestamp}.csv";
        string fullPath = Path.Combine(BasePath, fileName);

        if (!Directory.Exists(BasePath))
            Directory.CreateDirectory(BasePath);

        bool fileExists = File.Exists(fullPath);

        using (StreamWriter sw = new StreamWriter(fullPath, true))
        {
            if (!fileExists)
                sw.WriteLine(header);

            // 🔒 Forzar cultura invariante
            sw.WriteLine(
                string.Format(
                CultureInfo.GetCultureInfo("es-ES"),
                    "{0}",
                    line
                )
            );
        }
    }


}
