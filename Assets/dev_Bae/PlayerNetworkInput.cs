using Fusion;
using UnityEngine;

public struct PlayerNetworkInput : INetworkInput
{
    public Vector2 moveInput;
    public Vector2 lookDelta;
    public NetworkBool isSprinting;
    public NetworkBool isJumping;
    public NetworkBool isGrab;
    public NetworkBool isThrow;
}