using UnityEngine;

/// <summary>
/// Applies resolved weapon-pose clips to the shared third-person Animator
/// without duplicating the player controller per weapon.
/// </summary>
public class ThirdPersonWeaponPoseBinder : MonoBehaviour
{
    public const string LayerName = "ThirdPersonWeaponPose";
    public const string LegacyLayerName = "WeaponPose";

    private static readonly int WeaponPoseStateHash = Animator.StringToHash("WeaponPoseState");
    private static readonly int WeaponPoseWeightHash = Animator.StringToHash("WeaponPoseWeight");
    private static readonly int PoseClassHash = Animator.StringToHash("PoseClass");
    private static readonly int PoseCategoryHash = Animator.StringToHash("PoseCategory");

    [SerializeField] private Animator thirdPersonAnimator;
    [SerializeField] private AnimationClip holdSlot;
    [SerializeField] private AnimationClip sprintSlot;
    [SerializeField] private AnimationClip proneSlot;
    [SerializeField] private AnimationClip aimSlot;
    [SerializeField] private AnimationClip crouchSlot;

    private AnimatorOverrideController overrideController;
    private RuntimeAnimatorController sourceController;
    private readonly AnimationClip[] lastApplied = new AnimationClip[5];
    private int cachedLayer = int.MinValue;

    public int PoseLayerIndex => ResolveLayerIndex();

    public void Bind(Animator animator)
    {
        thirdPersonAnimator = animator;
        EnsureOverride();
    }

    public void Apply(
        WeaponDefinition definition,
        ThirdPersonWeaponPoseKind kind,
        float poseWeight)
    {
        if (thirdPersonAnimator == null)
            return;

        EnsureOverride();
        ApplyResolvedClips(definition);

        if (HasParameter(WeaponPoseStateHash))
            thirdPersonAnimator.SetInteger(WeaponPoseStateHash, (int)kind);
        if (HasParameter(WeaponPoseWeightHash))
            thirdPersonAnimator.SetFloat(WeaponPoseWeightHash, poseWeight);
        if (HasParameter(PoseClassHash) && definition != null)
            thirdPersonAnimator.SetInteger(PoseClassHash, (int)definition.WeaponPoseClass);
        if (HasParameter(PoseCategoryHash) && definition != null)
            thirdPersonAnimator.SetInteger(PoseCategoryHash, definition.WeaponPoseClass == ThirdPersonWeaponPoseClass.ShortGun ? 0 : 1);

        int layer = ResolveLayerIndex();
        if (layer < 0)
            return;

        // REQ-049: locomotion stays on the base layer. Clip holds are disabled.
        thirdPersonAnimator.SetLayerWeight(layer, 0f);
    }

    public void PlayPreview(WeaponDefinition definition, ThirdPersonWeaponPoseKind kind, float normalizedTime)
    {
        if (thirdPersonAnimator == null)
            return;

        Apply(definition, kind, 1f);
        int layer = ResolveLayerIndex();
        if (layer < 0)
            return;

        string state = kind switch
        {
            ThirdPersonWeaponPoseKind.Sprint => "WeaponPose_Sprint",
            ThirdPersonWeaponPoseKind.Prone => "WeaponPose_Prone",
            ThirdPersonWeaponPoseKind.Aim => "WeaponPose_Aim",
            ThirdPersonWeaponPoseKind.Crouch => "WeaponPose_Crouch",
            _ => "WeaponPose_Hold"
        };

        int hash = Animator.StringToHash(state);
        if (thirdPersonAnimator.HasState(layer, hash))
            thirdPersonAnimator.Play(hash, layer, Mathf.Clamp01(normalizedTime));
    }

    public void AssignSlots(
        AnimationClip hold,
        AnimationClip sprint,
        AnimationClip prone,
        AnimationClip aim,
        AnimationClip crouch)
    {
        holdSlot = hold;
        sprintSlot = sprint;
        proneSlot = prone;
        aimSlot = aim;
        crouchSlot = crouch;
    }

