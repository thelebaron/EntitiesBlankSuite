using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Junk.Entities
{
    public struct SharedEffectsPrefabs : IComponentData
    {
        public Entity DebrisPebble;
        public Entity DecalBloodSplatter;
        public Entity BloodSpraySpawnerLarge;
        public Entity BloodSplatGroundLarge;
        public Entity Gibble01Prefab;
    }
    
    public class SharedManagedPrefabs : IComponentData
    {
        public GameObject BulletSmokePrefab;
        public GameObject SparkPrefab;
        public GameObject BloodSpray02Prefab;
    }



}