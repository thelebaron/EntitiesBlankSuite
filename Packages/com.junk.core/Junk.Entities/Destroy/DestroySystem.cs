using Unity.Burst;
using Unity.Entities;

// Note this is 1:1 borrowed from tertle's excellent Core library
// Copyright (c) BovineLabs. All rights reserved.

namespace Junk.Entities
{
    [UpdateInGroup(typeof(DestroySystemGroup), OrderLast = true)]
    public partial struct DestroySystem : ISystem
    {
        private EntityQuery query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
#if UNITY_NETCODE
            // Client doesn't destroy ghosts, instead we'll disable them in
            this.query = Unity.NetCode.ClientServerWorldExtensions.IsClient(state.WorldUnmanaged)
                ? SystemAPI.QueryBuilder().WithAll<Destroy>().WithNone<Unity.NetCode.GhostInstance>().Build()
                : SystemAPI.QueryBuilder().WithAll<Destroy>().Build();
#else
            this.query = SystemAPI.QueryBuilder().WithAll<Destroy>().Build();
#endif
            this.query.SetChangedVersionFilter(ComponentType.ReadOnly<Destroy>());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var bufferSingleton = SystemAPI.GetSingleton<BeginInitializationEntityCommandBufferSystem.Singleton>();
            new DestroyJob { CommandBuffer = bufferSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter() }.ScheduleParallel(this.query);
        }

        [WithChangeFilter(typeof(Destroy))]
        [WithAll(typeof(Destroy))]
        [BurstCompile]
        private partial struct DestroyJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter CommandBuffer;

            private void Execute([ChunkIndexInQuery] int chunkIndexInQuery, Entity entity)
            {
                this.CommandBuffer.DestroyEntity(chunkIndexInQuery, entity);
            }
        }
    }
}