using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Junk.Entities
{
    public class KeyboardSpawnerAuthoring : MonoBehaviour
    {
        public GameObject Prefab;
        
    }
    
    public class KeyboardSpawnerBaker : Baker<KeyboardSpawnerAuthoring>
    {
        public override void Bake(KeyboardSpawnerAuthoring authoring)
        {
            var entity = GetEntity(authoring.gameObject, TransformUsageFlags.Dynamic);
            AddComponent(entity, new SimpleSpawner
            {
                Prefab = GetEntity(authoring.Prefab, TransformUsageFlags.Dynamic)
            });
        }
    }

    public struct SimpleSpawner : IComponentData
    {
        public Entity Prefab;
    }

    //[DisableAutoCreation]
    [UpdateInGroup(typeof(EndSimulationStructuralChangeSystemGroup))]
    public partial class KeyboardSpawner : SystemBase
    {
        private float timer;

        protected override void OnUpdate()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;
            timer -= SystemAPI.Time.DeltaTime;

            var spaceKeyDown = keyboard.tabKey.isPressed;
            if (spaceKeyDown)
            {
                //return;
            }
            
            if (spaceKeyDown && timer <= 0)
            {
                timer = 0.2f;
                //EditorApplication.isPaused = true;

                //Debug.Log("spaceKeyDown");
                Entities.ForEach((Entity entity, int entityInQueryIndex, ref SimpleSpawner spawner, in LocalToWorld localToWorld) =>
                {
                    //Debug.Log("foreach");
                    var e = EntityManager.Instantiate(spawner.Prefab);
                    EntityManager.SetName(e, "lightning spawned");
                    var position       = localToWorld.Position;
                    var localtransform = EntityManager.GetComponentData<LocalTransform>(spawner.Prefab);
                    
                    localtransform.Position = position;
                    EntityManager.SetComponentData (e, localtransform);
                    //Debug.Log(e);
                    
                }).WithStructuralChanges().WithoutBurst().Run();
            }
        }
    }
}