using Fusion;
using UnityEngine;

public class NetworkPlayerMovement : NetworkBehaviour
{
    public float speed = 5f;

    public override void FixedUpdateNetwork()
    {
        if (GetInput<PlayerInputData>(out var input))
        {
            Vector3 move = new Vector3(
                input.movement.x,
                0,
                input.movement.y
            );

            transform.position += move * speed * Runner.DeltaTime;
        }
    }
}
