using UnityEngine;
using TMPro;

public class PlaytimeUI : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI timeText;

    void Update()
    {
        float t = Time.timeSinceLevelLoad;
        int minutes = (int)(t / 60f);
        int seconds = (int)(t % 60f);
        timeText.SetText("{0:00}:{1:00}", minutes, seconds);
    }
}
