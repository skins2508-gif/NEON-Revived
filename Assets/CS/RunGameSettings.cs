using UnityEngine;

[CreateAssetMenu(menuName = "Neon Runner/Game Settings")]
public class RunGameSettings : ScriptableObject
{
    public string playerObjectName = "Player";
    public Sprite defaultCharacterSprite;
    public Texture2D longNoteSliderTexture; // Legacy artwork fallback.
    public Texture2D chargeFrameTexture;
    public Texture2D chargeFillTexture;
    public Texture2D chargeCurrentTexture;
    public Sprite longNotePlayerSprite;
    public AudioClip fullTrack;
    [Tooltip("메인메뉴 배경음악 볼륨입니다. 게임 중 음악 및 메뉴 효과음과 별개입니다.")]
    [Range(0f, 1f)] public float menuMusicVolume = 0.1f;
    public AudioClip menuMoveSound;
    public AudioClip menuSelectSound;
}
