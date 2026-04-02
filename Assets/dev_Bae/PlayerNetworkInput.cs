using Fusion;
using UnityEngine;

public struct PlayerNetworkInput : INetworkInput
{
    public Vector2 moveInput;
    public NetworkBool isSprinting;
    public NetworkBool isJumping;
}