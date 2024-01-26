using System;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Junk.Entities
{
    /// <summary>
    /// A timer that increments by delta time.
    /// </summary>
    [Serializable]
    public struct Timer : IComponentData, IEnableableComponent
    {
        public float Value;
        
        public void Reset()
        {
            Value = 0;
        }
    }
    
    /// <summary>
    /// Simple component that counts down to 0 using delta time.
    /// </summary>
    public struct Cooldown : IComponentData
    {
        public float Value;
    }
    
    /// <summary>
    /// Simple component that counts down to 0 using delta time, when zero the entity will be destroyed.
    /// </summary>
    public struct LifeTime : IComponentData
    {
        public float Value;
        
        // implicit operator
        public static implicit operator float(LifeTime lifeTime)
        {
            return lifeTime.Value;
        }
    }
    
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    public partial struct TimeSystem : ISystem
    {
        private EntityQuery                   cooldownQuery;
        private EntityQuery                   lifeTimeQuery;
        private EntityQuery                   timerQuery;

        [BurstCompile]
        public void OnCreate(ref  SystemState state)
        {
            var builder = new EntityQueryBuilder(Allocator.Temp);
            builder.WithAllRW<Cooldown>();
            cooldownQuery      = state.GetEntityQuery(builder);
            
            builder            = new EntityQueryBuilder(Allocator.Temp);
            builder.WithAllRW<LifeTime>();
            lifeTimeQuery      = state.GetEntityQuery(builder);
            
            builder            = new EntityQueryBuilder(Allocator.Temp);
            builder.WithAllRW<Timer>();
            timerQuery         = state.GetEntityQuery(builder);
            
            var timeScaleEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(timeScaleEntity, new TimeScale {Value = 1});
        }
        
        [BurstCompile]
        private struct CooldownJob : IJobChunk
        {
            public float                         DeltaTime;
            public ComponentTypeHandle<Cooldown> CooldownTypeHandle;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var cooldowns = chunk.GetNativeArray<Cooldown>(ref CooldownTypeHandle);
                var count     = chunk.Count;
                for (var i = 0; i < count; i++)
                {
                    var cooldown = cooldowns[i];
                    cooldown = new Cooldown {Value = math.select(0, cooldown.Value - DeltaTime, cooldown.Value > 0)};
                    cooldowns[i] = cooldown;
                }
            }
        }
        
        [BurstCompile]
        private struct LifeTimeJob : IJobChunk
        {
            public float                         DeltaTime;
            public ComponentTypeHandle<LifeTime> LifeTimeTypeHandle;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var lifeTimes = chunk.GetNativeArray<LifeTime>(ref LifeTimeTypeHandle);
                var count     = chunk.Count;
                for (var i = 0; i < count; i++)
                {
                    var lifeTime = lifeTimes[i];
                    lifeTime = new LifeTime
                    {
                        Value = math.select(0, lifeTime.Value - DeltaTime, lifeTime.Value > 0)
                    };
                    lifeTimes[i] = lifeTime;
                }
            }
        }
        
        [BurstCompile]
        private struct TimerJob : IJobChunk
        {
            public float                      DeltaTime;
            public ComponentTypeHandle<Timer> TimerTypeHandle;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var timers = chunk.GetNativeArray<Timer>(ref TimerTypeHandle);
                var count  = chunk.Count;
                
                for (var i = 0; i < count; i++)
                {
                    var timer = timers[i];
                    timer.Value += DeltaTime;
                    timers[i] = timer;
                }
            }
        }
        
        
        [BurstCompile]
        public unsafe struct LifeTimeDestroyJob : IJobChunk
        {
            public ComponentTypeHandle<Destroy>  DestroyHandle;
            public ComponentTypeHandle<LifeTime> LifeTimeHandle;
            public float                         DeltaTime;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var remainings = (float*)chunk.GetRequiredComponentDataPtrRW(ref this.LifeTimeHandle);

                var e = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (e.NextEntityIndex(out var entityIndex))
                {
                    remainings[entityIndex] = math.max(0, remainings[entityIndex] - this.DeltaTime);
                    if (remainings[entityIndex] == 0)
                    {
                        chunk.SetComponentEnabled(ref this.DestroyHandle, entityIndex, true);
                    }
                }
            }
        }

        [BurstCompile]
        public void OnUpdate(ref  SystemState state)
        {
            state.Dependency = new CooldownJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                CooldownTypeHandle = SystemAPI.GetComponentTypeHandle<Cooldown>()
                
            }.Schedule(cooldownQuery, state.Dependency);
            
            /*state.Dependency = new LifeTimeJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                LifeTimeTypeHandle = SystemAPI.GetComponentTypeHandle<LifeTime>()
            }.Schedule(lifeTimeQuery, state.Dependency);*/
            
            state.Dependency = new TimerJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                TimerTypeHandle = SystemAPI.GetComponentTypeHandle<Timer>()
            }.Schedule(timerQuery, state.Dependency);
            
            state.Dependency = new LifeTimeDestroyJob
            {
                DestroyHandle  = SystemAPI.GetComponentTypeHandle<Destroy>(),
                LifeTimeHandle = SystemAPI.GetComponentTypeHandle<LifeTime>(),
                DeltaTime      = SystemAPI.Time.DeltaTime
            }.ScheduleParallel(lifeTimeQuery, state.Dependency);
        }
    }
}