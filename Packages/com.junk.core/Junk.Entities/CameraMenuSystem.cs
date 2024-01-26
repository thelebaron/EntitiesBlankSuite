﻿using Unity.Burst;
using Unity.Entities;
using UnityEngine;

namespace Junk.Entities
{
    /// <summary>
    /// todo use changefilter so we arent running this every frame
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct PlayerMenuSystem : ISystem
    {
        private float inputBlockTime;
        
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Game>();
        }
        
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (game, entity) in SystemAPI.Query<RefRO<Game>>().WithEntityAccess())
            {
                if (state.EntityManager.HasComponent<GameMenu>(entity))
                {
                    // if the menu is enabled, disable the first person camera
                    var menuIsActive = state.EntityManager.IsComponentEnabled<GameMenu>(entity);
                    
                    foreach (var (playerControl, e) in SystemAPI.Query<RefRO<PlayerMenuControl>>().WithEntityAccess().WithOptions(EntityQueryOptions.IgnoreComponentEnabledState))
                    {
                        state.EntityManager.SetComponentEnabled<PlayerMenuControl>(e, !menuIsActive);
                    }
                }

            }
        }
    }
}