using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace Junk.Contacts
{
    /// <summary>
    /// Determines the resulting decal from a collision.
    /// </summary>
    /// todo rename ContactImpactType
    public enum ImpactType
    {
        Normal, // handgun caliber
        Large // large caliber ie revolver
    }
    
    public struct BulletDecalEvent : IComponentData 
    {
        public Entity     Prefab;
        public RaycastHit Hit;
        public ImpactType Type;
        public Random     Random;
    }
    
    public struct BulletDecalData : IComponentData
    {
        public int Value;
        public int PositionHash;
    }
}