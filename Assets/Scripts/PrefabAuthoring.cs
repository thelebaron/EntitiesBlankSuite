using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class PrefabAuthoring : MonoBehaviour
{
    public class PrefabAuthoringBaker : Baker<PrefabAuthoring>
    {
        public override void Bake(PrefabAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new PrefabData());

            var stubs = FindObjectsByType<StubBehaviour>(FindObjectsSortMode.None);

            foreach (var stub in stubs)
            {
                var additionalEntity = CreateAdditionalEntity(TransformUsageFlags.ManualOverride);
                AddComponent(additionalEntity, new Stub());
                AddComponent(additionalEntity, new OtherData());
                
            }
        }
    }
    
    public class StubBehaviourBaker : Baker<StubBehaviour>
    {
        public override void Bake(StubBehaviour authoring)
        {
            
        }
    }
}

public struct PrefabData : IComponentData
{
    
}
public struct OtherData : IComponentData
{
    
}

public struct Stub : IComponentData
{
    
}