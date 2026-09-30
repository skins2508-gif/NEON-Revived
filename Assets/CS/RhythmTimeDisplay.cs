using UnityEngine;
using UnityEngine.InputSystem;

public class RhythmTimeDisplay : MonoBehaviour
{
    [Header("음악")]
    [SerializeField] private AudioSource musicSource;

    [Header("리듬 설정")]
    [SerializeField] private float bpm = 123.05f;
    [SerializeField] private int beatsPerMeasure = 4;

    [Header("플레이어 이동")]
    [SerializeField] private float playerStartX = 0f;
    [SerializeField] private float runSpeed = 20f;

    private float beatInterval;

    private void Awake()
    {
        beatInterval = 60f / bpm;
    }

    private void Update()
    {
        if (musicSource == null || Keyboard.current == null)
            return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            RecordMemo("장애물"); // 1번
        }

        if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            RecordMemo("중력 반전"); //2번
        }

        if (Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            RecordMemo("롱비트"); //3번
        }

        if (Keyboard.current.digit4Key.wasPressedThisFrame)
        {
            RecordMemo("연타"); //4번
        }

        if (RunKeyBindings.Pressed(RunKeyBindings.Gravity))
        {
            RecordMemo("중력 반전");
        }
    }

    public void RecordMemo(string type)
    {
        float songTime = musicSource.time;

        int totalBeatIndex = Mathf.FloorToInt(songTime / beatInterval);
        int measure = totalBeatIndex / beatsPerMeasure + 1;
        int beatInMeasure = totalBeatIndex % beatsPerMeasure + 1;

        float placementX = playerStartX + songTime * runSpeed;
        float placementY = transform.position.y;
        int minutes = Mathf.FloorToInt(songTime / 60f);
        float seconds = songTime % 60f;

        Debug.Log(
            $"[{type}] " +
            $"시간 {minutes:00}:{seconds:00.000} | " +
            $"마디 {measure}, {beatInMeasure}박 | " +
            $"배치 X = {placementX:F1} " +
            $"Y = {placementY:F2}"
        );
    }
}
