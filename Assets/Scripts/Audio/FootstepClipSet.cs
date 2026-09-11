using UnityEngine;

/// <summary>
/// Walk and sprint clip slots for one ground material.
/// Leave arrays empty until assets are assigned.
/// </summary>
[System.Serializable]
public class FootstepClipSet
{
    [SerializeField] private FootstepSurfaceKind surface = FootstepSurfaceKind.Default;
    [SerializeField] private AudioClip[] walkClips;
    [SerializeField] private AudioClip[] sprintClips;

    public FootstepSurfaceKind Surface => surface;
    public AudioClip[] WalkClips => walkClips;
    public AudioClip[] SprintClips => sprintClips;

    public FootstepClipSet()
    {
    }

    public FootstepClipSet(FootstepSurfaceKind kind)
    {
        surface = kind;
    }

    public AudioClip[] Resolve(bool sprinting)
    {
        if (sprinting && HasAny(sprintClips))
            return sprintClips;

        return HasAny(walkClips) ? walkClips : sprintClips;
    }

    public static bool HasAny(AudioClip[] clips)
    {
        if (clips == null)
            return false;

        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null)
                return true;
        }

        return false;
    }
}
