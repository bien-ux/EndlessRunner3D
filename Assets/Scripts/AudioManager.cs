
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("AUDIO SOURCE")]
    public AudioSource musicSource;
    public AudioSource sfxSource;

    [Header("BACKGROUND MUSIC")]
    public AudioClip backgroundMusic;

    [Header("GAME SFX")]
    public AudioClip jumpSound;
    public AudioClip landSound;
    public AudioClip coinSound;
    public AudioClip hitSound;
    public AudioClip gameOverSound;
    public AudioClip buttonClickSound;
    public AudioClip startGameSound;

    [Header("VOLUME")]
    [Range(0f, 1f)] public float musicVolume = 0.35f;
    [Range(0f, 1f)] public float sfxVolume = 0.8f;

    private bool gameOverPlayed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        SetupAudioSources();
    }

    private void Start()
    {
        PlayBackgroundMusic();
    }

    private void SetupAudioSources()
    {
        if (musicSource == null)
            musicSource = gameObject.AddComponent<AudioSource>();

        if (sfxSource == null)
            sfxSource = gameObject.AddComponent<AudioSource>();

        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.volume = musicVolume;

        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.volume = sfxVolume;
    }

    public void PlayBackgroundMusic()
    {
        if (backgroundMusic == null || musicSource == null)
            return;

        musicSource.clip = backgroundMusic;
        musicSource.volume = musicVolume;
        musicSource.loop = true;

        if (!musicSource.isPlaying)
            musicSource.Play();
    }

    public void PlaySFX(AudioClip clip)
    {
        if (clip == null || sfxSource == null)
            return;

        sfxSource.PlayOneShot(clip, sfxVolume);
    }

    public void PlayJump()
    {
        PlaySFX(jumpSound);
    }

    public void PlayLand()
    {
        PlaySFX(landSound);
    }

    public void PlayCoin()
    {
        PlaySFX(coinSound);
    }

    public void PlayHit()
    {
        PlaySFX(hitSound);
    }

    public void PlayButtonClick()
    {
        PlaySFX(buttonClickSound);
    }

    public void PlayStartGame()
    {
        gameOverPlayed = false;
        PlaySFX(startGameSound);
    }

    public void PlayGameOver()
    {
        if (gameOverPlayed)
            return;

        gameOverPlayed = true;
        PlaySFX(gameOverSound);
    }

    public void SetMusicVolume(float value)
    {
        musicVolume = Mathf.Clamp01(value);

        if (musicSource != null)
            musicSource.volume = musicVolume;
    }

    public void SetSFXVolume(float value)
    {
        sfxVolume = Mathf.Clamp01(value);

        if (sfxSource != null)
            sfxSource.volume = sfxVolume;
    }

    public void StopMusic()
    {
        if (musicSource != null)
            musicSource.Stop();
    }

    public void ResumeMusic()
    {
        if (musicSource != null && !musicSource.isPlaying)
            musicSource.Play();
    }
}
