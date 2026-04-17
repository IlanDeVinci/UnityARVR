using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using System.Collections;

/// <summary>
/// Récupère les données de sensor depuis Chain API (MIT Media Lab)
/// et les affiche dans un TextMeshProUGUI.
/// Rafraîchit automatiquement toutes les `refreshInterval` secondes.
/// </summary>
public class SensorDataFetcher : MonoBehaviour
{
    [Header("API")]
    [SerializeField] private string sensorUrl = "https://chain-api.media.mit.edu/scalar_sensors/12085";
    [Tooltip("URL optionnelle pour un second capteur (ex: humidité).")]
    [SerializeField] private string secondSensorUrl = "";

    [Header("Display")]
    public TMP_Text displayText;
    [SerializeField] private float refreshInterval = 10f;

    [System.Serializable]
    private class ScalarSensor
    {
        public string metric;
        public float value;
        public string unit;
        public string updated;
    }

    private void Start()
    {
        if (displayText == null)
            displayText = GetComponent<TMP_Text>();

        if (displayText != null)
            displayText.text = "Chargement des capteurs...";

        StartCoroutine(FetchLoop());
    }

    private IEnumerator FetchLoop()
    {
        while (true)
        {
            yield return StartCoroutine(FetchAll());
            yield return new WaitForSeconds(refreshInterval);
        }
    }

    private IEnumerator FetchAll()
    {
        string result1 = null;
        string result2 = null;

        yield return StartCoroutine(Fetch(sensorUrl, v => result1 = v));

        if (!string.IsNullOrEmpty(secondSensorUrl))
            yield return StartCoroutine(Fetch(secondSensorUrl, v => result2 = v));

        if (displayText != null)
        {
            string text = "<b>CAPTEURS MIT</b>\n";
            if (result1 != null) text += result1;
            else text += "Capteur 1 indisponible";

            if (!string.IsNullOrEmpty(secondSensorUrl))
            {
                text += "\n";
                text += result2 ?? "Capteur 2 indisponible";
            }

            displayText.text = text;
        }
    }

    private IEnumerator Fetch(string url, System.Action<string> onResult)
    {
        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            req.timeout = 5;
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[SensorDataFetcher] Erreur {url}: {req.error}");
                onResult?.Invoke(null);
                yield break;
            }

            string json = req.downloadHandler.text;
            try
            {
                ScalarSensor data = JsonUtility.FromJson<ScalarSensor>(json);
                string metric = FormatMetric(data.metric);
                string unit = CleanUnit(data.unit);
                string formatted = $"<color=#FFD050>{metric}</color> : <b>{data.value:F2}</b> {unit}";
                onResult?.Invoke(formatted);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[SensorDataFetcher] JSON parse error: {e.Message}");
                onResult?.Invoke(null);
            }
        }
    }

    private static string FormatMetric(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "Capteur";
        string humanReadable = raw.Replace('_', ' ');

        // Traductions FR
        if (humanReadable.Contains("water temperature")) return "Température eau";
        if (humanReadable.Contains("temperature")) return "Température";
        if (humanReadable.Contains("humidity")) return "Humidité";
        if (humanReadable.Contains("light")) return "Luminosité";
        if (humanReadable.Contains("pressure")) return "Pression";

        // Capitalize
        return char.ToUpper(humanReadable[0]) + humanReadable.Substring(1);
    }

    private static string CleanUnit(string unit)
    {
        if (string.IsNullOrEmpty(unit)) return "";
        // Le caractère degré est mal encodé dans l'API — le remplacer
        return unit.Replace("\ufffd", "°").Replace("\uFFFD", "°");
    }
}