    private void EnsureOverride()
    {
        if (thirdPersonAnimator == null)
            return;

        RuntimeAnimatorController current = thirdPersonAnimator.runtimeAnimatorController;
        if (current == null)
            return;

        RuntimeAnimatorController baseController = current;
        if (current is AnimatorOverrideController existingOverride)
            baseController = existingOverride.runtimeAnimatorController;

        if (overrideController != null && sourceController == baseController)
        {
            if (thirdPersonAnimator.runtimeAnimatorController != overrideController)
                thirdPersonAnimator.runtimeAnimatorController = overrideController;
            DiscoverSlotsIfNeeded();
            return;
        }

        sourceController = baseController;
        overrideController = new AnimatorOverrideController(baseController)
        {
            name = baseController.name + "_WeaponPose"
        };
        thirdPersonAnimator.runtimeAnimatorController = overrideController;
        for (int i = 0; i < lastApplied.Length; i++)
            lastApplied[i] = null;
        DiscoverSlotsIfNeeded();
    }

    private void DiscoverSlotsIfNeeded()
    {
        if (overrideController == null)
            return;

        AnimationClipPair[] pairs = overrideController.clips;
        for (int i = 0; i < pairs.Length; i++)
        {
            AnimationClip original = pairs[i].originalClip;
            if (original == null)
                continue;

            if (IsSlot(original, "TP_WeaponPose_Hold", "WeaponPose_Hold", "LongGunReady", "LongGun_Hold"))
                holdSlot = original;
            else if (IsSlot(original, "TP_WeaponPose_Sprint", "WeaponPose_Sprint", "LongGunSprint", "LongGun_Sprint"))
                sprintSlot = original;
            else if (IsSlot(original, "TP_WeaponPose_Prone", "WeaponPose_Prone", "LongGunProne", "LongGun_Prone"))
                proneSlot = original;
            else if (IsSlot(original, "TP_WeaponPose_Aim", "WeaponPose_Aim", "LongGun_Aim"))
                aimSlot = original;
            else if (IsSlot(original, "TP_WeaponPose_Crouch", "WeaponPose_Crouch"))
                crouchSlot = original;
        }
    }

    private static bool IsSlot(AnimationClip clip, params string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            if (clip.name == names[i])
                return true;
        }

        return false;
    }

    private void ApplyResolvedClips(WeaponDefinition definition)
    {
        if (overrideController == null)
            return;

        ReplaceSlot(holdSlot, ThirdPersonWeaponPoseResolver.ResolveClip(definition, ThirdPersonWeaponPoseKind.Hold), 0);
        ReplaceSlot(sprintSlot, ThirdPersonWeaponPoseResolver.ResolveClip(definition, ThirdPersonWeaponPoseKind.Sprint), 1);
        ReplaceSlot(proneSlot, ThirdPersonWeaponPoseResolver.ResolveClip(definition, ThirdPersonWeaponPoseKind.Prone), 2);
        ReplaceSlot(aimSlot, ThirdPersonWeaponPoseResolver.ResolveClip(definition, ThirdPersonWeaponPoseKind.Aim), 3);
        ReplaceSlot(crouchSlot, ThirdPersonWeaponPoseResolver.ResolveClip(definition, ThirdPersonWeaponPoseKind.Crouch), 4);
    }

    private void ReplaceSlot(AnimationClip slot, AnimationClip resolved, int appliedIndex)
    {
        if (slot == null)
            return;

        AnimationClip next = resolved != null ? resolved : slot;
        if (lastApplied[appliedIndex] == next)
            return;

        overrideController[slot] = next;
        lastApplied[appliedIndex] = next;
    }

    private int ResolveLayerIndex()
    {
        if (thirdPersonAnimator == null)
            return -1;
        if (cachedLayer >= 0 && cachedLayer < thirdPersonAnimator.layerCount)
        {
            string name = thirdPersonAnimator.GetLayerName(cachedLayer);
            if (name == LayerName || name == LegacyLayerName)
                return cachedLayer;
        }

        cachedLayer = thirdPersonAnimator.GetLayerIndex(LayerName);
        if (cachedLayer < 0)
            cachedLayer = thirdPersonAnimator.GetLayerIndex(LegacyLayerName);
        return cachedLayer;
    }

    private bool HasParameter(int hash)
    {
        if (thirdPersonAnimator == null)
            return false;

        AnimatorControllerParameter[] parameters = thirdPersonAnimator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].nameHash == hash)
                return true;
        }

        return false;
    }
}
