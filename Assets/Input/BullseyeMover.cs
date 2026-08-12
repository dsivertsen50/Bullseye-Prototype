using UnityEngine;
using Unity.Netcode;

public class BullseyeMover : NetworkBehaviour
{
    [SerializeField] private Transform bullseye;

    private readonly Vector3[] positions =
    {
        new Vector3(0f, 1.1f, 0.5f),      // torso
        new Vector3(0f, 1.55f, 0.5f),     // chest
        new Vector3(0.35f, 1.45f, 0.45f), // shoulder
        new Vector3(-0.35f, 1.45f, 0.45f),
        new Vector3(0f, 1.85f, 0.45f)     // head
    };

    private NetworkVariable<int> positionIndex = new(0);

    private float timer;

    public override void OnNetworkSpawn()
    {
        positionIndex.OnValueChanged += OnPositionChanged;
        MoveBullseye(positionIndex.Value);

        if (IsServer)
            ResetTimer();
    }

    private void Update()
    {
        if (!IsServer)
            return;

        timer -= Time.deltaTime;

        if (timer <= 0f)
        {
            int newIndex;

            do
            {
                newIndex = Random.Range(0, positions.Length);
            }
            while (newIndex == positionIndex.Value);

            positionIndex.Value = newIndex;
            ResetTimer();
        }
    }

    private void OnPositionChanged(int oldIndex, int newIndex)
    {
        MoveBullseye(newIndex);
    }

    private void MoveBullseye(int index)
    {
        bullseye.localPosition = positions[index];
    }

    private void ResetTimer()
    {
        timer = Random.Range(2f, 4f);
    }
}