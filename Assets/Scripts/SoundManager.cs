using UnityEngine;
using System.Collections.Generic;

public class SoundManager : MonoBehaviour
{
    // 싱글톤 인스턴스
    public static SoundManager Instance;

    [Header("Audio Sources")]
    public AudioSource sfxSource;
    public AudioSource NPCSource;

    [Header("SFX")]
    public AudioClip scanSound;
    public AudioClip warningSound;
    public AudioClip trashSound;
    public AudioClip itemSound;
    [Header("NPC")]
    public AudioClip NPC1;
    public AudioClip NPC2;
    public AudioClip NPC3;
    public AudioClip NPC4;
    private void Awake()
    {
        // 싱글톤 설정: 이미 존재하면 파괴
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // 효과음 재생
    public void PlaySFX(AudioClip clip, float volume = 1.0f)
    {
        sfxSource.clip = clip;
        sfxSource.loop = true;
        sfxSource.PlayOneShot(clip, volume);
    }
    public void StopSFX()
    {
        sfxSource.Stop();
        sfxSource.loop = false;
    }
    public void PlayNPC(AudioClip clip, float volume = 1.0f)
    {
        NPCSource.PlayOneShot(clip, volume);
    }
}