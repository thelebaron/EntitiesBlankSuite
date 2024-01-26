using System;
using Junk.Entities;
using Junk.Physics.Stateful;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace Junk.Physics
{
    public struct PhysicsMover : IComponentData, IEnableableComponent
    {
        public float             Distance;
        public float3            Axis;
        public PhysicsMoverState State;
        public float3            EndPosition;
        public float             Speed;
        public float             WaitTime;
        public Entity            TriggerEntity;
        //public Entity          MoverEntity;
        public bool   Reset;
        public Entity TriggeringEntity;
        public float  Delay;
    }
    
    /// <summary>
    /// When a player collides with this trigger, it will start the PhysicsMover.
    /// </summary>
    public struct PhysicsMoverTriggerData : IComponentData, IEnableableComponent
    {
        public Entity                  PhysicsMoverEntity;
        public PhysicsMoverTriggerType TriggerType;
        public float                   Delay;
    }
    
    /// <summary>
    /// When a PhysicsMover collides with this trigger, it will stop and reset itself.
    /// </summary>
    public struct PhysicsMoverStopTrigger : IComponentData, IEnableableComponent
    {
        public Entity PhysicsMoverEntity;
    }
    
    public enum PhysicsMoverState
    {
        Stopped,
        Moving, // Moving to end position
        Returning, // Returning to start position
    }
    public enum PhysicsMoverTriggerType
    {
        Start,
        Return,
        Stop,
    }

    [UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
    public partial struct PhysicsMoverSystem : ISystem
    {
        private EntityQuery triggerQuery;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<SimulationSingleton>();
            
            triggerQuery = new EntityQueryBuilder(Allocator.Temp)
                    .WithAll<StatefulTriggerEvent>()
                    .WithAll<PhysicsMoverTriggerData>()
                    .Build(ref state);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new MoveTriggerEventJob
            {
                PlayerLookup               = SystemAPI.GetComponentLookup<Player>(),
                PhysicsMoverLookup         = SystemAPI.GetComponentLookup<PhysicsMover>(),
                PhysicsMoverTriggerLookup  = SystemAPI.GetComponentLookup<PhysicsMoverTriggerData>(true),
                PhysicsVelocityLookup      = SystemAPI.GetComponentLookup<PhysicsVelocity>(),
                LocalTransformLookup       = SystemAPI.GetComponentLookup<LocalTransform>(),
                StatefulTriggerEventLookup = SystemAPI.GetBufferLookup<StatefulTriggerEvent>(true),
                EntityType           = SystemAPI.GetEntityTypeHandle(),
                StatefulTriggerEventType = SystemAPI.GetBufferTypeHandle<StatefulTriggerEvent>(true),
                PhysicsMoverTriggerDataType = SystemAPI.GetComponentTypeHandle<PhysicsMoverTriggerData>(true),
            }.Schedule(triggerQuery, state.Dependency);
            
            state.Dependency = new PhysicsMoverJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
            }.Schedule(state.Dependency);
        }

        
        [BurstCompile]
        public partial struct PhysicsMoverJob : IJobEntity
        {
            public float DeltaTime;
            public void Execute(Entity entity, ref PhysicsMover physicsMover, ref PhysicsVelocity physicsVelocity, in LocalTransform localTransform)
            {
                if(physicsMover.Delay > 0)
                {
                    physicsMover.Delay -= DeltaTime;
                    physicsVelocity.Linear = float3.zero;
                    return;
                }
                if (physicsMover.State == PhysicsMoverState.Moving)
                {
                    var speed = physicsMover.Axis * physicsMover.Speed;
                    physicsVelocity.Linear = math.lerp(physicsVelocity.Linear, speed, DeltaTime * 5f);
                }
                if (physicsMover.State == PhysicsMoverState.Returning)
                {
                    var speed = -physicsMover.Axis * physicsMover.Speed;
                    physicsVelocity.Linear = math.lerp(physicsVelocity.Linear, speed, DeltaTime * 5f);
                }
                if (physicsMover.State == PhysicsMoverState.Stopped)
                {
                    // lerp to zero
                    physicsVelocity.Linear = math.lerp(physicsVelocity.Linear, float3.zero, DeltaTime * 15f);
                }
            }
        }
        
        [BurstCompile]
        public struct MoveTriggerEventJob : IJobChunk
        {
            public            ComponentLookup<Player>              PlayerLookup;
            [ReadOnly] public ComponentLookup<PhysicsMoverTriggerData> PhysicsMoverTriggerLookup;
            public            ComponentLookup<PhysicsMover>        PhysicsMoverLookup;
            public            ComponentLookup<PhysicsVelocity>     PhysicsVelocityLookup;
            public            ComponentLookup<LocalTransform>      LocalTransformLookup;
            [ReadOnly] public BufferLookup<StatefulTriggerEvent>   StatefulTriggerEventLookup;
            
            [ReadOnly] public EntityTypeHandle                       EntityType;
            [ReadOnly] public BufferTypeHandle<StatefulTriggerEvent> StatefulTriggerEventType;
            [ReadOnly] public ComponentTypeHandle<PhysicsMoverTriggerData> PhysicsMoverTriggerDataType;
            
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(EntityType);
                var physicsMoverTriggerDatas = chunk.GetNativeArray(ref PhysicsMoverTriggerDataType);
                var statefulTriggerEvents = chunk.GetBufferAccessor(ref StatefulTriggerEventType);
                
                var entityEnumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (entityEnumerator.NextEntityIndex(out var i))
                {
                    var entity             = entities[i];
                    var triggerData = physicsMoverTriggerDatas[i];
                    var triggerEvents      = statefulTriggerEvents[i];
                    //var enabled             = chunk.IsComponentEnabled(ref StatefulTriggerEventType, i);
                    
                    for (var j = 0; j < triggerEvents.Length; j++)
                    {
                        ProcessTriggerEvents(j, entity, triggerData, triggerEvents);
                    }
                }
            }
            
            private void ProcessTriggerEvents(int   index, Entity entity, PhysicsMoverTriggerData triggerData,
                DynamicBuffer<StatefulTriggerEvent> triggerEvents)
            {
                var triggerEvent = triggerEvents[index];
                var otherEntity  = triggerEvent.GetOtherEntity(entity);
                //Debug.Log("Enter" + otherEntity);
                var moverEntity = PhysicsMoverTriggerLookup[entity].PhysicsMoverEntity;
                var mover       = PhysicsMoverLookup[moverEntity];
                
                switch (triggerData.TriggerType)
                {
                    // if the trigger is a start trigger, and the mover is idle, set the mover to moving
                    case PhysicsMoverTriggerType.Start:
                    {
                        if (triggerEvent.State == StatefulEventState.Enter) // && !emitterEnabled)
                        {
                            if (mover.State == PhysicsMoverState.Stopped)
                            {
                                mover.State                     = PhysicsMoverState.Moving;
                                mover.Delay                     = triggerData.Delay;
                                PhysicsMoverLookup[moverEntity] = mover;
                            }
                        }
                    }
                    break;
                    
                    // if the trigger is a return trigger, and the mover is moving, set the mover to waiting to return
                    case PhysicsMoverTriggerType.Return:

                        if (triggerEvent.State == StatefulEventState.Enter) // && !emitterEnabled)
                        {
                            if (mover.State == PhysicsMoverState.Moving)
                            {
                                mover.State                     = PhysicsMoverState.Returning;
                                mover.Delay                     = triggerData.Delay;
                                PhysicsMoverLookup[moverEntity] = mover;
                            }
                        }
                        break;
                    
                    // if the trigger is a stop trigger, and the mover is moving, set the mover to waiting to return
                    case PhysicsMoverTriggerType.Stop:
                        if (triggerEvent.State == StatefulEventState.Enter) // && !emitterEnabled)
                        {
                            if (mover.State == PhysicsMoverState.Returning)
                            {
                                mover.State                     = PhysicsMoverState.Stopped;
                                mover.Delay                     = triggerData.Delay;
                                PhysicsMoverLookup[moverEntity] = mover;
                            }
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                

            }
        }
    }
}