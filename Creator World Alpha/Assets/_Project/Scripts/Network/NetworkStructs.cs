using Unity.Netcode;
using UnityEngine;

namespace CreatorWorld.Network
{
    /// <summary>
    /// Player animation state packed for network sync.
    /// Uses bit flags for booleans to minimize bandwidth.
    /// </summary>
    public struct PlayerAnimState : INetworkSerializable
    {
        public float speed;
        public float moveX;
        public float moveZ;
        public float velocityY;
        public byte flags; // Packed booleans

        // Flag bit positions
        private const byte FLAG_GROUNDED = 1 << 0;
        private const byte FLAG_CROUCHING = 1 << 1;
        private const byte FLAG_SPRINTING = 1 << 2;
        private const byte FLAG_JUMPING = 1 << 3;
        private const byte FLAG_FALLING = 1 << 4;
        private const byte FLAG_SLIDING = 1 << 5;
        private const byte FLAG_VAULTING = 1 << 6;
        private const byte FLAG_AIMING = 1 << 7;

        // Property accessors for flags
        public bool IsGrounded
        {
            get => (flags & FLAG_GROUNDED) != 0;
            set => flags = value ? (byte)(flags | FLAG_GROUNDED) : (byte)(flags & ~FLAG_GROUNDED);
        }

        public bool IsCrouching
        {
            get => (flags & FLAG_CROUCHING) != 0;
            set => flags = value ? (byte)(flags | FLAG_CROUCHING) : (byte)(flags & ~FLAG_CROUCHING);
        }

        public bool IsSprinting
        {
            get => (flags & FLAG_SPRINTING) != 0;
            set => flags = value ? (byte)(flags | FLAG_SPRINTING) : (byte)(flags & ~FLAG_SPRINTING);
        }

        public bool IsJumping
        {
            get => (flags & FLAG_JUMPING) != 0;
            set => flags = value ? (byte)(flags | FLAG_JUMPING) : (byte)(flags & ~FLAG_JUMPING);
        }

        public bool IsFalling
        {
            get => (flags & FLAG_FALLING) != 0;
            set => flags = value ? (byte)(flags | FLAG_FALLING) : (byte)(flags & ~FLAG_FALLING);
        }

        public bool IsSliding
        {
            get => (flags & FLAG_SLIDING) != 0;
            set => flags = value ? (byte)(flags | FLAG_SLIDING) : (byte)(flags & ~FLAG_SLIDING);
        }

        public bool IsVaulting
        {
            get => (flags & FLAG_VAULTING) != 0;
            set => flags = value ? (byte)(flags | FLAG_VAULTING) : (byte)(flags & ~FLAG_VAULTING);
        }

        public bool IsAiming
        {
            get => (flags & FLAG_AIMING) != 0;
            set => flags = value ? (byte)(flags | FLAG_AIMING) : (byte)(flags & ~FLAG_AIMING);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref speed);
            serializer.SerializeValue(ref moveX);
            serializer.SerializeValue(ref moveZ);
            serializer.SerializeValue(ref velocityY);
            serializer.SerializeValue(ref flags);
        }
    }

    /// <summary>
    /// Player input payload sent to server each tick.
    /// Contains all input state for server-side movement simulation.
    /// </summary>
    public struct PlayerInputPayload : INetworkSerializable
    {
        public Vector2 moveInput;
        public Vector2 lookInput;
        public byte inputFlags; // Packed input booleans
        public uint tick; // For client-side prediction reconciliation

        // Flag bit positions
        private const byte INPUT_JUMP = 1 << 0;
        private const byte INPUT_CROUCH = 1 << 1;
        private const byte INPUT_SPRINT = 1 << 2;
        private const byte INPUT_FIRE = 1 << 3;
        private const byte INPUT_RELOAD = 1 << 4;
        private const byte INPUT_INTERACT = 1 << 5;
        private const byte INPUT_AIM = 1 << 6;

        public bool Jump
        {
            get => (inputFlags & INPUT_JUMP) != 0;
            set => inputFlags = value ? (byte)(inputFlags | INPUT_JUMP) : (byte)(inputFlags & ~INPUT_JUMP);
        }

        public bool Crouch
        {
            get => (inputFlags & INPUT_CROUCH) != 0;
            set => inputFlags = value ? (byte)(inputFlags | INPUT_CROUCH) : (byte)(inputFlags & ~INPUT_CROUCH);
        }

        public bool Sprint
        {
            get => (inputFlags & INPUT_SPRINT) != 0;
            set => inputFlags = value ? (byte)(inputFlags | INPUT_SPRINT) : (byte)(inputFlags & ~INPUT_SPRINT);
        }

        public bool Fire
        {
            get => (inputFlags & INPUT_FIRE) != 0;
            set => inputFlags = value ? (byte)(inputFlags | INPUT_FIRE) : (byte)(inputFlags & ~INPUT_FIRE);
        }

        public bool Reload
        {
            get => (inputFlags & INPUT_RELOAD) != 0;
            set => inputFlags = value ? (byte)(inputFlags | INPUT_RELOAD) : (byte)(inputFlags & ~INPUT_RELOAD);
        }

        public bool Interact
        {
            get => (inputFlags & INPUT_INTERACT) != 0;
            set => inputFlags = value ? (byte)(inputFlags | INPUT_INTERACT) : (byte)(inputFlags & ~INPUT_INTERACT);
        }

        public bool Aim
        {
            get => (inputFlags & INPUT_AIM) != 0;
            set => inputFlags = value ? (byte)(inputFlags | INPUT_AIM) : (byte)(inputFlags & ~INPUT_AIM);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref moveInput);
            serializer.SerializeValue(ref lookInput);
            serializer.SerializeValue(ref inputFlags);
            serializer.SerializeValue(ref tick);
        }
    }

    /// <summary>
    /// Server state snapshot for client reconciliation.
    /// </summary>
    public struct PlayerStateSnapshot : INetworkSerializable
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 velocity;
        public uint tick;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref position);
            serializer.SerializeValue(ref rotation);
            serializer.SerializeValue(ref velocity);
            serializer.SerializeValue(ref tick);
        }
    }

    /// <summary>
    /// Weapon fire request from client to server.
    /// </summary>
    public struct FireRequest : INetworkSerializable
    {
        public Vector3 origin;
        public Vector3 direction;
        public float spread;
        public uint tick;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref origin);
            serializer.SerializeValue(ref direction);
            serializer.SerializeValue(ref spread);
            serializer.SerializeValue(ref tick);
        }
    }

    /// <summary>
    /// Hit result broadcast from server to clients.
    /// </summary>
    public struct HitResult : INetworkSerializable
    {
        public ulong shooterId;
        public ulong targetId; // 0 if hit environment
        public Vector3 hitPoint;
        public byte hitType; // 0=miss, 1=body, 2=head, 3=limb, 4=environment

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref shooterId);
            serializer.SerializeValue(ref targetId);
            serializer.SerializeValue(ref hitPoint);
            serializer.SerializeValue(ref hitType);
        }
    }
}
