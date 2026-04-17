using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using System.Collections;
using System;

/// <summary>
/// Récupère les données de sensor depuis Chain API (MIT Media Lab - Tidmarsh).
/// Structure HAL+JSON. Champs parsés : metric, value, unit, updated, active, dataType.
/// Affiche aussi le nom du device parent et l'âge de la dernière mesure.
/// </summary>
public class SensorDataFetcher : MonoBehaviour
{
    [Header("API")]
    [SerializeField] private string sensorUrl = "https://chain-api.media.mit.edu/scalar_sensors/12085";
    [Tooltip("URL optionnelle pour un second capteur (ex: humidité).")]
    [SerializeField] private string secondSensorUrl = "";

    [Header("Display")]
    public TMP_Text displayText;
    [SerializeField] private float refreshInterval = 15f;

    // ====== MODÈLE DE DONNÉES (parsing JsonUtility) ======

    [Serializable]
    private class ScalarSensor
    {
        public string metric;
        public float value;
        public string unit;
        public string updated;
        public bool active;
        public string dataType;
        public SensorLinks _links;
    }

    [Serializable]
    private class SensorLinks
    {
        [SerializeField] public Link self;
        [SerializeField] public Link chdevice; // mappé depuis "ch:device"
    }

    [Serializable]
    private class Link
    {
        public string href;
        public string title;
    }

    // ====== CYCLE DE VIE ======

    private void Start()
    {
        if (displayText == null)
            displayText = GetComponent<TMP_Text>();

        if (displayText != null)
            displayText.text = "<i>Connexion au MIT Media Lab...</i>";

        StartCoroutine(FetchLoop());
    }

    private IEnumerator FetchLoop()
    {
        while (true)
        {
            yield return StartCoroutine(UpdateDisplay());
            yield return new WaitForSeconds(refreshInterval);
        }
    }

    private IEnumerator UpdateDisplay()
    {
        string text = "<size=16><b><color=#FFD050>◉ CAPTEURS TIDMARSH</color></b></size>\n";
        text += "<size=10><color=#888>MIT Media Lab · Chain API</color></size>\n\n";

        string line1 = null;
        string line2 = null;

        yield return StartCoroutine(FetchSensor(sensorUrl, v => line1 = v));

        if (!string.IsNullOrEmpty(secondSensorUrl))
            yield return StartCoroutine(FetchSensor(secondSensorUrl, v => line2 = v));

        text += line1 ?? "<color=#FF6060>Capteur indisponible</color>";

        if (!string.IsNullOrEmpty(secondSensorUrl))
        {
            text += "\n\n";
            text += line2 ?? "<color=#FF6060>Capteur indisponible</color>";
        }

        if (displayText != null)
            displayText.text = text;
    }

    // ====== REQUÊTE + FORMATAGE ======

    private IEnumerator FetchSensor(string url, Action<string> onResult)
    {
        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            req.timeout = 5;
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[SensorDataFetcher] {url}: {req.error}");
                onResult?.Invoke(null);
                yield break;
            }

            string json = req.downloadHandler.text;

            // JsonUtility ne supporte pas les clés contenant ':' (ch:device)
            // On remplace avant parsing
            json = json.Replace("\"ch:device\"", "\"chdevice\"");

            ScalarSensor data;
            try
            {
                data = JsonUtility.FromJson<ScalarSensor>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SensorDataFetcher] JSON parse: {e.Message}");
                onResult?.Invoke(null);
                yield break;
            }

            onResult?.Invoke(FormatSensor(data));
        }
    }

    private static string FormatSensor(ScalarSensor d)
    {
        if (d == null || string.IsNullOrEmpty(d.metric))
            return null;

        string label = TranslateMetric(d.metric);
        string unit = CleanUnit(d.unit);
        string icon = GetIcon(d.metric);
        string statusDot = d.active
            ? "<color=#40D060>●</color>"
            : "<color=#FF5050>●</color>";

        string deviceName = d._links?.chdevice?.title ?? "?";
        string age = FormatAge(d.updated);

        // Valeur principale en grand
        string line = $"{statusDot} {icon} <b>{label}</b>\n";
        line += $"  <size=28><b><color=#4DD0E1>{d.value:F2}</color></b></size> <size=14>{unit}</size>\n";
        line += $"  <size=10><color=#AAA>Device: {deviceName} · MAJ: {age}</color></size>";

        return line;
    }

    // ====== HELPERS ======

    private static string TranslateMetric(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "Capteur";
        string lower = raw.ToLower();

        if (lower.Contains("water_temperature") || lower.Contains("water temperature"))
            return "Température de l'eau";
        if (lower.Contains("air_temperature") || lower.Contains("temperature"))
            return "Température";
        if (lower.Contains("humidity")) return "Humidité";
        if (lower.Contains("light")) return "Luminosité";
        if (lower.Contains("pressure")) return "Pression";
        if (lower.Contains("soil_moisture")) return "Humidité du sol";
        if (lower.Contains("co2")) return "CO₂";
        if (lower.Contains("wind")) return "Vent";

        return char.ToUpper(raw[0]) + raw.Substring(1).Replace('_', ' ');
    }

    private static string GetIcon(string metric)
    {
        if (string.IsNullOrEmpty(metric)) return "📊";
        string m = metric.ToLower();
        if (m.Contains("temperature")) return "🌡";
        if (m.Contains("humidity") || m.Contains("water")) return "💧";
        if (m.Contains("light")) return "☀";
        if (m.Contains("pressure")) return "⏱";
        if (m.Contains("wind")) return "🌬";
        return "◈";
    }

    private static string CleanUnit(string unit)
    {
        if (string.IsNullOrEmpty(unit)) return "";
        // L'API renvoie le caractère remplacement \ufffd au lieu de °
        return unit.Replace("\ufffd", "°").Replace("\uFFFD", "°");
    }

    private static string FormatAge(string isoDate)
    {
        if (string.IsNullOrEmpty(isoDate)) return "inconnu";

        if (!DateTime.TryParse(isoDate, null,
            System.Globalization.DateTimeStyles.AdjustToUniversal |
            System.Globalization.DateTimeStyles.AssumeUniversal, out DateTime dt))
            return isoDate;

        TimeSpan age = DateTime.UtcNow - dt;
        if (age.TotalSeconds < 0) return "futur";
        if (age.TotalMinutes < 1) return "à l'instant";
        if (age.TotalMinutes < 60) return $"il y a {(int)age.TotalMinutes} min";
        if (age.TotalHours < 24) return $"il y a {(int)age.TotalHours} h";
        if (age.TotalDays < 30) return $"il y a {(int)age.TotalDays} j";
        if (age.TotalDays < 365) return $"il y a {(int)(age.TotalDays / 30)} mois";
        return $"il y a {(int)(age.TotalDays / 365)} an{((int)(age.TotalDays / 365) > 1 ? "s" : "")}";
    }
}
