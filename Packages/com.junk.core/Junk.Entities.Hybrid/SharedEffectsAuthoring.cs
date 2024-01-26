using Unity.Entities;
using UnityEngine;

namespace Junk.Entities
{
    public class SharedEffectsAuthoring : MonoBehaviour
    {
        public GameObject DebrisPebble;
        public GameObject BloodSpraySpawnerLarge;
        
        [Header("Blood Decals")]
        public GameObject DecalBloodSplatter;
        public GameObject BloodSplatGroundLarge;
        
        [Header("Blood Gibs")]
        public GameObject Gibble01;
        
        [Header("Managed Prefabs")]
        public GameObject BulletSmokePrefab;
        public GameObject SparkPrefab;
        public GameObject BloodSpray02Prefab;
    }

    public class EntityResourcesBaker : Baker<SharedEffectsAuthoring>
    {
        public override void Bake(SharedEffectsAuthoring authoring)
        {
            var singletonEntity = GetEntity(TransformUsageFlags.None);
            
            var data = new SharedEffectsPrefabs
            {
                DebrisPebble           = GetEntity(authoring.DebrisPebble, TransformUsageFlags.Dynamic),
                DecalBloodSplatter     = GetEntity(authoring.DecalBloodSplatter, TransformUsageFlags.Renderable),
                BloodSpraySpawnerLarge = GetEntity(authoring.BloodSpraySpawnerLarge, TransformUsageFlags.Dynamic),
                //BulletSmokePrefab      = GetEntity(authoring.BulletSmokePrefab, TransformUsageFlags.Dynamic),
                BloodSplatGroundLarge  = GetEntity(authoring.BloodSplatGroundLarge, TransformUsageFlags.Renderable),
                Gibble01Prefab         = GetEntity(authoring.Gibble01, TransformUsageFlags.Dynamic),
            };
            
            var managedData = new SharedManagedPrefabs
            {
                BulletSmokePrefab = authoring.BulletSmokePrefab,
                SparkPrefab       = authoring.SparkPrefab,
                BloodSpray02Prefab = authoring.BloodSpray02Prefab
            };

            AddComponent(singletonEntity, data);
            AddComponentObject(singletonEntity, managedData);
        }
    }
}