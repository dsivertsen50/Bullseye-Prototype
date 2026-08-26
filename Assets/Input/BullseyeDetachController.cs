using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Extends the existing bullseye with Attached / Detached / Returning states.
/// The same bullseye object stays linked to its owning player.
/// </summary>
public class BullseyeDetachController : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Transform bullseye;
    [SerializeField] private BullseyeTarget bullseyeTarget;
    [SerializeField] private CapsuleCollider bodyCapsule;

    [Header("Return")]
    [SerializeField] private float detachedReturnDelay = 6f;
    [SerializeField] private float bullseyeReturnSpeed = 28f;
    [SerializeField] private float lostHeight = -20f;
    [SerializeField] private float maxDetachDistance = 40f;

    [Header("Detached Physics")]
    [SerializeField] private float bullseyeMass = 4.5f;
    [SerializeField] private float bullseyeDrag = 1.1f;
    [SerializeField] private float bullseyeAngularDrag = 1.4f;
    [SerializeField] private PhysicsMaterial bullseyePhysicsMaterial;
    [SerializeField] private MeshCollider discCollider;

    [Header("Feedback")]
    [SerializeField] private AudioClip bullseyeDetachSfx;
    [SerializeField] private AudioClip bullseyeReturnSfx;
    [SerializeField] private AudioClip[] bullseyeCollisionSfx;
    [SerializeField] private float collisionSfxVolume = 0.7f;
    [SerializeField] private float minCollisionSpeed = 1f;
    [SerializeField] private float maxCollisionSpeed = 8f;
    [SerializeField] private float collisionSfxCooldown = 0.14f;
    [SerializeField] private float feedbackVolume = 0.8f;
    [SerializeField] private float ownerMessageDuration = 2.4f;

    private readonly NetworkVariable<byte> attachState = new(
        (byte)BullseyeAttachState.Attached,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<Vector3> detachedPosition = new(
        Vector3.zero,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<Vector3> detachedEuler = new(
        Vector3.zero,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<double> returnAtServerTime = new(
        0d,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private PlayerHealth playerHealth;
    private Transform attachedParent;
    private Vector3 attachedScale;
    private Rigidbody physicsBody;
    private MeshCollider physicsCollider;
    private PhysicsCollisionRelay collisionRelay;
    private Coroutine returnRoutine;
    private float nextCollisionSfxTime;
    private Vector3 clientVisualPosition;
    private Quaternion clientVisualRotation;
    private bool hasClientVisual;
    private float ownerMessageUntil;
    private GUIStyle ownerMessageStyle;
    private GUIStyle ownerMessageCaptionStyle;

    public BullseyeAttachState State => (BullseyeAttachState)attachState.Value;
    public bool IsAttached => State == BullseyeAttachState.Attached;
    public bool IsDetached => State == BullseyeAttachState.Detached;
    public bool IsSurfaceDriven => IsAttached;
    public Transform BullseyeTransform => bullseye;
    public PlayerHealth OwnerHealth => playerHealth;

    private void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();

        if (bodyCapsule == null)
            bodyCapsule = GetComponentInChildren<CapsuleCollider>();

        if (bullseyeTarget == null)
            bullseyeTarget = GetComponentInChildren<BullseyeTarget>(true);

        if (bullseye == null && bullseyeTarget != null)
            bullseye = bullseyeTarget.transform;

        if (bullseye != null)
        {
            attachedParent = bullseye.parent;
            attachedScale = bullseye.localScale;
            if (discCollider == null)
                discCollider = bullseye.GetComponent<MeshCollider>();
        }
    }

    public override void OnNetworkSpawn()
    {
        attachState.OnValueChanged += OnAttachStateChanged;
        ApplyVisualState(State, playFeedback: false);
        hasClientVisual = false;
    }

    public override void OnNetworkDespawn()
    {
        attachState.OnValueChanged -= OnAttachStateChanged;
        UnbindCollisionRelay();
        StopReturnRoutine();
        ForceLocalAttach(disablePhysicsOnly: false);
    }

    public override void OnDestroy()
    {
        UnbindCollisionRelay();
        if (bullseye != null && attachedParent != null && bullseye.parent != attachedParent)
            ForceLocalAttach(disablePhysicsOnly: false);

        base.OnDestroy();
    }

    private void Update()
    {
        if (!IsSpawned || bullseye == null)
            return;

        if (IsServer)
            TickServerState();

        if (!IsAttached)
            ApplyNetworkedPose();
    }

    private void FixedUpdate()
    {
        if (!IsServer || !IsSpawned || !IsDetached || physicsBody == null)
            return;

        WriteDetachedPose(physicsBody.position, physicsBody.rotation);
    }

    public void NotifyExplosion(
        Vector3 explosionPosition,
        float bullseyeForce,
        float knockbackRadius,
        float detachRadius,
        float upwardModifier)
    {
        if (!IsServer || !IsSpawned || bullseye == null)
            return;

        if (playerHealth != null && playerHealth.IsDead)
            return;

        float distance = Vector3.Distance(bullseye.position, explosionPosition);

        if (State == BullseyeAttachState.Returning)
            return;

        if (State == BullseyeAttachState.Detached)
        {
            if (distance <= Mathf.Max(knockbackRadius, detachRadius))
                ApplyDetachedExplosionForce(explosionPosition, bullseyeForce, knockbackRadius, upwardModifier);
            return;
        }

        if (distance <= detachRadius)
            BeginDetach(explosionPosition, bullseyeForce, detachRadius, upwardModifier);
    }

    public void HandleOwnerDied()
    {
        if (!IsServer)
            return;

        StopReturnRoutine();
        returnAtServerTime.Value = 0d;

        if (bullseye != null && !IsAttached)
            WriteDetachedPose(bullseye.position, bullseye.rotation);

        ConfigurePhysics(active: false, colliding: false);
    }

    public void HandleOwnerRespawned()
    {
        if (!IsServer)
            return;

        StopReturnRoutine();
        returnAtServerTime.Value = 0d;
        SetState(BullseyeAttachState.Attached);
        if (bullseye != null && !bullseye.gameObject.activeSelf)
            bullseye.gameObject.SetActive(true);
    }

    private void BeginDetach(Vector3 explosionPosition, float force, float radius, float upwardModifier)
    {
        EnsurePhysics();
        WriteDetachedPose(bullseye.position, bullseye.rotation);
        returnAtServerTime.Value = NetworkManager.ServerTime.Time + Mathf.Max(0.1f, detachedReturnDelay);
        SetState(BullseyeAttachState.Detached);
        ApplyDetachedExplosionForce(explosionPosition, force, radius, upwardModifier);
    }

    private void ApplyDetachedExplosionForce(Vector3 explosionPosition, float force, float radius, float upwardModifier)
    {
        if (physicsBody == null || physicsBody.isKinematic)
            return;

        physicsBody.AddExplosionForce(
            Mathf.Max(0f, force),
            explosionPosition,
            Mathf.Max(0.1f, radius),
            Mathf.Max(0f, upwardModifier),
            ForceMode.Impulse);
    }

    private void TickServerState()
    {
        if (State != BullseyeAttachState.Detached)
            return;

        if (playerHealth != null && playerHealth.IsDead)
        {
            HandleOwnerDied();
            return;
        }

        if (IsLost())
        {
            StartReturn();
            return;
        }

        if (NetworkManager.ServerTime.Time >= returnAtServerTime.Value)
            StartReturn();
    }

    private bool IsLost()
    {
        if (bullseye == null)
            return true;

        if (bullseye.position.y < lostHeight)
            return true;

        float limit = Mathf.Max(1f, maxDetachDistance);
        return Vector3.Distance(bullseye.position, transform.position) > limit;
    }

    private void StartReturn()
    {
        if (State != BullseyeAttachState.Detached)
            return;

        if (playerHealth != null && playerHealth.IsDead)
        {
            HandleOwnerDied();
            return;
        }

        WriteDetachedPose(bullseye.position, bullseye.rotation);
        SetState(BullseyeAttachState.Returning);
        StopReturnRoutine();
        returnRoutine = StartCoroutine(ReturnToOwner());
    }

    private IEnumerator ReturnToOwner()
    {
        Vector3 start = bullseye != null ? bullseye.position : detachedPosition.Value;
        Quaternion startRotation = bullseye != null ? bullseye.rotation : Quaternion.Euler(detachedEuler.Value);
        float speed = Mathf.Max(4f, bullseyeReturnSpeed);

        while (IsSpawned && State == BullseyeAttachState.Returning)
        {
            if (playerHealth != null && playerHealth.IsDead)
            {
                HandleOwnerDied();
                yield break;
            }

            Vector3 target = GetReattachPosition();
            float distance = Vector3.Distance(start, target);
            float duration = Mathf.Clamp(distance / speed, 0.25f, 0.75f);
            float elapsed = 0f;
            Vector3 current = start;

            while (elapsed < duration && IsSpawned && State == BullseyeAttachState.Returning)
            {
                if (playerHealth != null && playerHealth.IsDead)
                {
                    HandleOwnerDied();
                    yield break;
                }

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                t = t * t * (3f - 2f * t);
                target = GetReattachPosition();
                current = Vector3.Lerp(start, target, t);
                Quaternion rotation = Quaternion.Slerp(startRotation, GetReattachRotation(), t);
                WriteDetachedPose(current, rotation);
                if (bullseye != null)
                {
                    bullseye.position = current;
                    bullseye.rotation = rotation;
                }

                yield return null;
            }

            break;
        }

        returnRoutine = null;
        if (IsSpawned && State == BullseyeAttachState.Returning)
            SetState(BullseyeAttachState.Attached);
    }

    private Vector3 GetReattachPosition()
    {
        if (bodyCapsule == null)
            return transform.position + Vector3.up * 1.2f;

        CapsuleBodySurface.Evaluate(
            bodyCapsule,
            0f,
            0.62f,
            out Vector3 localPosition,
            out Vector3 localNormal);

        return bodyCapsule.transform.TransformPoint(localPosition + localNormal * 0.08f);
    }

    private Quaternion GetReattachRotation()
    {
        if (bodyCapsule == null)
            return transform.rotation;

        CapsuleBodySurface.Evaluate(
            bodyCapsule,
            0f,
            0.62f,
            out _,
            out Vector3 localNormal);

        Vector3 worldNormal = bodyCapsule.transform.TransformDirection(localNormal).normalized;
        Vector3 upHint = Mathf.Abs(Vector3.Dot(worldNormal, transform.up)) > 0.95f
            ? transform.forward
            : transform.up;
        return Quaternion.LookRotation(worldNormal, upHint);
    }

    private void OnAttachStateChanged(byte previous, byte next)
    {
        ApplyVisualState((BullseyeAttachState)next, playFeedback: previous != next);
    }

    private void ApplyVisualState(BullseyeAttachState state, bool playFeedback)
    {
        if (bullseye == null)
            return;

        switch (state)
        {
            case BullseyeAttachState.Detached:
                EnterDetached(playFeedback);
                break;
            case BullseyeAttachState.Returning:
                EnterReturning(playFeedback);
                break;
            default:
                EnterAttached();
                break;
        }
    }

    private void EnterDetached(bool playFeedback)
    {
        UnparentBullseye();
        EnsurePhysics();
        ConfigurePhysics(active: true, colliding: true);

        if (IsServer && physicsBody != null)
        {
            physicsBody.position = detachedPosition.Value.sqrMagnitude > 0f
                ? detachedPosition.Value
                : bullseye.position;
            physicsBody.rotation = Quaternion.Euler(detachedEuler.Value);
        }

        hasClientVisual = false;
        if (playFeedback)
        {
            PlayClip(bullseyeDetachSfx);
            if (IsOwner)
                ownerMessageUntil = Time.unscaledTime + ownerMessageDuration;
        }
    }

    private void EnterReturning(bool playFeedback)
    {
        UnparentBullseye();
        EnsurePhysics();
        ConfigurePhysics(active: false, colliding: false);
        hasClientVisual = false;
        if (playFeedback)
            PlayClip(bullseyeReturnSfx);
    }

    private void EnterAttached()
    {
        StopReturnRoutine();
        ConfigurePhysics(active: false, colliding: false);
        ForceLocalAttach(disablePhysicsOnly: true);
        hasClientVisual = false;
        ownerMessageUntil = 0f;
    }

    private void ForceLocalAttach(bool disablePhysicsOnly)
    {
        if (bullseye == null)
            return;

        ConfigurePhysics(active: false, colliding: false);

        if (attachedParent != null && bullseye.parent != attachedParent)
            bullseye.SetParent(attachedParent, true);

        if (attachedScale.sqrMagnitude > 0f)
            bullseye.localScale = attachedScale;

        if (!disablePhysicsOnly)
            return;
    }

    private void UnparentBullseye()
    {
        if (bullseye == null)
            return;

        if (attachedParent == null)
            attachedParent = transform;

        if (bullseye.parent != null)
        {
            attachedScale = bullseye.localScale;
            bullseye.SetParent(null, true);
        }
    }

    private void ApplyNetworkedPose()
    {
        if (bullseye == null)
            return;

        Vector3 targetPosition = detachedPosition.Value;
        Quaternion targetRotation = Quaternion.Euler(detachedEuler.Value);

        if (IsServer && IsDetached && physicsBody != null && !physicsBody.isKinematic)
            return;

        if (!hasClientVisual)
        {
            clientVisualPosition = targetPosition;
            clientVisualRotation = targetRotation;
            hasClientVisual = true;
        }
        else
        {
            float follow = 1f - Mathf.Exp(-18f * Time.deltaTime);
            clientVisualPosition = Vector3.Lerp(clientVisualPosition, targetPosition, follow);
            clientVisualRotation = Quaternion.Slerp(clientVisualRotation, targetRotation, follow);
        }

        bullseye.SetPositionAndRotation(clientVisualPosition, clientVisualRotation);
    }

    private void WriteDetachedPose(Vector3 position, Quaternion rotation)
    {
        detachedPosition.Value = position;
        detachedEuler.Value = rotation.eulerAngles;
    }

    private void EnsurePhysics()
    {
        if (bullseye == null)
            return;

        if (physicsBody == null)
        {
            physicsBody = bullseye.GetComponent<Rigidbody>();
            if (physicsBody == null)
                physicsBody = bullseye.gameObject.AddComponent<Rigidbody>();
        }

        physicsBody.mass = Mathf.Max(0.1f, bullseyeMass);
        physicsBody.linearDamping = Mathf.Max(0f, bullseyeDrag);
        physicsBody.angularDamping = Mathf.Max(0f, bullseyeAngularDrag);
        physicsBody.interpolation = RigidbodyInterpolation.Interpolate;
        physicsBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        physicsBody.constraints = RigidbodyConstraints.None;
        physicsBody.freezeRotation = false;

        RemoveLegacyBallCollider();
        physicsCollider = discCollider != null ? discCollider : bullseye.GetComponent<MeshCollider>();
        if (physicsCollider == null)
            physicsCollider = bullseye.gameObject.AddComponent<MeshCollider>();

        discCollider = physicsCollider;
        if (physicsCollider.sharedMesh == null)
        {
            MeshFilter filter = bullseye.GetComponent<MeshFilter>();
            if (filter != null)
                physicsCollider.sharedMesh = filter.sharedMesh;
        }

        physicsCollider.convex = true;
        physicsCollider.material = bullseyePhysicsMaterial;
        BindCollisionRelay();
        IgnoreOwnerCollisions();
    }

    private void RemoveLegacyBallCollider()
    {
        if (bullseye == null)
            return;

        Transform existing = bullseye.Find("DetachCollider");
        if (existing == null)
            return;

        if (Application.isPlaying)
            Destroy(existing.gameObject);
        else
            DestroyImmediate(existing.gameObject);
    }

    private void BindCollisionRelay()
    {
        if (physicsCollider == null)
            return;

        if (collisionRelay == null)
            collisionRelay = physicsCollider.GetComponent<PhysicsCollisionRelay>();
        if (collisionRelay == null)
            collisionRelay = physicsCollider.gameObject.AddComponent<PhysicsCollisionRelay>();

        collisionRelay.CollisionEntered -= OnDetachedCollision;
        collisionRelay.CollisionEntered += OnDetachedCollision;
    }

    private void UnbindCollisionRelay()
    {
        if (collisionRelay == null)
            return;

        collisionRelay.CollisionEntered -= OnDetachedCollision;
    }

    private void OnDetachedCollision(Collision collision)
    {
        if (!IsServer || !IsDetached || collision == null)
            return;

        if (!TryGetImpactVolume(collision.relativeVelocity.magnitude, out float volume))
            return;

        Vector3 point = collision.contactCount > 0
            ? collision.GetContact(0).point
            : bullseye != null ? bullseye.position : transform.position;
        PlayCollisionSfxRpc(point, volume);
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void PlayCollisionSfxRpc(Vector3 point, float volume)
    {
        AudioClip clip = PickRandom(bullseyeCollisionSfx);
        if (clip == null)
            return;

        AudioSource.PlayClipAtPoint(clip, point, volume);
    }

    private bool TryGetImpactVolume(float speed, out float volume)
    {
        volume = 0f;
        if (speed < minCollisionSpeed || Time.time < nextCollisionSfxTime)
            return false;

        nextCollisionSfxTime = Time.time + Mathf.Max(0.02f, collisionSfxCooldown);
        float t = Mathf.InverseLerp(minCollisionSpeed, maxCollisionSpeed, speed);
        volume = Mathf.Lerp(collisionSfxVolume * 0.35f, collisionSfxVolume, t);
        return volume > 0.01f;
    }

    private void ConfigurePhysics(bool active, bool colliding)
    {
        if (physicsBody == null && !active)
            return;

        EnsurePhysics();
        if (physicsBody == null)
            return;

        bool simulate = active && IsServer && colliding;

        if (!simulate && !physicsBody.isKinematic)
        {
            physicsBody.linearVelocity = Vector3.zero;
            physicsBody.angularVelocity = Vector3.zero;
        }

        physicsBody.isKinematic = !simulate;
        physicsBody.useGravity = simulate;
        physicsBody.detectCollisions = true;

        if (physicsCollider != null)
        {
            physicsCollider.enabled = true;
            physicsCollider.convex = true;
            physicsCollider.isTrigger = !simulate;
        }

        IgnoreOwnerCollisions();
    }

    private void IgnoreOwnerCollisions()
    {
        if (physicsCollider == null)
            return;

        Collider[] ownerColliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < ownerColliders.Length; i++)
        {
            Collider ownerCollider = ownerColliders[i];
            if (ownerCollider == null || ownerCollider == physicsCollider)
                continue;
            if (ownerCollider.transform == bullseye || ownerCollider.transform.IsChildOf(bullseye))
                continue;

            Physics.IgnoreCollision(physicsCollider, ownerCollider, true);
        }
    }

    private void SetState(BullseyeAttachState state)
    {
        byte value = (byte)state;
        if (attachState.Value == value)
        {
            ApplyVisualState(state, playFeedback: false);
            return;
        }

        attachState.Value = value;
    }

    private void StopReturnRoutine()
    {
        if (returnRoutine == null)
            return;

        StopCoroutine(returnRoutine);
        returnRoutine = null;
    }

    private void PlayClip(AudioClip clip)
    {
        if (clip == null || bullseye == null)
            return;

        AudioSource.PlayClipAtPoint(clip, bullseye.position, Mathf.Clamp01(feedbackVolume));
    }

    private void OnGUI()
    {
        if (!IsOwner || !IsSpawned)
            return;

        if (playerHealth != null && playerHealth.IsDead)
            return;

        if (LocalPlayerMenuState.IsOpen(this))
            return;

        if (Time.unscaledTime > ownerMessageUntil && !IsDetached && State != BullseyeAttachState.Returning)
            return;

        if (Time.unscaledTime > ownerMessageUntil && IsAttached)
            return;

        float centerX = Screen.width * 0.5f;
        var captionRect = new Rect(centerX - 280f, 36f, 560f, 28f);
        var messageRect = new Rect(centerX - 280f, 62f, 560f, 36f);
        DrawShadowedLabel(captionRect, "BULLSEYE KNOCKED OFF", GetCaptionStyle());
        DrawShadowedLabel(messageRect, "You are temporarily vulnerable", GetMessageStyle());
    }

    private static void DrawShadowedLabel(Rect rect, string text, GUIStyle style)
    {
        Color previous = style.normal.textColor;
        style.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
        GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, style);
        style.normal.textColor = previous;
        GUI.Label(rect, text, style);
    }

    private GUIStyle GetCaptionStyle()
    {
        if (ownerMessageCaptionStyle != null)
            return ownerMessageCaptionStyle;

        ownerMessageCaptionStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.UpperCenter,
            fontSize = 18,
            fontStyle = FontStyle.Bold
        };
        ownerMessageCaptionStyle.normal.textColor = new Color(1f, 0.82f, 0.35f, 1f);
        return ownerMessageCaptionStyle;
    }

    private GUIStyle GetMessageStyle()
    {
        if (ownerMessageStyle != null)
            return ownerMessageStyle;

        ownerMessageStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.UpperCenter,
            fontSize = 16
        };
        ownerMessageStyle.normal.textColor = new Color(1f, 1f, 1f, 0.95f);
        return ownerMessageStyle;
    }

    private void OnValidate()
    {
        detachedReturnDelay = Mathf.Max(0.1f, detachedReturnDelay);
        bullseyeReturnSpeed = Mathf.Max(1f, bullseyeReturnSpeed);
        lostHeight = Mathf.Min(-1f, lostHeight);
        maxDetachDistance = Mathf.Max(5f, maxDetachDistance);
        bullseyeMass = Mathf.Max(0.1f, bullseyeMass);
        bullseyeDrag = Mathf.Max(0f, bullseyeDrag);
        bullseyeAngularDrag = Mathf.Max(0f, bullseyeAngularDrag);
        collisionSfxVolume = Mathf.Clamp01(collisionSfxVolume);
        minCollisionSpeed = Mathf.Max(0.05f, minCollisionSpeed);
        maxCollisionSpeed = Mathf.Max(minCollisionSpeed, maxCollisionSpeed);
        collisionSfxCooldown = Mathf.Max(0.02f, collisionSfxCooldown);
        feedbackVolume = Mathf.Clamp01(feedbackVolume);
        ownerMessageDuration = Mathf.Max(0.25f, ownerMessageDuration);
    }

    private static AudioClip PickRandom(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0)
            return null;

        int assigned = 0;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null)
                assigned++;
        }

        if (assigned <= 0)
            return null;

        int pick = Random.Range(0, assigned);
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] == null)
                continue;

            if (pick == 0)
                return clips[i];

            pick--;
        }

        return null;
    }
}
