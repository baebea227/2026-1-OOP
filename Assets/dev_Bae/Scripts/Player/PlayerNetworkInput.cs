using Fusion;
using UnityEngine;

public struct PlayerNetworkInput : INetworkInput
{
    public Vector2 moveInput;
    public float yaw;
    public NetworkBool isSprinting;
    public NetworkBool isJumping;
    public NetworkBool isGrab;
    public NetworkBool isThrow;
}